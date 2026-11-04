namespace Domain.Common.Exceptions;

public class IdempotencyConflictException : DomainException
{
    public IdempotencyConflictException(string message) : base(message) { }
    public IdempotencyConflictException(string message, Exception innerException) : base(message, innerException) { }
}

