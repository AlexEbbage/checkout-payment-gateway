using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using PaymentGateway.Api.Contracts.Payments;
using PaymentGateway.Api.Infrastructure.Banking;

namespace PaymentGateway.Api.Tests.Integration;

public sealed class PaymentsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PaymentsApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.BankClient.Reset();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostPayment_WhenBankAuthorizes_ReturnsCreatedWithAuthorizedPayment()
    {
        _factory.BankClient.Result = BankPaymentResult.Authorized("auth_123");

        var response = await _client.PostAsJsonAsync(
            "/api/payments",
            ValidRequest("4242424242424241"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<ProcessPaymentResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.Status.Should().Be("Authorized");
        body.AuthorizationCode.Should().Be("auth_123");
        body.LastFourCardDigits.Should().Be("4241");

        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task PostPayment_WhenBankDeclines_ReturnsCreatedWithDeclinedPayment()
    {
        _factory.BankClient.Result = BankPaymentResult.Declined();

        var response = await _client.PostAsJsonAsync(
            "/api/payments",
            ValidRequest("4242424242424242"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<ProcessPaymentResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.Status.Should().Be("Declined");
        body.AuthorizationCode.Should().BeNull();
    }

    [Fact]
    public async Task PostPayment_WhenRequestIsInvalid_ReturnsBadRequestAndDoesNotCallBank()
    {
        var invalidRequest = ValidRequest("not-a-card-number");

        var response = await _client.PostAsJsonAsync(
            "/api/payments",
            invalidRequest);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _factory.BankClient.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task PostPayment_WhenBankUnavailable_ReturnsServiceUnavailable()
    {
        _factory.BankClient.Result = BankPaymentResult.Unavailable();

        var response = await _client.PostAsJsonAsync(
            "/api/payments",
            ValidRequest("4242424242424240"));

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task PostPayment_WithSameIdempotencyKeyAndSamePayload_ReturnsSamePaymentIdAndDoesNotCallBankTwice()
    {
        _factory.BankClient.Result = BankPaymentResult.Authorized("auth_123");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments")
        {
            Content = JsonContent.Create(ValidRequest("4242424242424241"))
        };

        request.Headers.Add("Idempotency-Key", "integration-idempotency-key-1");

        var firstResponse = await _client.SendAsync(request);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var firstBody = await firstResponse.Content.ReadFromJsonAsync<ProcessPaymentResponse>(JsonOptions);

        var secondRequest = new HttpRequestMessage(HttpMethod.Post, "/api/payments")
        {
            Content = JsonContent.Create(ValidRequest("4242424242424241"))
        };

        secondRequest.Headers.Add("Idempotency-Key", "integration-idempotency-key-1");

        var secondResponse = await _client.SendAsync(secondRequest);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var secondBody = await secondResponse.Content.ReadFromJsonAsync<ProcessPaymentResponse>(JsonOptions);

        firstBody.Should().NotBeNull();
        secondBody.Should().NotBeNull();
        secondBody!.Id.Should().Be(firstBody!.Id);

        _factory.BankClient.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task PostPayment_WithSameIdempotencyKeyAndDifferentPayload_ReturnsConflict()
    {
        var firstRequest = new HttpRequestMessage(HttpMethod.Post, "/api/payments")
        {
            Content = JsonContent.Create(ValidRequest("4242424242424241") with { Amount = 1000 })
        };

        firstRequest.Headers.Add("Idempotency-Key", "integration-idempotency-key-2");

        var firstResponse = await _client.SendAsync(firstRequest);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var secondRequest = new HttpRequestMessage(HttpMethod.Post, "/api/payments")
        {
            Content = JsonContent.Create(ValidRequest("4242424242424241") with { Amount = 2000 })
        };

        secondRequest.Headers.Add("Idempotency-Key", "integration-idempotency-key-2");

        var secondResponse = await _client.SendAsync(secondRequest);

        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        _factory.BankClient.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetPayment_WhenPaymentExists_ReturnsStoredMaskedPayment()
    {
        _factory.BankClient.Result = BankPaymentResult.Authorized("auth_123");

        var postResponse = await _client.PostAsJsonAsync(
            "/api/payments",
            ValidRequest("4242424242424241"));

        postResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var postBody = await postResponse.Content.ReadFromJsonAsync<ProcessPaymentResponse>(JsonOptions);

        var getResponse = await _client.GetAsync($"/api/payments/{postBody!.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getBody = await getResponse.Content.ReadFromJsonAsync<GetPaymentResponse>(JsonOptions);

        getBody.Should().NotBeNull();
        getBody!.Id.Should().Be(postBody.Id);
        getBody.LastFourCardDigits.Should().Be("4241");
        getBody.Status.Should().Be("Authorized");
    }

    [Fact]
    public async Task GetPayment_WhenPaymentDoesNotExist_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/payments/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostPayment_ReturnsCorrelationIdHeader()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/payments",
            ValidRequest("4242424242424241"));

        response.Headers.TryGetValues("X-Correlation-Id", out var values).Should().BeTrue();
        values!.Single().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PostPayment_WhenCorrelationIdIsSupplied_ReturnsSameCorrelationId()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments")
        {
            Content = JsonContent.Create(ValidRequest("4242424242424241"))
        };

        request.Headers.Add("X-Correlation-Id", "integration-correlation-id-1");

        var response = await _client.SendAsync(request);

        response.Headers.TryGetValues("X-Correlation-Id", out var values).Should().BeTrue();
        values!.Single().Should().Be("integration-correlation-id-1");
    }

    [Fact]
    public async Task PostPayment_ResponseDoesNotContainFullCardNumberOrCvv()
    {
        var fullCardNumber = "4242424242424241";

        var response = await _client.PostAsJsonAsync(
            "/api/payments",
            ValidRequest(fullCardNumber) with { Cvv = "123" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<ProcessPaymentResponse>(JsonOptions);

        body.Should().NotBeNull();

        body!.LastFourCardDigits.Should().Be("4241");
        body.LastFourCardDigits.Should().NotBe(fullCardNumber);

        var rawJson = await response.Content.ReadAsStringAsync();

        rawJson.Should().NotContain(fullCardNumber);
        rawJson.Should().NotContain("\"cardNumber\"");
        rawJson.Should().NotContain("\"cvv\"");
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