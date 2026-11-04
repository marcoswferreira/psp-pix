namespace Domain.Common.Exceptions;

public class IdempotencyMismatchException : DomainException
{
    public IdempotencyMismatchException(string message) : base(message) { }
    public IdempotencyMismatchException(string message, Exception innerException) : base(message, innerException) { }
}

