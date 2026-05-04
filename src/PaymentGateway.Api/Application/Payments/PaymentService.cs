using System.Diagnostics;
using PaymentGateway.Api.Application.Idempotency;
using PaymentGateway.Api.Application.Time;
using PaymentGateway.Api.Contracts.Payments;
using PaymentGateway.Api.Domain;
using PaymentGateway.Api.Infrastructure.Banking;
using PaymentGateway.Api.Infrastructure.Persistence;
using PaymentGateway.Api.Observability;

namespace PaymentGateway.Api.Application.Payments;

public sealed class PaymentService : IPaymentService
{
    private readonly PaymentRequestValidator _validator;
    private readonly IAcquiringBankClient _bankClient;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly IClock _clock;
    private readonly PaymentMetrics _metrics;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        PaymentRequestValidator validator,
        IAcquiringBankClient bankClient,
        IPaymentRepository paymentRepository,
        IIdempotencyStore idempotencyStore,
        IClock clock,
        PaymentMetrics metrics,
        ILogger<PaymentService> logger)
    {
        _validator = validator;
        _bankClient = bankClient;
        _paymentRepository = paymentRepository;
        _idempotencyStore = idempotencyStore;
        _clock = clock;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<PaymentProcessingResult> ProcessPaymentAsync(
        ProcessPaymentRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        using var activity = PaymentTracing.ActivitySource.StartActivity(
            "PaymentService.ProcessPayment",
            ActivityKind.Internal);

        var stopwatch = Stopwatch.StartNew();
        var processingOutcome = "unknown";

        try
        {
            var validationErrors = _validator.Validate(request);

            if (validationErrors.Count > 0)
            {
                _metrics.RecordPaymentRejected("validation");
                processingOutcome = "rejected";

                _logger.LogInformation(
                    "Payment request rejected with {ValidationErrorCount} validation errors",
                    validationErrors.Count);

                return PaymentProcessingResult.Rejected(validationErrors);
            }

            var normalizedRequest = Normalize(request);
            var requestHash = IdempotencyHasher.Hash(normalizedRequest);

            activity?.SetTag("payment.amount", normalizedRequest.Amount);
            activity?.SetTag("payment.currency", normalizedRequest.Currency);
            activity?.SetTag("payment.has_idempotency_key", !string.IsNullOrWhiteSpace(idempotencyKey));

            var shouldCompleteIdempotencyRecord = false;

            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var idempotencyStart = await _idempotencyStore.TryStartAsync(
                    idempotencyKey,
                    requestHash,
                    cancellationToken);

                switch (idempotencyStart.Outcome)
                {
                    case IdempotencyStartOutcome.Started:
                        shouldCompleteIdempotencyRecord = true;
                        break;

                    case IdempotencyStartOutcome.CompletedSamePayload:
                        _metrics.RecordIdempotencyOutcome("replay");
                        processingOutcome = "idempotency_replay";

                        _logger.LogInformation(
                            "Returning completed idempotent response for idempotency key hash {IdempotencyKeyHash}",
                            IdempotencyHasher.Hash(idempotencyKey));

                        return idempotencyStart.ExistingResult!;

                    case IdempotencyStartOutcome.InProgressSamePayload:
                        _metrics.RecordIdempotencyOutcome("in_progress");
                        processingOutcome = "idempotency_in_progress";

                        _logger.LogWarning(
                            "Idempotent request already in progress for idempotency key hash {IdempotencyKeyHash}",
                            IdempotencyHasher.Hash(idempotencyKey));

                        return PaymentProcessingResult.IdempotencyInProgress();

                    case IdempotencyStartOutcome.ConflictDifferentPayload:
                        _metrics.RecordIdempotencyOutcome("conflict");
                        processingOutcome = "idempotency_conflict";

                        _logger.LogWarning(
                            "Idempotency conflict for idempotency key hash {IdempotencyKeyHash}",
                            IdempotencyHasher.Hash(idempotencyKey));

                        return PaymentProcessingResult.IdempotencyConflict();

                    default:
                        throw new InvalidOperationException($"Unsupported idempotency start outcome: {idempotencyStart.Outcome}");
                }
            }

            _logger.LogInformation(
                "Sending payment request to acquiring bank for {Amount} {Currency}",
                normalizedRequest.Amount,
                normalizedRequest.Currency);

            var bankResult = await _bankClient.ProcessPaymentAsync(normalizedRequest, cancellationToken);

            if (bankResult.Status == BankPaymentStatus.Unavailable)
            {
                var unavailableResult = PaymentProcessingResult.BankUnavailable();

                _metrics.RecordPaymentFailed("bank_unavailable");
                processingOutcome = "bank_unavailable";

                if (shouldCompleteIdempotencyRecord)
                {
                    await _idempotencyStore.CompleteAsync(
                        idempotencyKey!,
                        requestHash,
                        unavailableResult,
                        cancellationToken);
                }

                _logger.LogWarning("Acquiring bank unavailable while processing payment");

                return unavailableResult;
            }

            var paymentStatus = bankResult.Status switch
            {
                BankPaymentStatus.Authorized => PaymentStatus.Authorized,
                BankPaymentStatus.Declined => PaymentStatus.Declined,
                _ => throw new InvalidOperationException($"Unsupported bank payment status: {bankResult.Status}")
            };

            var payment = Payment.Create(
                paymentStatus,
                normalizedRequest.CardNumber!,
                normalizedRequest.ExpiryMonth,
                normalizedRequest.ExpiryYear,
                normalizedRequest.Currency!,
                normalizedRequest.Amount,
                bankResult.AuthorizationCode,
                _clock.UtcNow);

            await _paymentRepository.SaveAsync(payment, cancellationToken);

            var successResult = PaymentProcessingResult.Succeeded(payment);

            if (shouldCompleteIdempotencyRecord)
            {
                await _idempotencyStore.CompleteAsync(
                    idempotencyKey!,
                    requestHash,
                    successResult,
                    cancellationToken);
            }

            _metrics.RecordPaymentProcessed(payment.Status, payment.Currency);
            processingOutcome = "succeeded";

            _logger.LogInformation(
                "Payment {PaymentId} processed with status {PaymentStatus}",
                payment.Id,
                payment.Status);

            activity?.SetTag("payment.id", payment.Id);
            activity?.SetTag("payment.status", payment.Status.ToString());

            return successResult;
        }
        finally
        {
            stopwatch.Stop();
            _metrics.RecordPaymentProcessingDuration(stopwatch.Elapsed.TotalMilliseconds, processingOutcome);
        }
    }

    public async Task<GetPaymentResponse?> GetPaymentAsync(
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        using var activity = PaymentTracing.ActivitySource.StartActivity(
            "PaymentService.GetPayment",
            ActivityKind.Internal);

        activity?.SetTag("payment.id", paymentId);

        var payment = await _paymentRepository.GetAsync(paymentId, cancellationToken);

        if (payment is null)
        {
            return null;
        }

        return new GetPaymentResponse(
            payment.Id,
            payment.Status.ToString(),
            payment.LastFourCardDigits,
            payment.ExpiryMonth,
            payment.ExpiryYear,
            payment.Currency,
            payment.Amount,
            payment.AuthorizationCode,
            payment.CreatedAtUtc);
    }

    private static ProcessPaymentRequest Normalize(ProcessPaymentRequest request)
    {
        return request with
        {
            CardNumber = request.CardNumber?.Trim(),
            Currency = request.Currency?.Trim().ToUpperInvariant(),
            Cvv = request.Cvv?.Trim()
        };
    }
}
