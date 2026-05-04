namespace PaymentGateway.Api.Contracts;

public sealed record ValidationError(
    string Field,
    string Message);
