using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PaymentGateway.Api.Contracts.Payments;
using PaymentGateway.Api.Infrastructure.Banking;
using PaymentGateway.Api.Observability;

namespace PaymentGateway.Api.Tests.Infrastructure;

public sealed class AcquiringBankClientTests
{
    [Fact]
    public async Task ProcessPaymentAsync_WhenBankAuthorizes_ReturnsAuthorized()
    {
        var httpClient = CreateHttpClient(
            HttpStatusCode.OK,
            new BankPaymentResponse(true, "auth_123"));

        var client = new AcquiringBankClient(
            httpClient,
            new PaymentMetrics(),
            NullLogger<AcquiringBankClient>.Instance);

        var result = await client.ProcessPaymentAsync(
            ValidRequest(),
            CancellationToken.None);

        result.Status.Should().Be(BankPaymentStatus.Authorized);
        result.AuthorizationCode.Should().Be("auth_123");
    }

    [Fact]
    public async Task ProcessPaymentAsync_WhenBankDeclines_ReturnsDeclined()
    {
        var httpClient = CreateHttpClient(
            HttpStatusCode.OK,
            new BankPaymentResponse(false, null));

        var client = new AcquiringBankClient(
            httpClient,
            new PaymentMetrics(),
            NullLogger<AcquiringBankClient>.Instance);

        var result = await client.ProcessPaymentAsync(
            ValidRequest(),
            CancellationToken.None);

        result.Status.Should().Be(BankPaymentStatus.Declined);
    }

    [Fact]
    public async Task ProcessPaymentAsync_WhenBankReturns503_ReturnsUnavailable()
    {
        var httpClient = CreateHttpClient(
            HttpStatusCode.ServiceUnavailable,
            body: null);

        var client = new AcquiringBankClient(
            httpClient,
            new PaymentMetrics(),
            NullLogger<AcquiringBankClient>.Instance);

        var result = await client.ProcessPaymentAsync(
            ValidRequest(),
            CancellationToken.None);

        result.Status.Should().Be(BankPaymentStatus.Unavailable);
    }

    [Fact]
    public async Task ProcessPaymentAsync_WhenBankReturns500_ReturnsUnavailable()
    {
        var httpClient = CreateHttpClient(
            HttpStatusCode.InternalServerError,
            body: null);

        var client = new AcquiringBankClient(
            httpClient,
            new PaymentMetrics(),
            NullLogger<AcquiringBankClient>.Instance);

        var result = await client.ProcessPaymentAsync(
            ValidRequest(),
            CancellationToken.None);

        result.Status.Should().Be(BankPaymentStatus.Unavailable);
    }

    private static HttpClient CreateHttpClient(
        HttpStatusCode statusCode,
        BankPaymentResponse? body)
    {
        var handler = new StubHttpMessageHandler(async _ =>
        {
            var response = new HttpResponseMessage(statusCode);

            if (body is not null)
            {
                response.Content = JsonContent.Create(body);
            }

            return await Task.FromResult(response);
        });

        return new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:8080")
        };
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

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return _handler(request);
        }
    }
}
