using System.Text.Json.Serialization;

namespace PaymentGateway.Api.Infrastructure.Banking;

public sealed record BankPaymentResponse(
    [property: JsonPropertyName("authorized")]
    bool Authorized,

    [property: JsonPropertyName("authorization_code")]
    string? AuthorizationCode);
