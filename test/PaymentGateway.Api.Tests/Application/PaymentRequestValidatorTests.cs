using PaymentGateway.Api.Application.Payments;
using PaymentGateway.Api.Contracts.Payments;
using PaymentGateway.Api.Tests.TestDoubles;

namespace PaymentGateway.Api.Tests.Application;

public sealed class PaymentRequestValidatorTests
{
    private readonly FakeClock _clock = new()
    {
        UtcNow = new DateTimeOffset(2026, 04, 30, 12, 0, 0, TimeSpan.Zero)
    };

    [Fact]
    public void Validate_WhenRequestIsValid_ReturnsNoErrors()
    {
        var validator = new PaymentRequestValidator(_clock);

        var request = ValidRequest();

        var result = validator.Validate(request);

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_WhenCardNumberIsMissing_ReturnsError(string? cardNumber)
    {
        var validator = new PaymentRequestValidator(_clock);

        var request = ValidRequest() with { CardNumber = cardNumber };

        var result = validator.Validate(request);

        result.Should().Contain(error => error.Field == nameof(ProcessPaymentRequest.CardNumber));
    }

    [Fact]
    public void Validate_WhenCardNumberContainsNonDigits_ReturnsError()
    {
        var validator = new PaymentRequestValidator(_clock);

        var request = ValidRequest() with { CardNumber = "42424242424242ab" };

        var result = validator.Validate(request);

        result.Should().Contain(error => error.Field == nameof(ProcessPaymentRequest.CardNumber));
    }

    [Theory]
    [InlineData("1234567890123")]
    [InlineData("12345678901234567890")]
    public void Validate_WhenCardNumberLengthIsInvalid_ReturnsError(string cardNumber)
    {
        var validator = new PaymentRequestValidator(_clock);

        var request = ValidRequest() with { CardNumber = cardNumber };

        var result = validator.Validate(request);

        result.Should().Contain(error => error.Field == nameof(ProcessPaymentRequest.CardNumber));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Validate_WhenExpiryMonthIsInvalid_ReturnsError(int expiryMonth)
    {
        var validator = new PaymentRequestValidator(_clock);

        var request = ValidRequest() with { ExpiryMonth = expiryMonth };

        var result = validator.Validate(request);

        result.Should().Contain(error => error.Field == nameof(ProcessPaymentRequest.ExpiryMonth));
    }

    [Fact]
    public void Validate_WhenCardIsExpired_ReturnsError()
    {
        var validator = new PaymentRequestValidator(_clock);

        var request = ValidRequest() with
        {
            ExpiryMonth = 3,
            ExpiryYear = 2026
        };

        var result = validator.Validate(request);

        result.Should().Contain(error => error.Field == nameof(ProcessPaymentRequest.ExpiryYear));
    }

    [Fact]
    public void Validate_WhenCurrencyIsUnsupported_ReturnsError()
    {
        var validator = new PaymentRequestValidator(_clock);

        var request = ValidRequest() with { Currency = "JPY" };

        var result = validator.Validate(request);

        result.Should().Contain(error => error.Field == nameof(ProcessPaymentRequest.Currency));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WhenAmountIsNotPositive_ReturnsError(int amount)
    {
        var validator = new PaymentRequestValidator(_clock);

        var request = ValidRequest() with { Amount = amount };

        var result = validator.Validate(request);

        result.Should().Contain(error => error.Field == nameof(ProcessPaymentRequest.Amount));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12")]
    [InlineData("12345")]
    [InlineData("12a")]
    public void Validate_WhenCvvIsInvalid_ReturnsError(string? cvv)
    {
        var validator = new PaymentRequestValidator(_clock);

        var request = ValidRequest() with { Cvv = cvv };

        var result = validator.Validate(request);

        result.Should().Contain(error => error.Field == nameof(ProcessPaymentRequest.Cvv));
    }

    private static ProcessPaymentRequest ValidRequest()
    {
        return new ProcessPaymentRequest(
            "4242424242424241",
            12,
            2030,
            "GBP",
            1000,
            "123");
    }
}
