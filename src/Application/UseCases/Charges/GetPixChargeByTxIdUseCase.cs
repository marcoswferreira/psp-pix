using Application.UseCases.Charges.Dto;
using Domain.Interfaces.Repositories;

namespace Application.UseCases.Charges;

public class GetPixChargeByTxIdUseCase(IChargeRepository chargeRepository)
{
    private readonly IChargeRepository _chargeRepository = chargeRepository;

    public async Task<PixChargeOutput?> ExecuteAsync(string txId, CancellationToken ct = default)
    {
        var charge = await _chargeRepository.GetByTxIdAsync(txId, ct);
        return charge != null ? CreatePixChargeUseCase.ToOutput(charge) : null;
    }
}

