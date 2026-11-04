using Application.Dto;
using Application.UseCases.Payout;
using Application.UseCases.Payout.Dto;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using MiniPspPix.API.Observability;

namespace MiniPspPix.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class PayoutController(RequestPayoutUseCase payoutUseCase) : ControllerBase
{
    private readonly RequestPayoutUseCase _payoutUseCase = payoutUseCase;

    [HttpPost]
    [ProducesResponseType(typeof(ResultGeneric<PayoutOutput>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResultGeneric<PayoutOutput>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RequestPayout([FromBody] RequestPayoutInput input, CancellationToken ct)
    {
        var output = await _payoutUseCase.ExecuteAsync(input, ct);
        PixMetrics.PayoutsRequested.Add(1);
        return Ok(ResultGeneric<PayoutOutput>.Success(output));
    }
}
