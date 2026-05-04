using PaymentGateway.Api.Application.Time;

namespace PaymentGateway.Api.Tests.TestDoubles;

public sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 04, 30, 12, 0, 0, TimeSpan.Zero);
}
