using Domain.Enums;
using Domain.Interfaces.Services;
using Domain.ValueObjects;

namespace Infrastructure.Services;

public class SimulatedBankingProvider : IBankingProvider
{
    public async Task<BankChargeResult> RegisterChargeAsync(BankChargeRequest request, CancellationToken ct = default)
    {
        await ApplyChaosAsync(request.Chaos, ct);

        if (request.Chaos?.Action == ChaosAction.Fail)
        {
            return new BankChargeResult(
                Success: false,
                BankTransactionId: null,
                ErrorCode: request.Chaos.BankErrorCode ?? "CH01",
                ErrorMessage: request.Chaos.FailureReason ?? "Rejeição bancária simulada na emissão da cobrança."
            );
        }

        return new BankChargeResult(
            Success: true,
            BankTransactionId: $"BANK-TX-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
            ErrorCode: null,
            ErrorMessage: null
        );
    }

    public async Task<BankPayoutResult> SendPayoutAsync(BankPayoutRequest request, CancellationToken ct = default)
    {
        await ApplyChaosAsync(request.Chaos, ct);

        if (request.Chaos?.Action == ChaosAction.Fail)
        {
            return new BankPayoutResult(
                Success: false,
                EndToEndId: null,
                BankTransactionId: null,
                ErrorCode: request.Chaos.BankErrorCode ?? "AB03",
                ErrorMessage: request.Chaos.FailureReason ?? "Chave Pix de destino inexistente ou conta encerrada.",
                SettledAtUtc: DateTime.UtcNow
            );
        }

        var endToEndId = EndToEndId.Generate("12345678").Value;
        var bankTxId = $"PAYOUT-SPI-{Guid.NewGuid():N}"[..24].ToUpperInvariant();

        return new BankPayoutResult(
            Success: true,
            EndToEndId: endToEndId,
            BankTransactionId: bankTxId,
            ErrorCode: null,
            ErrorMessage: null,
            SettledAtUtc: DateTime.UtcNow
        );
    }

    public async Task<SimulatedInboundPaymentResult> SimulateInboundPaymentAsync(SimulatedInboundPaymentRequest request, CancellationToken ct = default)
    {
        await ApplyChaosAsync(request.Chaos, ct);

        if (request.Chaos?.Action == ChaosAction.Fail)
        {
            return new SimulatedInboundPaymentResult(
                Success: false,
                EndToEndId: string.Empty,
                BankTransactionId: null,
                WasDuplicated: false,
                ErrorMessage: request.Chaos.FailureReason ?? "Falha simulada na liquidação."
            );
        }

        var endToEndId = EndToEndId.Generate("98765432").Value;
        var wasDuplicated = request.Chaos?.Action == ChaosAction.Duplicate;

        return new SimulatedInboundPaymentResult(
            Success: true,
            EndToEndId: endToEndId,
            BankTransactionId: $"INBOUND-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
            WasDuplicated: wasDuplicated,
            ErrorMessage: null
        );
    }

    private static async Task ApplyChaosAsync(ChaosOptions? chaos, CancellationToken ct)
    {
        if (chaos == null) return;

        if (chaos.Action == ChaosAction.Delay && chaos.DelayMilliseconds > 0)
        {
            await Task.Delay(chaos.DelayMilliseconds, ct);
        }
        else if (chaos.Action == ChaosAction.Timeout)
        {
            if (chaos.DelayMilliseconds > 0)
            {
                await Task.Delay(chaos.DelayMilliseconds, ct);
            }
            throw new TimeoutException("Timeout bancário simulado: a câmara SPI não respondeu dentro do SLA de 1000ms.");
        }
    }
}

