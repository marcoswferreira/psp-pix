using FluentAssertions;
using Infrastructure.Services;

namespace Application.UnitTests.Services;

public class BrCodeServiceTests
{
    private readonly BrCodeService _service = new();

    [Fact]
    public void ComputeCrc16Hex_WithKnownBacenString_ShouldReturnD3E6()
    {
        // Vetor oficial Bacen
        var payloadWithoutCrc = "00020126330014br.gov.bcb.pix0111123456789005204000053039865802BR5913Fulano de Tal6008BRASILIA62070503***6304";
        var crc = _service.ComputeCrc16Hex(payloadWithoutCrc);

        crc.Should().Be("D3E6");
    }

    [Fact]
    public void GenerateBrCode_WithStandardParameters_ShouldContainAllEMVTags()
    {
        var brCode = _service.GenerateBrCode(
            pixKey: "12345678901",
            amount: 125.75m,
            merchantName: "Loja ABC",
            merchantCity: "Sao Paulo",
            txId: "TX1234567890123456",
            description: "Pagamento Pedido 99"
        );

        brCode.Should().StartWith("000201");
        brCode.Should().Contain("26"); // Merchant Account Information
        brCode.Should().Contain("br.gov.bcb.pix");
        brCode.Should().Contain("12345678901");
        brCode.Should().Contain("52040000"); // MCC
        brCode.Should().Contain("5303986"); // Currency BRL
        brCode.Should().Contain("5406125.75"); // Transaction Amount
        brCode.Should().Contain("5802BR"); // Country Code
        brCode.Should().Contain("5908LOJA ABC"); // Merchant Name
        brCode.Should().Contain("6009SAO PAULO"); // Merchant City
        brCode.Should().Contain("62"); // Additional Data Field (TxId)
        brCode.Should().Contain("6304"); // CRC16

        // Validar CRC16
        var bodyWithoutCrc = brCode[..^4];
        var expectedCrc = _service.ComputeCrc16Hex(bodyWithoutCrc);
        brCode.EndsWith(expectedCrc).Should().BeTrue();
    }

    [Fact]
    public void GenerateBrCode_WithoutAmount_ShouldOmitTag54()
    {
        var brCode = _service.GenerateBrCode(
            pixKey: "pix@teste.com",
            amount: 0m,
            merchantName: "Empresa",
            merchantCity: "Curitiba",
            txId: "TX1234567890123456"
        );

        brCode.Should().NotContain("540");
    }

    [Fact]
    public void GenerateSvg_ShouldOutputValidSvgWithPixBranding()
    {
        var svg = _service.GenerateSvg("00020126...BRCode", size: 300);

        svg.Should().StartWith("<svg");
        svg.Should().EndWith("</svg>");
        svg.Should().Contain("PIX");
        svg.Should().Contain("#32BCAD");
    }
}
