using Domain.Common.Exceptions;
using Domain.Enums;
using Domain.ValueObjects;
using FluentAssertions;

namespace Domain.UnitTests.ValueObjects;

public class ValueObjectTests
{
    [Theory]
    [InlineData("12345678901", PixKeyType.Cpf)]
    [InlineData("12345678000195", PixKeyType.Cnpj)]
    [InlineData("teste@exemplo.com.br", PixKeyType.Email)]
    [InlineData("+5511999998888", PixKeyType.Phone)]
    [InlineData("5511999998888", PixKeyType.Phone)]
    [InlineData("123e4567-e89b-12d3-a456-426614174000", PixKeyType.Random)]
    public void PixKey_WithValidValue_ShouldDetectCorrectType(string rawKey, PixKeyType expectedType)
    {
        var pixKey = new PixKey(rawKey);

        pixKey.Value.Should().Be(rawKey.Trim());
        pixKey.Type.Should().Be(expectedType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("chave_invalida_sem_formato")]
    public void PixKey_WithInvalidValue_ShouldThrowDomainException(string invalidKey)
    {
        var act = () => new PixKey(invalidKey);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void TxId_NewTxId_ShouldGenerateValidLength()
    {
        var txId = TxId.NewTxId();

        txId.Value.Should().NotBeNullOrWhiteSpace();
        txId.Value.Length.Should().BeInRange(26, 35);
    }

    [Fact]
    public void TxId_WithValidCustomValue_ShouldPreserveValue()
    {
        var custom = "TX12345678901234567890123456";
        var txId = new TxId(custom);

        txId.Value.Should().Be(custom);
    }

    [Theory]
    [InlineData("too-short")]
    [InlineData("invalid*char#with@special!chars")]
    public void TxId_WithInvalidValue_ShouldThrowDomainException(string invalidTxId)
    {
        var act = () => new TxId(invalidTxId);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void EndToEndId_Generate_ShouldFollowBacenStandard()
    {
        var ispb = "12345678";
        var e2e = EndToEndId.Generate(ispb);

        e2e.Value.Should().StartWith($"E{ispb}");
        e2e.Value.Length.Should().Be(32);
    }

    [Fact]
    public void EndToEndId_WithValidValue_ShouldBeAccepted()
    {
        var valid = "E1234567820261104120000000000001";
        var e2e = new EndToEndId(valid);

        e2e.Value.Should().Be(valid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("SHORT")]
    [InlineData("D1234567820261104120000000000001")] // Doesn't start with E
    public void EndToEndId_WithInvalidValue_ShouldThrowDomainException(string invalid)
    {
        var act = () => new EndToEndId(invalid);

        act.Should().Throw<DomainException>();
    }
}
