using System.Text.RegularExpressions;
using Domain.Common.Exceptions;

namespace Domain.ValueObjects;

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

