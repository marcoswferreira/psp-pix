namespace Domain.Interfaces.Services;

public interface IBankingProvider
{
    Task<BankChargeResult> RegisterChargeAsync(BankChargeRequest request, CancellationToken ct = default);
    Task<BankPayoutResult> SendPayoutAsync(BankPayoutRequest request, CancellationToken ct = default);
    Task<SimulatedInboundPaymentResult> SimulateInboundPaymentAsync(SimulatedInboundPaymentRequest request, CancellationToken ct = default);
}

