using PaymentGateway.Api.Application.Time;
using PaymentGateway.Api.Contracts;
using PaymentGateway.Api.Contracts.Payments;

namespace PaymentGateway.Api.Application.Payments;

public sealed class PaymentRequestValidator
{
    private static readonly HashSet<string> SupportedCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "GBP",
        "USD",
        "EUR"
    };

    private readonly IClock _clock;

    public PaymentRequestValidator(IClock clock)
    {
        _clock = clock;
    }

    public IReadOnlyCollection<ValidationError> Validate(ProcessPaymentRequest request)
    {
        var errors = new List<ValidationError>();

        ValidateCardNumber(request.CardNumber, errors);
        ValidateExpiry(request.ExpiryMonth, request.ExpiryYear, errors);
        ValidateCurrency(request.Currency, errors);
        ValidateAmount(request.Amount, errors);
        ValidateCvv(request.Cvv, errors);

        return errors;
    }

    private static void ValidateCardNumber(string? cardNumber, ICollection<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(cardNumber))
        {
            errors.Add(new ValidationError(nameof(ProcessPaymentRequest.CardNumber), "Card number is required."));
            return;
        }

        if (cardNumber.Length is < 14 or > 19)
        {
            errors.Add(new ValidationError(nameof(ProcessPaymentRequest.CardNumber), "Card number must be between 14 and 19 digits."));
        }

        if (!cardNumber.All(char.IsDigit))
        {
            errors.Add(new ValidationError(nameof(ProcessPaymentRequest.CardNumber), "Card number must contain digits only."));
        }
    }

    private void ValidateExpiry(int expiryMonth, int expiryYear, ICollection<ValidationError> errors)
    {
        if (expiryMonth is < 1 or > 12)
        {
            errors.Add(new ValidationError(nameof(ProcessPaymentRequest.ExpiryMonth), "Expiry month must be between 1 and 12."));
            return;
        }

        if (expiryYear < 1)
        {
            errors.Add(new ValidationError(nameof(ProcessPaymentRequest.ExpiryYear), "Expiry year is invalid."));
            return;
        }

        var now = _clock.UtcNow;

        var expiryEndOfMonth = new DateTimeOffset(
            expiryYear,
            expiryMonth,
            DateTime.DaysInMonth(expiryYear, expiryMonth),
            23,
            59,
            59,
            TimeSpan.Zero);

        if (expiryEndOfMonth < now)
        {
            errors.Add(new ValidationError(nameof(ProcessPaymentRequest.ExpiryYear), "Card expiry date must be in the future."));
        }
    }

    private static void ValidateCurrency(string? currency, ICollection<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            errors.Add(new ValidationError(nameof(ProcessPaymentRequest.Currency), "Currency is required."));
            return;
        }

        if (currency.Length != 3)
        {
            errors.Add(new ValidationError(nameof(ProcessPaymentRequest.Currency), "Currency must be a 3-letter ISO currency code."));
            return;
        }

        if (!SupportedCurrencies.Contains(currency))
        {
            errors.Add(new ValidationError(nameof(ProcessPaymentRequest.Currency), "Currency is not supported."));
        }
    }

    private static void ValidateAmount(int amount, ICollection<ValidationError> errors)
    {
        if (amount <= 0)
        {
            errors.Add(new ValidationError(nameof(ProcessPaymentRequest.Amount), "Amount must be a positive integer in minor units."));
        }
    }

    private static void ValidateCvv(string? cvv, ICollection<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(cvv))
        {
            errors.Add(new ValidationError(nameof(ProcessPaymentRequest.Cvv), "CVV is required."));
            return;
        }

        if (cvv.Length is < 3 or > 4)
        {
            errors.Add(new ValidationError(nameof(ProcessPaymentRequest.Cvv), "CVV must be 3 or 4 digits."));
        }

        if (!cvv.All(char.IsDigit))
        {
            errors.Add(new ValidationError(nameof(ProcessPaymentRequest.Cvv), "CVV must contain digits only."));
        }
    }
}
