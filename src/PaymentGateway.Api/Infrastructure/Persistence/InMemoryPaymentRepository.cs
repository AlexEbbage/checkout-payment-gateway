using System.Collections.Concurrent;
using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Infrastructure.Persistence;

public sealed class InMemoryPaymentRepository : IPaymentRepository
{
    private readonly ConcurrentDictionary<Guid, Payment> _payments = new();

    public Task SaveAsync(
        Payment payment,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _payments[payment.Id] = payment;

        return Task.CompletedTask;
    }

    public Task<Payment?> GetAsync(
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _payments.TryGetValue(paymentId, out var payment);

        return Task.FromResult(payment);
    }
}
