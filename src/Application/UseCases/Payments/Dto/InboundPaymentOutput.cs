namespace Application.UseCases.Payments.Dto;

public record InboundPaymentOutput(
    bool Success,
    string TxId,
    string EndToEndId,
    decimal Amount,
    DateTime PaidAtUtc,
    string Message
);

