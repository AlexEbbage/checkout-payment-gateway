using PaymentGateway.Api.Contracts.Payments;

namespace PaymentGateway.Api.Application.Payments;

public interface IPaymentService
{
    Task<PaymentProcessingResult> ProcessPaymentAsync(
        ProcessPaymentRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken);

    Task<GetPaymentResponse?> GetPaymentAsync(
        Guid paymentId,
        CancellationToken cancellationToken);
}
