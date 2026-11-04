using Application.UseCases.Charges;
using Application.UseCases.Ledger;
using Application.UseCases.Payments;
using Application.UseCases.Payout;
using Application.UseCases.Webhooks;
using Domain.Interfaces.Repositories;
using Domain.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Extensions;

public static class ApplicationExtensions
{
    public static readonly Guid DefaultPixTransitAccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid DefaultMerchantAccountId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        return services.AddApplicationServices(DefaultPixTransitAccountId, DefaultMerchantAccountId);
    }

    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        Guid pixTransitAccountId,
        Guid merchantAccountId)
    {
        // Charges Use Cases
        services.AddScoped<CreatePixChargeUseCase>();
        services.AddScoped<GetPixChargeByTxIdUseCase>();

        // Payments & Payout Use Cases
        services.AddScoped(sp => new ProcessInboundPaymentUseCase(
            sp.GetRequiredService<IChargeRepository>(),
            sp.GetRequiredService<ILedgerRepository>(),
            sp.GetRequiredService<IOutboxRepository>(),
            sp.GetRequiredService<IBankingProvider>(),
            pixTransitAccountId,
            merchantAccountId
        ));

        services.AddScoped(sp => new RequestPayoutUseCase(
            sp.GetRequiredService<ILedgerRepository>(),
            sp.GetRequiredService<IBankingProvider>(),
            sp.GetRequiredService<IOutboxRepository>(),
            pixTransitAccountId
        ));

        // Ledger Use Cases
        services.AddScoped<GetAccountBalanceUseCase>();
        services.AddScoped<GetAccountStatementUseCase>();

        // Webhook Use Cases
        services.AddScoped<RegisterWebhookUseCase>();
        services.AddScoped<DispatchWebhookUseCase>();

        return services;
    }
}
