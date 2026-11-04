using System.Text.Json;
using Domain.Common.Exceptions;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Repositories;
using Domain.Interfaces.Services;
using Domain.ValueObjects;

namespace Application.UseCases.Payments.Dto;

public record ProcessInboundPaymentInput(
    string TxId,
    decimal Amount,
    string? EndToEndId = null,
    ChaosOptions? Chaos = null
);

public record InboundPaymentOutput(
    bool Success,
    string TxId,
    string EndToEndId,
    decimal Amount,
    DateTime PaidAtUtc,
    string Message
);

