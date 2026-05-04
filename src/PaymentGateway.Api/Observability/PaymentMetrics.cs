using System.Diagnostics.Metrics;
using PaymentGateway.Api.Domain;
using PaymentGateway.Api.Infrastructure.Banking;

namespace PaymentGateway.Api.Observability;

public sealed class PaymentMetrics
{
    public const string MeterName = "PaymentGateway.Api.Payments";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    private static readonly Counter<long> PaymentsProcessed =
        Meter.CreateCounter<long>(
            name: "payments.processed",
            unit: "{payment}",
            description: "Number of payment requests processed by the gateway.");

    private static readonly Counter<long> PaymentsRejected =
        Meter.CreateCounter<long>(
            name: "payments.rejected",
            unit: "{payment}",
            description: "Number of payment requests rejected before reaching the acquiring bank.");

    private static readonly Counter<long> PaymentsFailed =
        Meter.CreateCounter<long>(
            name: "payments.failed",
            unit: "{payment}",
            description: "Number of payment requests that failed due to dependency or infrastructure errors.");

    private static readonly Counter<long> IdempotencyRequests =
        Meter.CreateCounter<long>(
            name: "payments.idempotency.requests",
            unit: "{request}",
            description: "Number of idempotency outcomes observed by the gateway.");

    private static readonly Histogram<double> PaymentProcessingDuration =
        Meter.CreateHistogram<double>(
            name: "payments.processing.duration",
            unit: "ms",
            description: "Duration of payment processing inside the gateway.");

    private static readonly Histogram<double> BankRequestDuration =
        Meter.CreateHistogram<double>(
            name: "payments.bank_request.duration",
            unit: "ms",
            description: "Duration of acquiring bank payment requests.");

    public void RecordPaymentProcessed(PaymentStatus status, string currency)
    {
        PaymentsProcessed.Add(
            1,
            new KeyValuePair<string, object?>("status", status.ToString().ToLowerInvariant()),
            new KeyValuePair<string, object?>("currency", currency.ToUpperInvariant()));
    }

    public void RecordPaymentRejected(string reason)
    {
        PaymentsRejected.Add(
            1,
            new KeyValuePair<string, object?>("reason", reason));
    }

    public void RecordPaymentFailed(string reason)
    {
        PaymentsFailed.Add(
            1,
            new KeyValuePair<string, object?>("reason", reason));
    }

    public void RecordIdempotencyOutcome(string outcome)
    {
        IdempotencyRequests.Add(
            1,
            new KeyValuePair<string, object?>("outcome", outcome));
    }

    public void RecordPaymentProcessingDuration(double durationMs, string outcome)
    {
        PaymentProcessingDuration.Record(
            durationMs,
            new KeyValuePair<string, object?>("outcome", outcome));
    }

    public void RecordBankRequestDuration(double durationMs, BankPaymentStatus status)
    {
        BankRequestDuration.Record(
            durationMs,
            new KeyValuePair<string, object?>("status", status.ToString().ToLowerInvariant()));
    }
}
