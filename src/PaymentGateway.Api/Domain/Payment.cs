namespace PaymentGateway.Api.Domain;

public sealed class Payment
{
    private Payment(
        Guid id,
        PaymentStatus status,
        string lastFourCardDigits,
        int expiryMonth,
        int expiryYear,
        string currency,
        int amount,
        string? authorizationCode,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        Status = status;
        LastFourCardDigits = lastFourCardDigits;
        ExpiryMonth = expiryMonth;
        ExpiryYear = expiryYear;
        Currency = currency;
        Amount = amount;
        AuthorizationCode = authorizationCode;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; }

    public PaymentStatus Status { get; }

    public string LastFourCardDigits { get; }

    public int ExpiryMonth { get; }

    public int ExpiryYear { get; }

    public string Currency { get; }

    public int Amount { get; }

    public string? AuthorizationCode { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public static Payment Create(
        PaymentStatus status,
        string cardNumber,
        int expiryMonth,
        int expiryYear,
        string currency,
        int amount,
        string? authorizationCode,
        DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        if (cardNumber.Length < 4)
        {
            throw new ArgumentException("Card number must contain at least four digits.", nameof(cardNumber));
        }

        return new Payment(
            Guid.NewGuid(),
            status,
            cardNumber[^4..],
            expiryMonth,
            expiryYear,
            currency.ToUpperInvariant(),
            amount,
            authorizationCode,
            createdAtUtc);
    }
}
