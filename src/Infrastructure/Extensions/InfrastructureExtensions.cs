using Domain.Interfaces.Repositories;
using Domain.Interfaces.Services;
using Infrastructure.BackgroundWorkers;
using Infrastructure.Database.DbContexts;
using Infrastructure.Database.Repositories;
using Infrastructure.Database.Seeders;
using Infrastructure.Messaging;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var dbProvider = configuration["Database:Provider"] ?? "InMemory";
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
                               ?? configuration["ConnectionStrings:DefaultConnection"];

        if (string.Equals(dbProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString, b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));
        }
        else
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase("MiniPspPixDb"));
        }

        // Repositories
        services.AddScoped<IChargeRepository, ChargeRepository>();
        services.AddScoped<ILedgerRepository, LedgerRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IWebhookRepository, WebhookRepository>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();

        // Services
        services.AddSingleton<IBankingProvider, SimulatedBankingProvider>();
        services.AddSingleton<IBrCodeService, BrCodeService>();
        services.AddSingleton<IHmacSignerService, HmacSignerService>();
        services.AddHttpClient<IWebhookSenderClient, HttpWebhookSenderClient>();

        // Messaging
        var useRabbitMq = configuration.GetValue<bool>("RabbitMQ:Enabled", false);
        if (useRabbitMq)
        {
            services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
        }
        else
        {
            services.AddSingleton<InMemoryEventPublisher>();
            services.AddSingleton<IEventPublisher>(sp => sp.GetRequiredService<InMemoryEventPublisher>());
        }

        // Background Workers
        var enableWorkers = configuration.GetValue<bool>("Workers:Enabled", true);
        if (enableWorkers)
        {
            services.AddHostedService<OutboxPublisherWorker>();
            services.AddHostedService<WebhookDeliveryWorker>();
        }

        return services;
    }

    public static async Task MigrateDatabaseAsync(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<ApplicationDbContext>>();
        var context = services.GetRequiredService<ApplicationDbContext>();

        try
        {
            if (context.Database.IsRelational())
            {
                logger.LogInformation("Applying relational database migrations...");
                await context.Database.MigrateAsync();
            }
            else
            {
                logger.LogInformation("Ensuring InMemory database is created...");
                await context.Database.EnsureCreatedAsync();
            }

            await DatabaseSeeder.SeedAsync(context, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while migrating or seeding the database.");
            throw;
        }
    }
}

