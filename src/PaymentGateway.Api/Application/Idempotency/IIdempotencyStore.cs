using PaymentGateway.Api.Application.Payments;

namespace PaymentGateway.Api.Application.Idempotency;

public interface IIdempotencyStore
{
    Task<IdempotencyStartResult> TryStartAsync(
        string key,
        string requestHash,
        CancellationToken cancellationToken);

    Task CompleteAsync(
        string key,
        string requestHash,
        PaymentProcessingResult result,
        CancellationToken cancellationToken);
}
