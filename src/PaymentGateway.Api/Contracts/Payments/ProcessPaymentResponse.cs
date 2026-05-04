namespace PaymentGateway.Api.Contracts.Payments;

public sealed record ProcessPaymentResponse(
    Guid Id,
    string Status,
    string LastFourCardDigits,
    int ExpiryMonth,
    int ExpiryYear,
    string Currency,
    int Amount,
    string? AuthorizationCode);
