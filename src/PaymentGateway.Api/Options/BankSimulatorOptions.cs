namespace PaymentGateway.Api.Options;

public sealed class BankSimulatorOptions
{
    public const string SectionName = "BankSimulator";

    public string BaseUrl { get; init; } = "http://localhost:8080";

    public int TimeoutSeconds { get; init; } = 3;
}
