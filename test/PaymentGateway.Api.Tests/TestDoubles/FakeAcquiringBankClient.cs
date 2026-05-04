using PaymentGateway.Api.Contracts.Payments;
using PaymentGateway.Api.Infrastructure.Banking;

namespace PaymentGateway.Api.Tests.TestDoubles;

public sealed class FakeAcquiringBankClient : IAcquiringBankClient
{
    private readonly TimeSpan _delay;

    public FakeAcquiringBankClient()
    {
    }

    public FakeAcquiringBankClient(TimeSpan delay)
    {
        _delay = delay;
    }

    public int CallCount { get; private set; }

    public BankPaymentResult Result { get; set; } = BankPaymentResult.Authorized("auth_123");

    public async Task<BankPaymentResult> ProcessPaymentAsync(
        ProcessPaymentRequest request,
        CancellationToken cancellationToken)
    {
        CallCount++;

        if (_delay > TimeSpan.Zero)
        {
            await Task.Delay(_delay, cancellationToken);
        }

        return Result;
    }
}
