using FluentAssertions;
using MiniPspPix.Application.BrCode;
using MiniPspPix.Domain.Charges;
using MiniPspPix.Domain.Exceptions;

namespace MiniPspPix.UnitTests;

public class BrCodeBuilderTests
{
    [Fact]
    public void ComputeHex_WithKnownBacenString_ShouldReturnAccurateCrc16()
    {
        // Vector de teste oficial Bacen com prefixo 6304
        var payloadWithoutCrc = "00020126330014br.gov.bcb.pix0111123456789005204000053039865802BR5913Fulano de Tal6008BRASILIA62070503***6304";
        var crc = Crc16Ccitt.ComputeHex(payloadWithoutCrc);

        crc.Should().Be("D3E6");
    }

    [Fact]
    public void Build_WithValidParameters_ShouldGenerateConformingEmvPayload()
    {
        var builder = new BrCodeBuilder()
            .WithPixKey("12345678900")
            .WithMerchant("Loja Exemplo", "Sao Paulo")
            .WithAmount(150.50m)
            .WithTxId("TX123456789")
            .WithDescription("Pedido 123");

        var brCode = builder.Build();

        brCode.Should().NotBeNullOrWhiteSpace();
        brCode.Should().StartWith("000201"); // 00 Payload Format Indicator
        brCode.Should().Contain("26"); // 26 Merchant Account Info
        brCode.Should().Contain("br.gov.bcb.pix");
        brCode.Should().Contain("12345678900");
        brCode.Should().Contain("52040000"); // 52 MCC
        brCode.Should().Contain("5303986"); // 53 BRL
        brCode.Should().Contain("5406150.50"); // 54 Amount
        brCode.Should().Contain("5802BR"); // 58 Country Code
        brCode.Should().Contain("5912LOJA EXEMPLO"); // 59 Sanitized Name
        brCode.Should().Contain("6009SAO PAULO"); // 60 Sanitized City
        brCode.Should().Contain("62150511TX123456789"); // 62 Additional Data Field / TxId
        brCode.Should().Contain("6304"); // 63 CRC16 tag

        // Valida que o CRC16 gerado confere com o cálculo sobre o corpo
        var payloadWithoutCrc = brCode[..^4];
        var expectedCrc = Crc16Ccitt.ComputeHex(payloadWithoutCrc);
        brCode.EndsWith(expectedCrc).Should().BeTrue();
    }

    [Fact]
    public void Build_WithoutAmount_ShouldOmitTag54()
    {
        var builder = new BrCodeBuilder()
            .WithPixKey("pix@empresa.com.br")
            .WithMerchant("Empresa", "Brasilia");

        var brCode = builder.Build();

        brCode.Should().NotContain("54");
    }

    [Fact]
    public void Build_WithoutPixKey_ShouldThrowDomainException()
    {
        var builder = new BrCodeBuilder();

        var act = () => builder.Build();

        act.Should().Throw<PixDomainException>().WithMessage("*Chave Pix é obrigatória*");
    }

    [Fact]
    public void Build_WithNegativeAmount_ShouldThrowDomainException()
    {
        var builder = new BrCodeBuilder();

        var act = () => builder.WithAmount(-10m);

        act.Should().Throw<PixDomainException>().WithMessage("*não pode ser negativo*");
    }

    [Fact]
    public void GenerateSvg_ShouldContainValidSvgElementsAndPixBadge()
    {
        var svg = QrCodeSvgGenerator.GenerateSvg("00020126330014br.gov.bcb.pix...", 300);

        svg.Should().StartWith("<svg");
        svg.Should().EndWith("</svg>");
        svg.Should().Contain("PIX");
        svg.Should().Contain("#32BCAD");
    }
}

