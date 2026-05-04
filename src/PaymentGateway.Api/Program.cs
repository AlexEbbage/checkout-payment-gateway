using Microsoft.Extensions.Options;
using PaymentGateway.Api.Application.Idempotency;
using PaymentGateway.Api.Application.Payments;
using PaymentGateway.Api.Application.Time;
using PaymentGateway.Api.Infrastructure.Banking;
using PaymentGateway.Api.Infrastructure.Persistence;
using PaymentGateway.Api.Middleware;
using PaymentGateway.Api.Observability;
using PaymentGateway.Api.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<BankSimulatorOptions>(
    builder.Configuration.GetSection(BankSimulatorOptions.SectionName));

builder.Services.AddSingleton<IClock, SystemClock>();

builder.Services.AddSingleton<IPaymentRepository, InMemoryPaymentRepository>();
builder.Services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();

builder.Services.AddSingleton<PaymentRequestValidator>();
builder.Services.AddSingleton<PaymentMetrics>();
builder.Services.AddScoped<IPaymentService, PaymentService>();

builder.Services.AddHttpClient<IAcquiringBankClient, AcquiringBankClient>((serviceProvider, httpClient) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<BankSimulatorOptions>>().Value;

    if (string.IsNullOrWhiteSpace(options.BaseUrl))
    {
        throw new InvalidOperationException("BankSimulator:BaseUrl configuration is required.");
    }

    httpClient.BaseAddress = new Uri(options.BaseUrl);
    httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();

public partial class Program;
