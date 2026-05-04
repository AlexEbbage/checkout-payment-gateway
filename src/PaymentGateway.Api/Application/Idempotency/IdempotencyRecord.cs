using PaymentGateway.Api.Application.Payments;

namespace PaymentGateway.Api.Application.Idempotency;

public enum IdempotencyRecordState
{
    InProgress = 1,
    Completed = 2
}

public sealed record IdempotencyRecord(
    string Key,
    string RequestHash,
    IdempotencyRecordState State,
    PaymentProcessingResult? Result,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc)
{
    public static IdempotencyRecord InProgress(
        string key,
        string requestHash,
        DateTimeOffset createdAtUtc)
    {
        return new IdempotencyRecord(
            key,
            requestHash,
            IdempotencyRecordState.InProgress,
            Result: null,
            createdAtUtc,
            CompletedAtUtc: null);
    }

    public IdempotencyRecord Complete(
        PaymentProcessingResult result,
        DateTimeOffset completedAtUtc)
    {
        return this with
        {
            State = IdempotencyRecordState.Completed,
            Result = result,
            CompletedAtUtc = completedAtUtc
        };
    }
}
