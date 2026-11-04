using Application.Extensions;
using Infrastructure.Extensions;
using MiniPspPix.API;
using MiniPspPix.API.Extensions;
using MiniPspPix.API.Middlewares;
using MiniPspPix.API.Observability;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

// Add Services matching 18SOAT clean structure
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();

builder.Services.AddControllers();
builder.Services.AddApiVersioningExtensions();
builder.Services.AddSwaggerExtensions();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ExceptionHandler>();

// OpenTelemetry Metrics & Prometheus Scraping
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter(PixMetrics.MeterName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddPrometheusExporter();
    });

var app = builder.Build();

// Migrate/Seed Database
await app.MigrateDatabaseAsync();

app.UseExceptionHandler();
app.UseMiddleware<IdempotencyMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseOpenTelemetryPrometheusScrapingEndpoint();
app.MapControllers();

app.Run();

// Required for WebApplicationFactory in IntegrationTests
public partial class Program { }
