using System.Net;
using System.Net.Http.Json;
using Application.Dto;
using Application.UseCases.Charges.Dto;
using FluentAssertions;
using IntegrationTests.Common;

namespace IntegrationTests.Controllers;

public class ChargesControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public ChargesControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostCharge_ShouldCreateCharge_WithBrCodeAndSvg()
    {
        var input = new CreatePixChargeInput(
            Amount: 125.75m,
            PixKey: "12345678901",
            Description: "Assinatura Mensal",
            MerchantName: "Livraria Cultura",
            MerchantCity: "Sao Paulo"
        );

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/charges")
        {
            Content = JsonContent.Create(input)
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ResultGeneric<PixChargeOutput>>();
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();
        result.Value.Amount.Should().Be(125.75m);
        result.Value.BrCode.Should().StartWith("000201");
        result.Value.BrCode.Should().Contain("12345678901");
        result.Value.QrCodeSvg.Should().Contain("<svg");
        result.Value.Status.Should().Be("Active");
    }

    [Fact]
    public async Task PostCharge_WithSameIdempotencyKey_ShouldReturnCachedReplay()
    {
        var key = "idemp-key-" + Guid.NewGuid().ToString("N");
        var input = new CreatePixChargeInput(
            Amount: 89.90m,
            PixKey: "pix@ecommerce.com",
            Description: "Fone de Ouvido"
        );

        // 1ª Chamada
        var request1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/charges")
        {
            Content = JsonContent.Create(input)
        };
        request1.Headers.Add("Idempotency-Key", key);
        var response1 = await _client.SendAsync(request1);
        response1.StatusCode.Should().Be(HttpStatusCode.Created);
        response1.Headers.Contains("Idempotent-Replay").Should().BeFalse();
        var result1 = await response1.Content.ReadFromJsonAsync<ResultGeneric<PixChargeOutput>>();

        // 2ª Chamada (Mesma chave + mesmo payload)
        var request2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/charges")
        {
            Content = JsonContent.Create(input)
        };
        request2.Headers.Add("Idempotency-Key", key);
        var response2 = await _client.SendAsync(request2);

        response2.StatusCode.Should().Be(HttpStatusCode.Created);
        response2.Headers.Contains("Idempotent-Replay").Should().BeTrue();
        var result2 = await response2.Content.ReadFromJsonAsync<ResultGeneric<PixChargeOutput>>();

        result2!.Value.TxId.Should().Be(result1!.Value.TxId);
        result2.Value.BrCode.Should().Be(result1.Value.BrCode);
    }

    [Fact]
    public async Task PostCharge_WithSameIdempotencyKey_DifferentPayload_ShouldReturn422Conflict()
    {
        var key = "idemp-key-conflict-" + Guid.NewGuid().ToString("N");
        var input1 = new CreatePixChargeInput(Amount: 50.00m, PixKey: "chave1@pix.com", Description: "Item A");
        var input2 = new CreatePixChargeInput(Amount: 999.00m, PixKey: "chave2@pix.com", Description: "Item B Divergente");

        var request1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/charges")
        {
            Content = JsonContent.Create(input1)
        };
        request1.Headers.Add("Idempotency-Key", key);
        var response1 = await _client.SendAsync(request1);
        response1.StatusCode.Should().Be(HttpStatusCode.Created);

        // 2ª Chamada reutilizando a chave com outro payload
        var request2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/charges")
        {
            Content = JsonContent.Create(input2)
        };
        request2.Headers.Add("Idempotency-Key", key);
        var response2 = await _client.SendAsync(request2);

        response2.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task GetChargeByTxId_WhenExists_ShouldReturnCharge()
    {
        var txId = "TX" + Guid.NewGuid().ToString("N")[..15].ToUpperInvariant();
        var input = new CreatePixChargeInput(
            Amount: 42.00m,
            PixKey: "12345678901",
            Description: "Item",
            TxId: txId
        );

        var createResp = await _client.PostAsJsonAsync("/api/v1/charges", input);
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var getResp = await _client.GetAsync($"/api/v1/charges/{txId}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await getResp.Content.ReadFromJsonAsync<ResultGeneric<PixChargeOutput>>();
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();
        result.Value.TxId.Should().Be(txId);
        result.Value.Amount.Should().Be(42.00m);
    }

    [Fact]
    public async Task GetChargeByTxId_WhenNotFound_ShouldReturn404()
    {
        var response = await _client.GetAsync("/api/v1/charges/TXNONEXISTENT9999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

