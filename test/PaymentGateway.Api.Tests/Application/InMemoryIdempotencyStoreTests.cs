using PaymentGateway.Api.Application.Idempotency;
using PaymentGateway.Api.Application.Payments;
using PaymentGateway.Api.Contracts.Payments;
using PaymentGateway.Api.Tests.TestDoubles;

namespace PaymentGateway.Api.Tests.Application;

public sealed class InMemoryIdempotencyStoreTests
{
    [Fact]
    public async Task TryStartAsync_WhenKeyDoesNotExist_ReturnsStarted()
    {
        var store = new InMemoryIdempotencyStore(new FakeClock());

        var result = await store.TryStartAsync(
            "key-1",
            "hash-1",
            CancellationToken.None);

        result.Outcome.Should().Be(IdempotencyStartOutcome.Started);
    }

    [Fact]
    public async Task TryStartAsync_WhenSameKeyAndSameHashIsInProgress_ReturnsInProgressSamePayload()
    {
        var store = new InMemoryIdempotencyStore(new FakeClock());

        await store.TryStartAsync("key-1", "hash-1", CancellationToken.None);

        var result = await store.TryStartAsync(
            "key-1",
            "hash-1",
            CancellationToken.None);

        result.Outcome.Should().Be(IdempotencyStartOutcome.InProgressSamePayload);
    }

    [Fact]
    public async Task TryStartAsync_WhenSameKeyAndDifferentHash_ReturnsConflictDifferentPayload()
    {
        var store = new InMemoryIdempotencyStore(new FakeClock());

        await store.TryStartAsync("key-1", "hash-1", CancellationToken.None);

        var result = await store.TryStartAsync(
            "key-1",
            "hash-2",
            CancellationToken.None);

        result.Outcome.Should().Be(IdempotencyStartOutcome.ConflictDifferentPayload);
    }

    [Fact]
    public async Task TryStartAsync_WhenCompletedWithSameHash_ReturnsCompletedSamePayload()
    {
        var store = new InMemoryIdempotencyStore(new FakeClock());

        await store.TryStartAsync("key-1", "hash-1", CancellationToken.None);

        var paymentResponse = new ProcessPaymentResponse(
            Guid.NewGuid(),
            "Authorized",
            "4241",
            12,
            2030,
            "GBP",
            1000,
            "auth_123");

        await store.CompleteAsync(
            "key-1",
            "hash-1",
            PaymentProcessingResult.Succeeded(paymentResponse),
            CancellationToken.None);

        var result = await store.TryStartAsync(
            "key-1",
            "hash-1",
            CancellationToken.None);

        result.Outcome.Should().Be(IdempotencyStartOutcome.CompletedSamePayload);
        result.ExistingResult!.Response!.Id.Should().Be(paymentResponse.Id);
    }

    [Fact]
    public async Task TryStartAsync_WhenCalledConcurrently_OnlyOneCallStarts()
    {
        var store = new InMemoryIdempotencyStore(new FakeClock());

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => store.TryStartAsync("key-1", "hash-1", CancellationToken.None))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        results.Count(result => result.Outcome == IdempotencyStartOutcome.Started).Should().Be(1);
        results.Count(result => result.Outcome == IdempotencyStartOutcome.InProgressSamePayload).Should().Be(19);
    }
}
