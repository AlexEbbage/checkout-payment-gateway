using PaymentGateway.Api.Application.Payments;

namespace PaymentGateway.Api.Application.Idempotency;

public sealed record IdempotencyStartResult(
    IdempotencyStartOutcome Outcome,
    PaymentProcessingResult? ExistingResult = null)
{
    public static IdempotencyStartResult Started()
    {
        return new IdempotencyStartResult(IdempotencyStartOutcome.Started);
    }

    public static IdempotencyStartResult InProgressSamePayload()
    {
        return new IdempotencyStartResult(IdempotencyStartOutcome.InProgressSamePayload);
    }

    public static IdempotencyStartResult CompletedSamePayload(PaymentProcessingResult result)
    {
        return new IdempotencyStartResult(
            IdempotencyStartOutcome.CompletedSamePayload,
            result);
    }

    public static IdempotencyStartResult ConflictDifferentPayload()
    {
        return new IdempotencyStartResult(IdempotencyStartOutcome.ConflictDifferentPayload);
    }
}
