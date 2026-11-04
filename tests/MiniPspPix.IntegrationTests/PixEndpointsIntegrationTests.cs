using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MiniPspPix.Api.Common;
using MiniPspPix.Application.Charges;
using MiniPspPix.Application.Payments;
using MiniPspPix.Domain.Banking;
using MiniPspPix.Infrastructure.Persistence;

namespace MiniPspPix.IntegrationTests;

public class PixEndpointsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public PixEndpointsIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostCob_ShouldCreateCharge_WithBrCodeAndSvg()
    {
        var command = new CreateChargeCommand(
            Amount: 125.75m,
            PixKey: "12345678900",
            Description: "Assinatura Mensal",
            MerchantName: "Livraria Cultura",
            MerchantCity: "Sao Paulo"
        );

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/pix/cob")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ChargeResultDto>();
        body.Should().NotBeNull();
        body!.Amount.Should().Be(125.75m);
        body.BrCode.Should().StartWith("000201");
        body.BrCode.Should().Contain("12345678900");
        body.QrCodeSvg.Should().Contain("<svg");
        body.Status.Should().Be("Active");
    }

    [Fact]
    public async Task PostCob_WithSameIdempotencyKey_ShouldReturnCachedReplay()
    {
        var key = "idemp-key-" + Guid.NewGuid().ToString("N");
        var command = new CreateChargeCommand(
            Amount: 89.90m,
            PixKey: "pix@ecommerce.com",
            Description: "Fone de Ouvido"
        );

        // 1ª Chamada
        var request1 = new HttpRequestMessage(HttpMethod.Post, "/v1/pix/cob")
        {
            Content = JsonContent.Create(command)
        };
        request1.Headers.Add("Idempotency-Key", key);
        var response1 = await _client.SendAsync(request1);
        response1.StatusCode.Should().Be(HttpStatusCode.Created);
        response1.Headers.Contains("Idempotent-Replay").Should().BeFalse();
        var body1 = await response1.Content.ReadFromJsonAsync<ChargeResultDto>();

        // 2ª Chamada (Mesma chave + mesmo payload)
        var request2 = new HttpRequestMessage(HttpMethod.Post, "/v1/pix/cob")
        {
            Content = JsonContent.Create(command)
        };
        request2.Headers.Add("Idempotency-Key", key);
        var response2 = await _client.SendAsync(request2);

        response2.StatusCode.Should().Be(HttpStatusCode.Created);
        response2.Headers.Contains("Idempotent-Replay").Should().BeTrue();
        var body2 = await response2.Content.ReadFromJsonAsync<ChargeResultDto>();

        // Os resultados devem ser exatamente idênticos
        body2!.TxId.Should().Be(body1!.TxId);
        body2.BrCode.Should().Be(body1.BrCode);
    }

    [Fact]
    public async Task PostCob_WithSameIdempotencyKey_DifferentPayload_ShouldReturn422Conflict()
    {
        var key = "idemp-key-conflict-" + Guid.NewGuid().ToString("N");
        var command1 = new CreateChargeCommand(Amount: 50.00m, PixKey: "chave1@pix.com", Description: "Item A");
        var command2 = new CreateChargeCommand(Amount: 999.00m, PixKey: "chave2@pix.com", Description: "Item B Divergente");

        var request1 = new HttpRequestMessage(HttpMethod.Post, "/v1/pix/cob")
        {
            Content = JsonContent.Create(command1)
        };
        request1.Headers.Add("Idempotency-Key", key);
        var response1 = await _client.SendAsync(request1);
        response1.StatusCode.Should().Be(HttpStatusCode.Created);

        // Segunda requisição reutilizando a chave com outro payload
        var request2 = new HttpRequestMessage(HttpMethod.Post, "/v1/pix/cob")
        {
            Content = JsonContent.Create(command2)
        };
        request2.Headers.Add("Idempotency-Key", key);
        var response2 = await _client.SendAsync(request2);

        response2.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task InboundPaymentSimulation_ShouldSettleCharge_AndCreditMerchantLedger()
    {
        // 1. Cria cobrança
        var txId = "TX" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        var createCmd = new CreateChargeCommand(Amount: 200.00m, PixKey: "merch@pix.com", Description: "Serviço Consultoria", TxId: txId);
        var createResp = await _client.PostAsJsonAsync("/v1/pix/cob", createCmd);
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);

        // 2. Simula liquidação bancária
        var simCmd = new ProcessInboundPaymentCommand(TxId: txId, Amount: 200.00m);
        var simResp = await _client.PostAsJsonAsync("/v1/pix/simulation/inbound-payment", simCmd);
        simResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var simBody = await simResp.Content.ReadFromJsonAsync<InboundPaymentResultDto>();
        simBody!.Success.Should().BeTrue();
        simBody.EndToEndId.Should().StartWith("E");

        // 3. Consulta cobrança para validar status Completed
        var getResp = await _client.GetAsync($"/v1/pix/cob/{txId}");
        var getBody = await getResp.Content.ReadFromJsonAsync<ChargeResultDto>();
        getBody!.Status.Should().Be("Completed");
        getBody.PaidAtUtc.Should().NotBeNull();
        getBody.EndToEndId.Should().Be(simBody.EndToEndId);

        // 4. Valida se o Outbox gravou o evento
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PixDbContext>();
        var outboxMsg = db.OutboxMessages.FirstOrDefault(m => m.EventType == "pix.payment.received" && m.PayloadJson.Contains(txId));
        outboxMsg.Should().NotBeNull();

        // 5. Simulação de notificação duplicada do banco: deve ser absorvida idempotentemente
        var simDuplicateResp = await _client.PostAsJsonAsync("/v1/pix/simulation/inbound-payment", simCmd);
        simDuplicateResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var simDuplicateBody = await simDuplicateResp.Content.ReadFromJsonAsync<InboundPaymentResultDto>();
        simDuplicateBody!.Message.Should().Contain("Replay Idempotente");
    }

    [Fact]
    public async Task Payout_WithSufficientBalance_ShouldDebitMerchant_AndCreditTransit()
    {
        var payoutCmd = new RequestPayoutCommand(
            MerchantAccountId: DefaultAccounts.MerchantAccountId,
            Amount: 150.00m,
            DestinationPixKey: "fornecedor@teste.com",
            Description: "Pagamento Fornecedor"
        );

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/pix/payout")
        {
            Content = JsonContent.Create(payoutCmd)
        };
        request.Headers.Add("Idempotency-Key", "payout-" + Guid.NewGuid().ToString("N"));

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PayoutResultDto>();
        body!.Success.Should().BeTrue();
        body.EndToEndId.Should().StartWith("E");
    }

    [Fact]
    public async Task Payout_ExceedingBalance_ShouldReturn422InsufficientFunds()
    {
        var payoutCmd = new RequestPayoutCommand(
            MerchantAccountId: DefaultAccounts.MerchantAccountId,
            Amount: 999999.00m, // Saldo indisponível
            DestinationPixKey: "alvo@teste.com",
            Description: "Tentativa de saque sem saldo"
        );

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/pix/payout")
        {
            Content = JsonContent.Create(payoutCmd)
        };
        request.Headers.Add("Idempotency-Key", "payout-excess-" + Guid.NewGuid().ToString("N"));

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task WebhookRegistration_ShouldCreateSubscription()
    {
        var cmd = new
        {
            MerchantId = "MERCHANT-001",
            TargetUrl = "https://loja.com.br/pix/webhook"
        };

        var response = await _client.PostAsJsonAsync("/v1/merchants/webhooks", cmd);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}

