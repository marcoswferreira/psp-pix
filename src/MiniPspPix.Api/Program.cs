using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniPspPix.Api.Common;
using MiniPspPix.Api.Middlewares;
using MiniPspPix.Api.Observability;
using MiniPspPix.Application.Charges;
using MiniPspPix.Application.Idempotency;
using MiniPspPix.Application.Ledger;
using MiniPspPix.Application.Payments;
using MiniPspPix.Application.Webhooks;
using MiniPspPix.Domain.Banking;
using MiniPspPix.Domain.Charges;
using MiniPspPix.Domain.Ledger;
using MiniPspPix.Domain.Webhooks;
using MiniPspPix.Infrastructure.BackgroundWorkers;
using MiniPspPix.Infrastructure.Banking;
using MiniPspPix.Infrastructure.Ledger;
using MiniPspPix.Infrastructure.Messaging;
using MiniPspPix.Infrastructure.Persistence;
using MiniPspPix.Infrastructure.Webhooks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Configuração do Banco de Dados
var postgresConn = builder.Configuration.GetConnectionString("PostgresConnection");
if (!string.IsNullOrWhiteSpace(postgresConn))
{
    builder.Services.AddDbContext<PixDbContext>(opt => opt.UseNpgsql(postgresConn));
}
else
{
    builder.Services.AddDbContext<PixDbContext>(opt => opt.UseInMemoryDatabase("MiniPspPixDb"));
}

// Repositórios e Serviços de Domínio/Aplicação
builder.Services.AddScoped<IChargeRepository, EfCoreChargeRepository>();
builder.Services.AddScoped<IOutboxRepository, EfCoreOutboxRepository>();
builder.Services.AddScoped<IWebhookRepository, EfCoreWebhookRepository>();
builder.Services.AddScoped<IIdempotencyStore, EfCoreIdempotencyStore>();
builder.Services.AddScoped<ILedgerService, EfCoreLedgerService>();

// Provedor Bancário Simulado (com suporte a injeção de caos)
builder.Services.AddSingleton<IBankingProvider, SimulatedBankingProvider>();

// Webhooks HTTP Client
builder.Services.AddHttpClient<IWebhookSenderClient, HttpWebhookSenderClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddScoped<DispatchWebhookUseCase>();

// Mensageria e Event Publisher
var rabbitConn = builder.Configuration.GetConnectionString("RabbitMqConnection");
if (!string.IsNullOrWhiteSpace(rabbitConn))
{
    builder.Services.AddSingleton<IEventPublisher>(sp =>
        new RabbitMqEventPublisher(rabbitConn, sp.GetRequiredService<ILogger<RabbitMqEventPublisher>>()));
}
else
{
    builder.Services.AddSingleton<IEventPublisher, InMemoryEventPublisher>();
}

// Background Workers
builder.Services.AddHostedService<OutboxPublisherWorker>();
builder.Services.AddHostedService<WebhookDeliveryWorker>();

// Casos de Uso
builder.Services.AddScoped<CreateChargeUseCase>();
builder.Services.AddScoped(sp => new ProcessInboundPaymentUseCase(
    sp.GetRequiredService<IChargeRepository>(),
    sp.GetRequiredService<ILedgerService>(),
    sp.GetRequiredService<IOutboxRepository>(),
    sp.GetRequiredService<IBankingProvider>(),
    DefaultAccounts.PixTransitAccountId,
    DefaultAccounts.MerchantAccountId
));
builder.Services.AddScoped(sp => new RequestPayoutUseCase(
    sp.GetRequiredService<ILedgerService>(),
    sp.GetRequiredService<IBankingProvider>(),
    sp.GetRequiredService<IOutboxRepository>(),
    DefaultAccounts.PixTransitAccountId
));

// OpenTelemetry (Métricas e Traces)
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter(PixMetrics.MeterName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddPrometheusExporter();

        var otelEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        if (!string.IsNullOrWhiteSpace(otelEndpoint))
        {
            metrics.AddOtlpExporter(opt => opt.Endpoint = new Uri(otelEndpoint));
        }
    })
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(PixMetrics.MeterName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();

        var otelEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        if (!string.IsNullOrWhiteSpace(otelEndpoint))
        {
            tracing.AddOtlpExporter(opt => opt.Endpoint = new Uri(otelEndpoint));
        }
    });

// OpenAPI nativo do .NET 10
builder.Services.AddOpenApi();

var app = builder.Build();

// Seeder de contas iniciais para testes e demonstrações
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PixDbContext>();
    if (!string.IsNullOrWhiteSpace(postgresConn))
    {
        db.Database.EnsureCreated();
    }
    if (!db.LedgerAccounts.Any())
    {
        db.LedgerAccounts.AddRange(
            new LedgerAccount(DefaultAccounts.PixTransitAccountId, "TRANSIT-BACEN-001", AccountType.PixSettlementTransit, 0),
            new LedgerAccount(DefaultAccounts.MerchantAccountId, "MERCHANT-001", AccountType.Merchant, 1000.00m)
        );
        db.SaveChanges();
    }
}

// Pipeline de Middlewares
app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<IdempotencyMiddleware>();

// OpenAPI endpoint nativo (/openapi/v1.json)
app.MapOpenApi();

// Exporter de métricas do Prometheus
app.UseOpenTelemetryPrometheusScrapingEndpoint();

// ==========================================
// ENDPOINTS
// ==========================================

// Health Check
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }));

// 1. Cobrança Imediata Pix (Criação de Pix Cob com BR Code)
app.MapPost("/v1/pix/cob", async (
    [FromBody] CreateChargeCommand command,
    CreateChargeUseCase useCase,
    CancellationToken ct) =>
{
    var result = await useCase.ExecuteAsync(command, ct);
    PixMetrics.ChargesCreatedCounter.Add(1);
    return Results.Created($"/v1/pix/cob/{result.TxId}", result);
})
.WithName("CreatePixCharge")
.WithSummary("Cria uma cobrança imediata Pix com geração de BR Code EMV e CRC16");

// Consulta de Cobrança Pix
app.MapGet("/v1/pix/cob/{txid}", async (
    string txid,
    IChargeRepository repo,
    CancellationToken ct) =>
{
    var charge = await repo.GetByTxIdAsync(txid, ct);
    return charge != null
        ? Results.Ok(CreateChargeUseCase.ToDto(charge))
        : Results.NotFound(new { detail = $"Cobrança com TxId '{txid}' não encontrada." });
})
.WithName("GetPixCharge")
.WithSummary("Consulta o estado de uma cobrança Pix");

// 2. Simulação de Pagamento Bancário Recebido (Inbound Notification)
app.MapPost("/v1/pix/simulation/inbound-payment", async (
    [FromBody] ProcessInboundPaymentCommand command,
    ProcessInboundPaymentUseCase useCase,
    CancellationToken ct) =>
{
    var result = await useCase.ExecuteAsync(command, ct);
    PixMetrics.PaymentsReceivedCounter.Add(1);
    return Results.Ok(result);
})
.WithName("SimulateInboundPayment")
.WithSummary("Simula notificação bancária de liquidação Pix com suporte a injeção de caos");

// 3. Payout Pix (Transferência / Saque com idempotência e ledger)
app.MapPost("/v1/pix/payout", async (
    [FromBody] RequestPayoutCommand command,
    RequestPayoutUseCase useCase,
    CancellationToken ct) =>
{
    var result = await useCase.ExecuteAsync(command, ct);
    PixMetrics.PayoutsProcessedCounter.Add(1);
    return Results.Ok(result);
})
.WithName("RequestPayout")
.WithSummary("Realiza um saque/payout Pix debitando o lojista no Ledger com concorrência otimista");

// 4. Ledger: Consulta de Saldo e Extrato
app.MapGet("/v1/ledger/accounts/{accountId:guid}/balance", async (
    Guid accountId,
    ILedgerService ledgerService,
    CancellationToken ct) =>
{
    var account = await ledgerService.GetAccountAsync(accountId, ct);
    return account != null
        ? Results.Ok(new { account.Id, account.AccountNumber, account.Type, account.Balance, account.Currency, account.Version })
        : Results.NotFound();
})
.WithName("GetAccountBalance");

app.MapGet("/v1/ledger/accounts/{accountId:guid}/statement", async (
    Guid accountId,
    ILedgerService ledgerService,
    CancellationToken ct) =>
{
    var statement = await ledgerService.GetStatementAsync(accountId, ct);
    return Results.Ok(statement);
})
.WithName("GetAccountStatement");

// 5. Registro de Webhook pelo Merchant
app.MapPost("/v1/merchants/webhooks", async (
    [FromBody] RegisterWebhookCommand command,
    IWebhookRepository repo,
    CancellationToken ct) =>
{
    var sub = new WebhookSubscription(command.MerchantId, command.TargetUrl, command.SecretKey);
    await repo.AddSubscriptionAsync(sub, ct);
    return Results.Created($"/v1/merchants/webhooks/{sub.Id}", new
    {
        sub.Id,
        sub.MerchantId,
        sub.TargetUrl,
        sub.SecretKey,
        sub.IsActive,
        sub.CreatedAtUtc
    });
})
.WithName("RegisterWebhook");

app.Run();

// Necessário para permitir referenciar Program a partir de WebApplicationFactory nos testes de integração
public partial class Program { }
