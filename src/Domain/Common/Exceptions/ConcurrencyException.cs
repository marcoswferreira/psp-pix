namespace Domain.Common.Exceptions;

public class ConcurrencyException : DomainException
{
    public ConcurrencyException(string message) : base(message) { }
    public ConcurrencyException(string message, Exception innerException) : base(message, innerException) { }
}

