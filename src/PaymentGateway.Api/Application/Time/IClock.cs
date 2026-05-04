namespace PaymentGateway.Api.Application.Time;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
