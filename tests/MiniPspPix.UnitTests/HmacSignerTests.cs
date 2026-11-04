using FluentAssertions;
using MiniPspPix.Application.Webhooks;

namespace MiniPspPix.UnitTests;

public class HmacSignerTests
{
    private const string Secret = "super-secret-key-12345";
    private const string Payload = "{\"event\":\"pix.received\",\"amount\":100.00}";

    [Fact]
    public void CreateSignature_ShouldGenerateStandardFormat()
    {
        var signature = HmacSigner.CreateSignature(Payload, Secret);

        signature.Should().StartWith("t=");
        signature.Should().Contain(",v1=");
    }

    [Fact]
    public void VerifySignature_WithValidSignature_ShouldReturnTrue()
    {
        var signature = HmacSigner.CreateSignature(Payload, Secret);
        var isValid = HmacSigner.VerifySignature(Payload, signature, Secret);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void VerifySignature_WithTamperedPayload_ShouldReturnFalse()
    {
        var signature = HmacSigner.CreateSignature(Payload, Secret);
        var tamperedPayload = "{\"event\":\"pix.received\",\"amount\":9999.00}";

        var isValid = HmacSigner.VerifySignature(tamperedPayload, signature, Secret);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void VerifySignature_WithWrongSecret_ShouldReturnFalse()
    {
        var signature = HmacSigner.CreateSignature(Payload, Secret);

        var isValid = HmacSigner.VerifySignature(Payload, signature, "different-secret-key");

        isValid.Should().BeFalse();
    }

    [Fact]
    public void VerifySignature_WithExpiredTimestamp_ShouldReturnFalse()
    {
        // Timestamp de 10 minutos atrás
        var oldTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();
        var signature = HmacSigner.CreateSignature(Payload, Secret, oldTimestamp);

        var isValid = HmacSigner.VerifySignature(Payload, signature, Secret, tolerance: TimeSpan.FromMinutes(5));

        isValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-format")]
    [InlineData("t=123")]
    [InlineData("v1=abc")]
    public void VerifySignature_WithMalformedHeader_ShouldReturnFalse(string malformedHeader)
    {
        var isValid = HmacSigner.VerifySignature(Payload, malformedHeader, Secret);

        isValid.Should().BeFalse();
    }
}

