using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using PaymentGateway.Api.Contracts.Payments;

namespace PaymentGateway.Api.Tests.E2E;

public sealed class PaymentGatewaySmokeTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    [Trait("Category", "E2E")]
    public async Task Smoke_AuthorizedPayment_CanBeProcessedAndRetrieved()
    {
        var baseUrl = GetE2EBaseUrl();

        if (baseUrl is null)
        {
            return;
        }

        using var client = CreateClient(baseUrl);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments")
        {
            Content = JsonContent.Create(ValidRequest("4242424242424241"))
        };

        request.Headers.Add("Idempotency-Key", $"e2e-auth-{Guid.NewGuid():N}");

        var postResponse = await client.SendAsync(request);

        postResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var postBody = await postResponse.Content.ReadFromJsonAsync<ProcessPaymentResponse>(JsonOptions);

        postBody.Should().NotBeNull();
        postBody!.Status.Should().Be("Authorized");
        postBody.LastFourCardDigits.Should().Be("4241");

        var getResponse = await client.GetAsync($"/api/payments/{postBody.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getBody = await getResponse.Content.ReadFromJsonAsync<GetPaymentResponse>(JsonOptions);

        getBody.Should().NotBeNull();
        getBody!.Id.Should().Be(postBody.Id);
        getBody.LastFourCardDigits.Should().Be("4241");
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task Smoke_DeclinedPayment_ReturnsDeclined()
    {
        var baseUrl = GetE2EBaseUrl();

        if (baseUrl is null)
        {
            return;
        }

        using var client = CreateClient(baseUrl);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments")
        {
            Content = JsonContent.Create(ValidRequest("4242424242424242"))
        };

        request.Headers.Add("Idempotency-Key", $"e2e-decline-{Guid.NewGuid():N}");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<ProcessPaymentResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.Status.Should().Be("Declined");
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task Smoke_BankUnavailable_ReturnsServiceUnavailable()
    {
        var baseUrl = GetE2EBaseUrl();

        if (baseUrl is null)
        {
            return;
        }

        using var client = CreateClient(baseUrl);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments")
        {
            Content = JsonContent.Create(ValidRequest("4242424242424240"))
        };

        request.Headers.Add("Idempotency-Key", $"e2e-unavailable-{Guid.NewGuid():N}");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    private static string? GetE2EBaseUrl()
    {
        return Environment.GetEnvironmentVariable("PAYMENT_GATEWAY_E2E_BASE_URL");
    }

    private static HttpClient CreateClient(string baseUrl)
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };

        return new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl)
        };
    }

    private static ProcessPaymentRequest ValidRequest(string cardNumber)
    {
        return new ProcessPaymentRequest(
            cardNumber,
            12,
            2030,
            "GBP",
            1050,
            "123");
    }
}