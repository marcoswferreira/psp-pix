using Domain.Common.Exceptions;

namespace Domain.ValueObjects;

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

