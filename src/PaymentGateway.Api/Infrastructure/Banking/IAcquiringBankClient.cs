using PaymentGateway.Api.Contracts.Payments;

namespace PaymentGateway.Api.Infrastructure.Banking;

public interface IAcquiringBankClient
{
    Task<BankPaymentResult> ProcessPaymentAsync(
        ProcessPaymentRequest request,
        CancellationToken cancellationToken);
}
