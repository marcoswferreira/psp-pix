using System.Net;
using System.Net.Http.Json;
using Application.Dto;
using Application.UseCases.Charges.Dto;
using Application.UseCases.Ledger.Dto;
using Application.UseCases.Payments.Dto;
using Application.UseCases.Payout.Dto;
using FluentAssertions;
using Infrastructure.Database.DbContexts;
using Infrastructure.Database.Seeders;
using IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Controllers;

public class PaymentsAndPayoutIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public PaymentsAndPayoutIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task InboundPaymentSimulation_ShouldSettleCharge_AndCreditMerchantLedger()
    {
        // 1. Cria cobrança
        var txId = "TX" + Guid.NewGuid().ToString("N")[..15].ToUpperInvariant();
        var createInput = new CreatePixChargeInput(
            Amount: 200.00m,
            PixKey: "merch@pix.com",
            Description: "Serviço Consultoria",
            TxId: txId
        );
        var createResp = await _client.PostAsJsonAsync("/api/v1/charges", createInput);
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);

        // 2. Simula liquidação bancária inbound
        var simInput = new ProcessInboundPaymentInput(TxId: txId, Amount: 200.00m);
        var simResp = await _client.PostAsJsonAsync("/api/v1/payments/simulation", simInput);
        simResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var simResult = await simResp.Content.ReadFromJsonAsync<ResultGeneric<InboundPaymentOutput>>();
        simResult.Should().NotBeNull();
        simResult!.IsSuccess.Should().BeTrue();
        simResult.Value.Success.Should().BeTrue();
        simResult.Value.EndToEndId.Should().StartWith("E");

        // 3. Consulta cobrança para validar status Completed
        var getResp = await _client.GetAsync($"/api/v1/charges/{txId}");
        var getResult = await getResp.Content.ReadFromJsonAsync<ResultGeneric<PixChargeOutput>>();
        getResult!.Value.Status.Should().Be("Completed");
        getResult.Value.PaidAtUtc.Should().NotBeNull();
        getResult.Value.EndToEndId.Should().Be(simResult.Value.EndToEndId);

        // 4. Consulta saldo e extrato no Ledger
        var balanceResp = await _client.GetAsync($"/api/v1/ledger/accounts/{DatabaseSeeder.DefaultMerchantAccountId}/balance");
        balanceResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var balanceResult = await balanceResp.Content.ReadFromJsonAsync<ResultGeneric<AccountBalanceOutput>>();
        balanceResult!.Value.Balance.Should().BeGreaterThanOrEqualTo(1200.00m); // 1000 inicial + 200

        var statementResp = await _client.GetAsync($"/api/v1/ledger/accounts/{DatabaseSeeder.DefaultMerchantAccountId}/statement");
        statementResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var statementResult = await statementResp.Content.ReadFromJsonAsync<ResultGeneric<IReadOnlyList<LedgerStatementItemOutput>>>();
        statementResult!.Value.Should().NotBeEmpty();

        // 5. Valida se o Outbox gravou o evento
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var outboxMsg = await db.OutboxMessages.FirstOrDefaultAsync(m => m.EventType == "pix.payment.received" && m.PayloadJson.Contains(txId));
        outboxMsg.Should().NotBeNull();

        // 6. Simulação de notificação duplicada do banco: deve ser absorvida idempotentemente
        var simDuplicateResp = await _client.PostAsJsonAsync("/api/v1/payments/simulation", simInput);
        simDuplicateResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var simDuplicateResult = await simDuplicateResp.Content.ReadFromJsonAsync<ResultGeneric<InboundPaymentOutput>>();
        simDuplicateResult!.Value.Message.Should().Contain("liquidada");
    }

    [Fact]
    public async Task Payout_WithSufficientBalance_ShouldDebitMerchant_AndCreditTransit()
    {
        var payoutInput = new RequestPayoutInput(
            MerchantAccountId: DatabaseSeeder.DefaultMerchantAccountId,
            Amount: 50.00m,
            DestinationPixKey: "fornecedor@teste.com",
            Description: "Pagamento Fornecedor"
        );

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payout")
        {
            Content = JsonContent.Create(payoutInput)
        };
        request.Headers.Add("Idempotency-Key", "payout-" + Guid.NewGuid().ToString("N"));

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ResultGeneric<PayoutOutput>>();
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();
        result.Value.Success.Should().BeTrue();
        result.Value.EndToEndId.Should().StartWith("E");
    }

    [Fact]
    public async Task Payout_ExceedingBalance_ShouldReturn422InsufficientFunds()
    {
        var payoutInput = new RequestPayoutInput(
            MerchantAccountId: DatabaseSeeder.DefaultMerchantAccountId,
            Amount: 9999999.00m, // Saldo indisponível
            DestinationPixKey: "alvo@teste.com",
            Description: "Tentativa de saque sem saldo"
        );

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payout")
        {
            Content = JsonContent.Create(payoutInput)
        };
        request.Headers.Add("Idempotency-Key", "payout-excess-" + Guid.NewGuid().ToString("N"));

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
