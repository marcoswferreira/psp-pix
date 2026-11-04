using Domain.Interfaces.Services;

namespace Application.UseCases.Payments.Dto;

public record ProcessInboundPaymentInput(
    string TxId,
    decimal Amount,
    string? EndToEndId = null,
    ChaosOptions? Chaos = null
);

