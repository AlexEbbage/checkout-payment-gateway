using System.Collections.Concurrent;
using PaymentGateway.Api.Application.Payments;
using PaymentGateway.Api.Application.Time;

namespace PaymentGateway.Api.Application.Idempotency;

public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, IdempotencyRecord> _records = new(StringComparer.Ordinal);
    private readonly IClock _clock;

    public InMemoryIdempotencyStore(IClock clock)
    {
        _clock = clock;
    }

    public Task<IdempotencyStartResult> TryStartAsync(
        string key,
        string requestHash,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);

        var newRecord = IdempotencyRecord.InProgress(
            key,
            requestHash,
            _clock.UtcNow);

        var existingOrNewRecord = _records.GetOrAdd(key, newRecord);

        if (ReferenceEquals(existingOrNewRecord, newRecord))
        {
            return Task.FromResult(IdempotencyStartResult.Started());
        }

        if (!string.Equals(existingOrNewRecord.RequestHash, requestHash, StringComparison.Ordinal))
        {
            return Task.FromResult(IdempotencyStartResult.ConflictDifferentPayload());
        }

        if (existingOrNewRecord.State == IdempotencyRecordState.InProgress)
        {
            return Task.FromResult(IdempotencyStartResult.InProgressSamePayload());
        }

        if (existingOrNewRecord.Result is null)
        {
            throw new InvalidOperationException("Completed idempotency record did not contain a result.");
        }

        return Task.FromResult(IdempotencyStartResult.CompletedSamePayload(existingOrNewRecord.Result));
    }

    public Task CompleteAsync(
        string key,
        string requestHash,
        PaymentProcessingResult result,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);

        while (true)
        {
            if (!_records.TryGetValue(key, out var existingRecord))
            {
                throw new InvalidOperationException("Cannot complete an idempotency record that has not been started.");
            }

            if (!string.Equals(existingRecord.RequestHash, requestHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Cannot complete an idempotency record with a different request hash.");
            }

            if (existingRecord.State == IdempotencyRecordState.Completed)
            {
                return Task.CompletedTask;
            }

            var completedRecord = existingRecord.Complete(result, _clock.UtcNow);

            if (_records.TryUpdate(key, completedRecord, existingRecord))
            {
                return Task.CompletedTask;
            }
        }
    }
}
