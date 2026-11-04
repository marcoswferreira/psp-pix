using FluentAssertions;
using Infrastructure.Idempotency;
using Infrastructure.Services;

namespace Application.UnitTests.Services;

public class HmacAndIdempotencyTests
{
    private readonly HmacSignerService _signer = new();

    [Fact]
    public void Hmac_CreateAndVerify_ShouldBeValidWithMatchingSecret()
    {
        var secret = "secret_key_123456789";
        var payload = "{\"event\":\"charge.paid\",\"amount\":100.00}";

        var signature = _signer.CreateSignature(payload, secret);

        signature.Should().StartWith("t=");
        signature.Should().Contain(",v1=");

        var isValid = _signer.VerifySignature(payload, signature, secret);
        isValid.Should().BeTrue();
    }

    [Fact]
    public void Hmac_Verify_WithWrongSecret_ShouldFail()
    {
        var secret = "correct_secret";
        var wrongSecret = "wrong_secret";
        var payload = "{\"event\":\"charge.paid\"}";

        var signature = _signer.CreateSignature(payload, secret);
        var isValid = _signer.VerifySignature(payload, signature, wrongSecret);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void Hmac_Verify_WithTamperedPayload_ShouldFail()
    {
        var secret = "super_secret";
        var payload = "{\"amount\":10.00}";
        var tamperedPayload = "{\"amount\":1000.00}";

        var signature = _signer.CreateSignature(payload, secret);
        var isValid = _signer.VerifySignature(tamperedPayload, signature, secret);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void IdempotencyHasher_CanonicalJson_ShouldProduceSameHashRegardlessOfPropertyOrder()
    {
        var jsonA = "{\"amount\": 100.00, \"pixKey\": \"test@pix.com\", \"description\": \"Demo\"}";
        var jsonB = "{\"description\": \"Demo\", \"amount\": 100.00, \"pixKey\": \"test@pix.com\"}";

        var hashA = IdempotencyHasher.ComputeRequestHash("POST", "/api/v1/charges", jsonA);
        var hashB = IdempotencyHasher.ComputeRequestHash("POST", "/api/v1/charges", jsonB);

        hashA.Should().Be(hashB);
    }

    [Fact]
    public void IdempotencyHasher_DifferentContent_ShouldProduceDifferentHash()
    {
        var jsonA = "{\"amount\": 100.00}";
        var jsonB = "{\"amount\": 200.00}";

        var hashA = IdempotencyHasher.ComputeRequestHash("POST", "/api/v1/charges", jsonA);
        var hashB = IdempotencyHasher.ComputeRequestHash("POST", "/api/v1/charges", jsonB);

        hashA.Should().NotBe(hashB);
    }
}

