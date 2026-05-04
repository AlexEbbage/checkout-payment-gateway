namespace PaymentGateway.Api.Contracts.Payments;

public sealed record GetPaymentResponse(
    Guid Id,
    string Status,
    string LastFourCardDigits,
    int ExpiryMonth,
    int ExpiryYear,
    string Currency,
    int Amount,
    string? AuthorizationCode,
    DateTimeOffset CreatedAtUtc);
