using System.Diagnostics;

namespace PaymentGateway.Api.Observability;

public static class PaymentTracing
{
    public const string SourceName = "PaymentGateway.Api";

    public static readonly ActivitySource ActivitySource = new(SourceName);
}
