using System.Text.Json;
using Domain.Common.Exceptions;
using Domain.Entities;
using Domain.Interfaces.Repositories;
using Domain.Interfaces.Services;
using Domain.ValueObjects;

namespace Application.UseCases.Charges.Dto;

public record CreatePixChargeInput(
    decimal Amount,
    string PixKey,
    string Description,
    string MerchantName = "PSP MERCHANT",
    string MerchantCity = "BRASILIA",
    string? TxId = null,
    TimeSpan? ExpiresIn = null
);

public record PixChargeOutput(
    Guid Id,
    string TxId,
    decimal Amount,
    string Status,
    string PixKey,
    string Description,
    string BrCode,
    string? QrCodeSvg,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    DateTime? PaidAtUtc,
    string? EndToEndId
);
