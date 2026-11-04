using Application.Dto;
using Application.UseCases.Webhooks;
using Application.UseCases.Webhooks.Dto;
using Asp.Versioning;
using Domain.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace MiniPspPix.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class WebhooksController(
    RegisterWebhookUseCase registerWebhookUseCase,
    IHmacSignerService hmacSigner) : ControllerBase
{
    private readonly RegisterWebhookUseCase _registerWebhookUseCase = registerWebhookUseCase;
    private readonly IHmacSignerService _hmacSigner = hmacSigner;

    [HttpPost]
    [ProducesResponseType(typeof(ResultGeneric<WebhookSubscriptionOutput>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResultGeneric<WebhookSubscriptionOutput>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterWebhook([FromBody] RegisterWebhookInput input, CancellationToken ct)
    {
        var output = await _registerWebhookUseCase.ExecuteAsync(input, ct);
        return Ok(ResultGeneric<WebhookSubscriptionOutput>.Success(output));
    }

    [HttpPost("test-receiver")]
    public async Task<IActionResult> TestReceiver([FromHeader(Name = "X-Signature")] string? signature)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        return Ok(new
        {
            received = true,
            receivedSignature = signature,
            payloadLength = body.Length,
            timestampUtc = DateTime.UtcNow
        });
    }
}

