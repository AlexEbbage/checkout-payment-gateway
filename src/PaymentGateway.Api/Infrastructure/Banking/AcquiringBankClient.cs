using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using PaymentGateway.Api.Contracts.Payments;
using PaymentGateway.Api.Observability;

namespace PaymentGateway.Api.Infrastructure.Banking;

public sealed class AcquiringBankClient : IAcquiringBankClient
{
    private readonly HttpClient _httpClient;
    private readonly PaymentMetrics _metrics;
    private readonly ILogger<AcquiringBankClient> _logger;

    public AcquiringBankClient(
        HttpClient httpClient,
        PaymentMetrics metrics,
        ILogger<AcquiringBankClient> logger)
    {
        _httpClient = httpClient;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<BankPaymentResult> ProcessPaymentAsync(
        ProcessPaymentRequest request,
        CancellationToken cancellationToken)
    {
        using var activity = PaymentTracing.ActivitySource.StartActivity(
            "AcquiringBankClient.ProcessPayment",
            ActivityKind.Client);

        var stopwatch = Stopwatch.StartNew();

        var result = BankPaymentResult.Unavailable();

        try
        {
            var bankRequest = new BankPaymentRequest(
                request.CardNumber!,
                FormatExpiryDate(request.ExpiryMonth, request.ExpiryYear),
                request.Currency!,
                request.Amount,
                request.Cvv!);

            using var response = await _httpClient.PostAsJsonAsync(
                "/payments",
                bankRequest,
                cancellationToken);

            activity?.SetTag("http.status_code", (int)response.StatusCode);

            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                result = BankPaymentResult.Unavailable();
                return result;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Acquiring bank returned unexpected status code {StatusCode}",
                    response.StatusCode);

                result = BankPaymentResult.Unavailable();
                return result;
            }

            var bankResponse = await response.Content.ReadFromJsonAsync<BankPaymentResponse>(
                cancellationToken: cancellationToken);

            if (bankResponse is null)
            {
                _logger.LogWarning("Acquiring bank returned an empty response body");
                result = BankPaymentResult.Unavailable();
                return result;
            }

            if (!bankResponse.Authorized)
            {
                result = BankPaymentResult.Declined();
                return result;
            }

            if (string.IsNullOrWhiteSpace(bankResponse.AuthorizationCode))
            {
                _logger.LogWarning("Acquiring bank authorized payment without an authorization code");
                result = BankPaymentResult.Unavailable();
                return result;
            }

            result = BankPaymentResult.Authorized(bankResponse.AuthorizationCode);
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Acquiring bank request timed out");
            result = BankPaymentResult.Unavailable();
            return result;
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "Acquiring bank request failed");

            result = BankPaymentResult.Unavailable();
            return result;
        }
        finally
        {
            stopwatch.Stop();

            _metrics.RecordBankRequestDuration(
                stopwatch.Elapsed.TotalMilliseconds,
                result.Status);

            activity?.SetTag("bank.payment_status", result.Status.ToString());
        }
    }

    private static string FormatExpiryDate(int expiryMonth, int expiryYear)
    {
        return $"{expiryMonth:D2}/{expiryYear}";
    }
}
