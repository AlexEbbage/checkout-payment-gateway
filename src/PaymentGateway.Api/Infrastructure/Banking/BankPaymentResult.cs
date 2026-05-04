namespace PaymentGateway.Api.Infrastructure.Banking;

public sealed record BankPaymentResult(
    BankPaymentStatus Status,
    string? AuthorizationCode)
{
    public static BankPaymentResult Authorized(string authorizationCode)
    {
        return new BankPaymentResult(BankPaymentStatus.Authorized, authorizationCode);
    }

    public static BankPaymentResult Declined()
    {
        return new BankPaymentResult(BankPaymentStatus.Declined, null);
    }

    public static BankPaymentResult Unavailable()
    {
        return new BankPaymentResult(BankPaymentStatus.Unavailable, null);
    }
}
