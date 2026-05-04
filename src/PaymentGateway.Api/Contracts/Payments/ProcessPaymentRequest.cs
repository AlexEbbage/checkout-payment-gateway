namespace PaymentGateway.Api.Contracts.Payments;

public sealed record ProcessPaymentRequest(
    string? CardNumber,
    int ExpiryMonth,
    int ExpiryYear,
    string? Currency,
    int Amount,
    string? Cvv);
