using System.Text.Json;

using PaymentGateway.Api.Infrastructure.Banking;

namespace PaymentGateway.Api.Tests.Contract;

public sealed class BankContractTests
{
    [Fact]
    public void BankPaymentRequest_SerializesUsingExpectedBankFieldNames()
    {
        var request = new BankPaymentRequest(
            "4242424242424241",
            "12/2030",
            "GBP",
            1050,
            "123");

        var json = JsonSerializer.Serialize(request);

        json.Should().Contain("\"card_number\"");
        json.Should().Contain("\"expiry_date\"");
        json.Should().Contain("\"currency\"");
        json.Should().Contain("\"amount\"");
        json.Should().Contain("\"cvv\"");

        json.Should().NotContain("\"cardNumber\"");
        json.Should().NotContain("\"expiryDate\"");
    }

    [Fact]
    public void BankPaymentResponse_DeserializesUsingExpectedBankFieldNames()
    {
        var json = """
        {
          "authorized": true,
          "authorization_code": "auth_123"
        }
        """;

        var response = JsonSerializer.Deserialize<BankPaymentResponse>(json);

        response.Should().NotBeNull();
        response!.Authorized.Should().BeTrue();
        response.AuthorizationCode.Should().Be("auth_123");
    }
}