using Application.Dto;
using Application.UseCases.Payments;
using Application.UseCases.Payments.Dto;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using MiniPspPix.API.Observability;

namespace MiniPspPix.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class PaymentsController(ProcessInboundPaymentUseCase paymentUseCase) : ControllerBase
{
    private readonly ProcessInboundPaymentUseCase _paymentUseCase = paymentUseCase;

    [HttpPost("simulation")]
    [ProducesResponseType(typeof(ResultGeneric<InboundPaymentOutput>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResultGeneric<InboundPaymentOutput>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ProcessInboundPayment([FromBody] ProcessInboundPaymentInput input, CancellationToken ct)
    {
        var output = await _paymentUseCase.ExecuteAsync(input, ct);
        PixMetrics.PaymentsProcessed.Add(1);
        return Ok(ResultGeneric<InboundPaymentOutput>.Success(output));
    }
}
