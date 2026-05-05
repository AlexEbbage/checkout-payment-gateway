using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using PaymentGateway.Api.Contracts.Payments;
using PaymentGateway.Api.Infrastructure.Banking;

namespace PaymentGateway.Api.Tests.Integration;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public ControllableAcquiringBankClient BankClient { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAcquiringBankClient>();
            services.AddSingleton<IAcquiringBankClient>(BankClient);
        });
    }
}

public sealed class ControllableAcquiringBankClient : IAcquiringBankClient
{
    private readonly object _lock = new();

    public int CallCount { get; private set; }

    public BankPaymentResult Result { get; set; } = BankPaymentResult.Authorized("auth_123");

    public Task<BankPaymentResult> ProcessPaymentAsync(
        ProcessPaymentRequest request,
        CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            CallCount++;
        }

        return Task.FromResult(Result);
    }

    public void Reset()
    {
        lock (_lock)
        {
            CallCount = 0;
            Result = BankPaymentResult.Authorized("auth_123");
        }
    }
}