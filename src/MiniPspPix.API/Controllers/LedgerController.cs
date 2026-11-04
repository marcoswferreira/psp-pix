using Application.Dto;
using Application.UseCases.Ledger;
using Application.UseCases.Ledger.Dto;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace MiniPspPix.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class LedgerController(
    GetAccountBalanceUseCase getBalanceUseCase,
    GetAccountStatementUseCase getStatementUseCase) : ControllerBase
{
    private readonly GetAccountBalanceUseCase _getBalanceUseCase = getBalanceUseCase;
    private readonly GetAccountStatementUseCase _getStatementUseCase = getStatementUseCase;

    [HttpGet("accounts/{id:guid}/balance")]
    [ProducesResponseType(typeof(ResultGeneric<AccountBalanceOutput>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResultGeneric<AccountBalanceOutput>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBalance([FromRoute] Guid id, CancellationToken ct)
    {
        var output = await _getBalanceUseCase.ExecuteAsync(id, ct);
        if (output == null)
        {
            return NotFound(ResultGeneric<AccountBalanceOutput>.Failure($"Conta '{id}' não encontrada."));
        }

        return Ok(ResultGeneric<AccountBalanceOutput>.Success(output));
    }

    [HttpGet("accounts/{id:guid}/statement")]
    [ProducesResponseType(typeof(ResultGeneric<IReadOnlyList<LedgerStatementItemOutput>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResultGeneric<IReadOnlyList<LedgerStatementItemOutput>>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatement([FromRoute] Guid id, CancellationToken ct)
    {
        var output = await _getStatementUseCase.ExecuteAsync(id, ct);
        return Ok(ResultGeneric<IReadOnlyList<LedgerStatementItemOutput>>.Success(output));
    }
}
