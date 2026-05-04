namespace PaymentGateway.Api.Application.Idempotency;

public enum IdempotencyStartOutcome
{
    Started = 1,
    InProgressSamePayload = 2,
    CompletedSamePayload = 3,
    ConflictDifferentPayload = 4
}
