namespace PaymentGateway.Api.Application.Payments;

public enum PaymentProcessingOutcome
{
    Succeeded = 1,
    Rejected = 2,
    IdempotencyConflict = 3,
    IdempotencyInProgress = 4,
    BankUnavailable = 5
}
