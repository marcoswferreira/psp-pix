using System.Diagnostics.Metrics;

namespace MiniPspPix.Api.Observability;

public static class PixMetrics
{
    public const string MeterName = "MiniPspPix";
    private static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly Counter<long> ChargesCreatedCounter =
        Meter.CreateCounter<long>("pix_charges_created_total", description: "Total de cobranças Pix emitidas");

    public static readonly Counter<long> PaymentsReceivedCounter =
        Meter.CreateCounter<long>("pix_payments_received_total", description: "Total de liquidações Pix recebidas");

    public static readonly Counter<long> PayoutsProcessedCounter =
        Meter.CreateCounter<long>("pix_payouts_processed_total", description: "Total de saques Pix processados");

    public static readonly Counter<long> IdempotencyHitsCounter =
        Meter.CreateCounter<long>("pix_idempotency_hits_total", description: "Total de replays idempotentes servidos do cache");

    public static readonly Counter<long> WebhookRetriesCounter =
        Meter.CreateCounter<long>("pix_webhook_retries_total", description: "Total de retentativas de webhook");

    public static readonly Counter<long> WebhookDeadLettersCounter =
        Meter.CreateCounter<long>("pix_webhook_dead_letters_total", description: "Total de webhooks encaminhados para a Dead Letter Queue (DLQ)");
}

