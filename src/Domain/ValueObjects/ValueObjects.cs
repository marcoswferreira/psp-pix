using System.Text.RegularExpressions;
using Domain.Common.Exceptions;
using Domain.Enums;

namespace Domain.ValueObjects;

public interface IValueObject { }

public record PixKey : IValueObject
{
    public string Value { get; }
    public PixKeyType Type { get; }

    public PixKey(string value, PixKeyType? type = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Chave Pix não pode ser vazia.");

        Value = value.Trim();
        Type = type ?? DetectType(Value);
    }

    private static PixKeyType DetectType(string value)
    {
        var digitsOnly = Regex.Replace(value, @"\D", "");
        if (value.Contains('@'))
            return PixKeyType.Email;
        if (digitsOnly.Length == 11 && !value.StartsWith("+"))
            return PixKeyType.Cpf;
        if (digitsOnly.Length == 14)
            return PixKeyType.Cnpj;
        if (value.StartsWith("+") || (digitsOnly.Length is >= 12 and <= 13))
            return PixKeyType.Phone;
        if (Guid.TryParse(value, out _))
            return PixKeyType.Random;

        throw new DomainException($"Chave Pix '{value}' não corresponde a nenhum formato válido (CPF, CNPJ, E-mail, Telefone ou EVP/Chave Aleatória).");
    }

    public override string ToString() => Value;
}

public record TxId : IValueObject
{
    private static readonly Regex AlphanumericRegex = new("^[a-zA-Z0-9]{1,35}$", RegexOptions.Compiled);

    public string Value { get; }

    public TxId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !AlphanumericRegex.IsMatch(value))
            throw new DomainException("TxId deve ter entre 1 e 35 caracteres alfanuméricos.");

        Value = value;
    }

    public static TxId NewTxId() => new(Guid.NewGuid().ToString("N"));

    public override string ToString() => Value;
}

public record EndToEndId : IValueObject
{
    public string Value { get; }

    public EndToEndId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 32 || !value.StartsWith("E"))
            throw new DomainException("EndToEndId Pix deve ter 32 caracteres iniciando com 'E'.");

        Value = value;
    }

    public static EndToEndId Generate(string ispb = "00000000")
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmm");
        var randomPart = Guid.NewGuid().ToString("N")[..11];
        return new($"E{ispb.PadLeft(8, '0')[..8]}{timestamp}{randomPart}");
    }

    public override string ToString() => Value;
}
