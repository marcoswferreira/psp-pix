using Application.Dto;
using Application.UseCases.Charges;
using Application.UseCases.Charges.Dto;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using MiniPspPix.API.Observability;

namespace MiniPspPix.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class ChargesController(
    CreatePixChargeUseCase createChargeUseCase,
    GetPixChargeByTxIdUseCase getChargeUseCase) : ControllerBase
{
    private readonly CreatePixChargeUseCase _createChargeUseCase = createChargeUseCase;
    private readonly GetPixChargeByTxIdUseCase _getChargeUseCase = getChargeUseCase;

    [HttpPost]
    [ProducesResponseType(typeof(ResultGeneric<PixChargeOutput>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResultGeneric<PixChargeOutput>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateCharge([FromBody] CreatePixChargeInput input, CancellationToken ct)
    {
        var output = await _createChargeUseCase.ExecuteAsync(input, ct);
        PixMetrics.ChargesCreated.Add(1);

        var result = ResultGeneric<PixChargeOutput>.Success(output);
        return CreatedAtAction(nameof(GetChargeByTxId), new { version = "1.0", txId = output.TxId }, result);
    }

    [HttpGet("{txId}")]
    [ProducesResponseType(typeof(ResultGeneric<PixChargeOutput>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResultGeneric<PixChargeOutput>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChargeByTxId([FromRoute] string txId, CancellationToken ct)
    {
        var output = await _getChargeUseCase.ExecuteAsync(txId, ct);
        if (output == null)
        {
            return NotFound(ResultGeneric<PixChargeOutput>.Failure($"Cobrança Pix '{txId}' não encontrada."));
        }

        return Ok(ResultGeneric<PixChargeOutput>.Success(output));
    }
}
