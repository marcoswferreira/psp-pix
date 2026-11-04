using System.Net;
using System.Net.Http.Json;
using Application.Dto;
using Application.UseCases.Webhooks.Dto;
using FluentAssertions;
using IntegrationTests.Common;

namespace IntegrationTests.Controllers;

public class WebhooksIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public WebhooksIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task WebhookRegistration_ShouldCreateSubscription()
    {
        var input = new RegisterWebhookInput(
            MerchantId: "MERCHANT-001",
            TargetUrl: "https://loja.com.br/pix/webhook",
            SecretKey: "secret_webhook_123"
        );

        var response = await _client.PostAsJsonAsync("/api/v1/webhooks", input);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ResultGeneric<WebhookSubscriptionOutput>>();
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();
        result.Value.MerchantId.Should().Be("MERCHANT-001");
        result.Value.TargetUrl.Should().Be("https://loja.com.br/pix/webhook");
    }

    [Fact]
    public async Task WebhookTestReceiver_ShouldAcknowledgePayloadAndSignature()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/test-receiver")
        {
            Content = JsonContent.Create(new { eventType = "pix.payment.received", amount = 100 })
        };
        request.Headers.Add("X-Signature", "t=12345,v1=abcde");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
