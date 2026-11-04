using System.Diagnostics.Metrics;

namespace MiniPspPix.API.Observability;

public static class PixMetrics
{
    public const string MeterName = "MiniPspPix";
    private static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly Counter<long> ChargesCreated = Meter.CreateCounter<long>(
        "pix_charges_created_total",
        description: "Total de cobranças Pix criadas");

    public static readonly Counter<long> PaymentsProcessed = Meter.CreateCounter<long>(
        "pix_payments_processed_total",
        description: "Total de pagamentos Pix inbound processados");

    public static readonly Counter<long> PayoutsRequested = Meter.CreateCounter<long>(
        "pix_payouts_requested_total",
        description: "Total de payouts Pix requisitados");

    public static readonly Counter<long> IdempotentReplays = Meter.CreateCounter<long>(
        "pix_idempotent_replays_total",
        description: "Total de requisições idempotentes repetidas (cached replay)");

    public static readonly Counter<long> IdempotentConflicts = Meter.CreateCounter<long>(
        "pix_idempotent_conflicts_total",
        description: "Total de conflitos de chave de idempotência");

    public static readonly Counter<long> DoubleSpendingAttempts = Meter.CreateCounter<long>(
        "pix_double_spending_attempts_total",
        description: "Total de tentativas de gasto duplo detectadas");

    public static readonly Counter<long> WebhooksDispatched = Meter.CreateCounter<long>(
        "pix_webhooks_dispatched_total",
        description: "Total de webhooks despachados");
}
