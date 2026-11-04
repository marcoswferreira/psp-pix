using System.Text.Json;
using Domain.Common.Exceptions;
using Domain.Entities;
using Domain.Interfaces.Repositories;
using Domain.Interfaces.Services;

namespace Application.UseCases.Payout.Dto;

public record RequestPayoutInput(
    Guid MerchantAccountId,
    decimal Amount,
    string DestinationPixKey,
    string Description,
    ChaosOptions? Chaos = null
);

public record PayoutOutput(
    bool Success,
    Guid PayoutId,
    string? EndToEndId,
    decimal Amount,
    decimal RemainingBalance,
    string Message
);
