using Microsoft.Extensions.Logging.Abstractions;
using PaymentGateway.Api.Application.Idempotency;
using PaymentGateway.Api.Application.Payments;
using PaymentGateway.Api.Contracts.Payments;
using PaymentGateway.Api.Infrastructure.Banking;
using PaymentGateway.Api.Infrastructure.Persistence;
using PaymentGateway.Api.Observability;
using PaymentGateway.Api.Tests.TestDoubles;

namespace PaymentGateway.Api.Tests.Application;

public sealed class PaymentServiceTests
{
    [Fact]
    public async Task ProcessPaymentAsync_WhenRequestIsInvalid_DoesNotCallBank()
    {
        var bankClient = new FakeAcquiringBankClient();
        var service = CreateService(bankClient);

        var request = ValidRequest() with { CardNumber = "" };

        var result = await service.ProcessPaymentAsync(
            request,
            idempotencyKey: null,
            CancellationToken.None);

        result.Outcome.Should().Be(PaymentProcessingOutcome.Rejected);
        bankClient.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task ProcessPaymentAsync_WhenBankAuthorizes_StoresAuthorizedPayment()
    {
        var bankClient = new FakeAcquiringBankClient
        {
            Result = BankPaymentResult.Authorized("auth_123")
        };

        var repository = new InMemoryPaymentRepository();
        var service = CreateService(bankClient, repository);

        var result = await service.ProcessPaymentAsync(
            ValidRequest(),
            idempotencyKey: null,
            CancellationToken.None);

        result.Outcome.Should().Be(PaymentProcessingOutcome.Succeeded);
        result.Response!.Status.Should().Be("Authorized");
        result.Response.AuthorizationCode.Should().Be("auth_123");
        result.Response.LastFourCardDigits.Should().Be("4241");

        var storedPayment = await repository.GetAsync(result.Response.Id, CancellationToken.None);
        storedPayment.Should().NotBeNull();
        storedPayment!.Status.ToString().Should().Be("Authorized");
    }

    [Fact]
    public async Task ProcessPaymentAsync_WhenBankDeclines_StoresDeclinedPayment()
    {
        var bankClient = new FakeAcquiringBankClient
        {
            Result = BankPaymentResult.Declined()
        };

        var service = CreateService(bankClient);

        var result = await service.ProcessPaymentAsync(
            ValidRequest(),
            idempotencyKey: null,
            CancellationToken.None);

        result.Outcome.Should().Be(PaymentProcessingOutcome.Succeeded);
        result.Response!.Status.Should().Be("Declined");
        result.Response.AuthorizationCode.Should().BeNull();
    }

    [Fact]
    public async Task ProcessPaymentAsync_WhenBankUnavailable_ReturnsBankUnavailable()
    {
        var bankClient = new FakeAcquiringBankClient
        {
            Result = BankPaymentResult.Unavailable()
        };

        var service = CreateService(bankClient);

        var result = await service.ProcessPaymentAsync(
            ValidRequest(),
            idempotencyKey: null,
            CancellationToken.None);

        result.Outcome.Should().Be(PaymentProcessingOutcome.BankUnavailable);
    }

    [Fact]
    public async Task ProcessPaymentAsync_WhenSameIdempotencyKeyAndPayloadAfterCompletion_ReturnsOriginalResponseAndDoesNotCallBankAgain()
    {
        var bankClient = new FakeAcquiringBankClient
        {
            Result = BankPaymentResult.Authorized("auth_123")
        };

        var service = CreateService(bankClient);

        var request = ValidRequest();

        var firstResult = await service.ProcessPaymentAsync(
            request,
            "idem-key-1",
            CancellationToken.None);

        var secondResult = await service.ProcessPaymentAsync(
            request,
            "idem-key-1",
            CancellationToken.None);

        firstResult.Outcome.Should().Be(PaymentProcessingOutcome.Succeeded);
        secondResult.Outcome.Should().Be(PaymentProcessingOutcome.Succeeded);
        secondResult.Response!.Id.Should().Be(firstResult.Response!.Id);
        bankClient.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task ProcessPaymentAsync_WhenSameIdempotencyKeyAndDifferentPayload_ReturnsConflict()
    {
        var bankClient = new FakeAcquiringBankClient();
        var service = CreateService(bankClient);

        await service.ProcessPaymentAsync(
            ValidRequest(),
            "idem-key-1",
            CancellationToken.None);

        var changedRequest = ValidRequest() with { Amount = 2000 };

        var secondResult = await service.ProcessPaymentAsync(
            changedRequest,
            "idem-key-1",
            CancellationToken.None);

        secondResult.Outcome.Should().Be(PaymentProcessingOutcome.IdempotencyConflict);
        bankClient.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task ProcessPaymentAsync_WhenSameIdempotencyKeyIsAlreadyProcessing_ReturnsInProgressAndDoesNotCallBankTwice()
    {
        var bankClient = new FakeAcquiringBankClient(TimeSpan.FromMilliseconds(100))
        {
            Result = BankPaymentResult.Authorized("auth_123")
        };

        var service = CreateService(bankClient);

        var request = ValidRequest();

        var firstTask = service.ProcessPaymentAsync(
            request,
            "idem-key-1",
            CancellationToken.None);

        await Task.Delay(20);

        var secondResult = await service.ProcessPaymentAsync(
            request,
            "idem-key-1",
            CancellationToken.None);

        var firstResult = await firstTask;

        firstResult.Outcome.Should().Be(PaymentProcessingOutcome.Succeeded);
        secondResult.Outcome.Should().Be(PaymentProcessingOutcome.IdempotencyInProgress);
        bankClient.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetPaymentAsync_WhenPaymentExists_ReturnsMaskedPayment()
    {
        var bankClient = new FakeAcquiringBankClient();
        var service = CreateService(bankClient);

        var processResult = await service.ProcessPaymentAsync(
            ValidRequest(),
            idempotencyKey: null,
            CancellationToken.None);

        var getResult = await service.GetPaymentAsync(
            processResult.Response!.Id,
            CancellationToken.None);

        getResult.Should().NotBeNull();
        getResult!.LastFourCardDigits.Should().Be("4241");
        getResult.Status.Should().Be("Authorized");
    }

    private static PaymentService CreateService(
        FakeAcquiringBankClient bankClient,
        IPaymentRepository? repository = null)
    {
        var clock = new FakeClock();
        var validator = new PaymentRequestValidator(clock);
        var metrics = new PaymentMetrics();

        return new PaymentService(
            validator,
            bankClient,
            repository ?? new InMemoryPaymentRepository(),
            new InMemoryIdempotencyStore(clock),
            clock,
            metrics,
            NullLogger<PaymentService>.Instance);
    }

    private static ProcessPaymentRequest ValidRequest()
    {
        return new ProcessPaymentRequest(
            "4242424242424241",
            12,
            2030,
            "GBP",
            1000,
            "123");
    }
}
