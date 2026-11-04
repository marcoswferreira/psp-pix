using System.Text.RegularExpressions;
using Domain.Common.Exceptions;
using Domain.Enums;

namespace Domain.ValueObjects;

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

