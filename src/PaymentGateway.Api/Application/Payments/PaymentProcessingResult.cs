using PaymentGateway.Api.Contracts;
using PaymentGateway.Api.Contracts.Payments;
using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Application.Payments;

public sealed record PaymentProcessingResult(
    PaymentProcessingOutcome Outcome,
    ProcessPaymentResponse? Response = null,
    IReadOnlyCollection<ValidationError>? ValidationErrors = null,
    string? ErrorMessage = null)
{
    public static PaymentProcessingResult Succeeded(Payment payment)
    {
        return new PaymentProcessingResult(
            PaymentProcessingOutcome.Succeeded,
            ToProcessPaymentResponse(payment));
    }

    public static PaymentProcessingResult Succeeded(ProcessPaymentResponse response)
    {
        return new PaymentProcessingResult(
            PaymentProcessingOutcome.Succeeded,
            response);
    }

    public static PaymentProcessingResult Rejected(IReadOnlyCollection<ValidationError> errors)
    {
        return new PaymentProcessingResult(
            PaymentProcessingOutcome.Rejected,
            ValidationErrors: errors);
    }

    public static PaymentProcessingResult IdempotencyConflict()
    {
        return new PaymentProcessingResult(
            PaymentProcessingOutcome.IdempotencyConflict,
            ErrorMessage: "The supplied idempotency key has already been used with a different request payload.");
    }

    public static PaymentProcessingResult IdempotencyInProgress()
    {
        return new PaymentProcessingResult(
            PaymentProcessingOutcome.IdempotencyInProgress,
            ErrorMessage: "A request with this idempotency key is already being processed. Retry shortly using the same key.");
    }

    public static PaymentProcessingResult BankUnavailable()
    {
        return new PaymentProcessingResult(
            PaymentProcessingOutcome.BankUnavailable,
            ErrorMessage: "The payment could not be processed because the acquiring bank was unavailable.");
    }

    public static ProcessPaymentResponse ToProcessPaymentResponse(Payment payment)
    {
        return new ProcessPaymentResponse(
            payment.Id,
            payment.Status.ToString(),
            payment.LastFourCardDigits,
            payment.ExpiryMonth,
            payment.ExpiryYear,
            payment.Currency,
            payment.Amount,
            payment.AuthorizationCode);
    }
}
