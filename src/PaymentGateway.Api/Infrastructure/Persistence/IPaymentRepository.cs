using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Infrastructure.Persistence;

public interface IPaymentRepository
{
    Task SaveAsync(
        Payment payment,
        CancellationToken cancellationToken);

    Task<Payment?> GetAsync(
        Guid paymentId,
        CancellationToken cancellationToken);
}
