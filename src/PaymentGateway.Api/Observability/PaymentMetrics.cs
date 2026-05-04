using System.Diagnostics.Metrics;
using PaymentGateway.Api.Domain;
using PaymentGateway.Api.Infrastructure.Banking;

namespace PaymentGateway.Api.Observability;

public sealed class PaymentMetrics
{
    private readonly Meter _meter;

    private readonly Counter<long> _authorizedPayments;
    private readonly Counter<long> _declinedPayments;
    private readonly Counter<long> _rejectedPayments;
    private readonly Counter<long> _bankUnavailablePayments;
    private readonly Counter<long> _idempotencyReplays;
    private readonly Counter<long> _idempotencyConflicts;
    private readonly Counter<long> _idempotencyInProgress;

    private readonly Histogram<double> _paymentProcessingDurationMs;
    private readonly Histogram<double> _bankRequestDurationMs;

    public PaymentMetrics()
    {
        _meter = new Meter("PaymentGateway.Api.Payments", "1.0.0");

        _authorizedPayments = _meter.CreateCounter<long>("payments.authorized.count");
        _declinedPayments = _meter.CreateCounter<long>("payments.declined.count");
        _rejectedPayments = _meter.CreateCounter<long>("payments.rejected.count");
        _bankUnavailablePayments = _meter.CreateCounter<long>("payments.bank_unavailable.count");
        _idempotencyReplays = _meter.CreateCounter<long>("payments.idempotency.replay.count");
        _idempotencyConflicts = _meter.CreateCounter<long>("payments.idempotency.conflict.count");
        _idempotencyInProgress = _meter.CreateCounter<long>("payments.idempotency.in_progress.count");

        _paymentProcessingDurationMs = _meter.CreateHistogram<double>("payments.processing.duration_ms");
        _bankRequestDurationMs = _meter.CreateHistogram<double>("payments.bank_request.duration_ms");
    }

    public void RecordPaymentProcessed(PaymentStatus status)
    {
        if (status == PaymentStatus.Authorized)
        {
            _authorizedPayments.Add(1);
            return;
        }

        if (status == PaymentStatus.Declined)
        {
            _declinedPayments.Add(1);
        }
    }

    public void RecordPaymentRejected()
    {
        _rejectedPayments.Add(1);
    }

    public void RecordBankUnavailable()
    {
        _bankUnavailablePayments.Add(1);
    }

    public void RecordIdempotencyReplay()
    {
        _idempotencyReplays.Add(1);
    }

    public void RecordIdempotencyConflict()
    {
        _idempotencyConflicts.Add(1);
    }

    public void RecordIdempotencyInProgress()
    {
        _idempotencyInProgress.Add(1);
    }

    public void RecordPaymentProcessingDuration(double durationMs)
    {
        _paymentProcessingDurationMs.Record(durationMs);
    }

    public void RecordBankRequestDuration(double durationMs, BankPaymentStatus status)
    {
        _bankRequestDurationMs.Record(
            durationMs,
            new KeyValuePair<string, object?>("bank_payment_status", status.ToString()));
    }
}
