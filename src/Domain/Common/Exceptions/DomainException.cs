namespace Domain.Common.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception innerException) : base(message, innerException) { }
}

public class InsufficientFundsException : DomainException
{
    public InsufficientFundsException(string message) : base(message) { }
    public InsufficientFundsException(string message, Exception innerException) : base(message, innerException) { }
}

public class ConcurrencyException : DomainException
{
    public ConcurrencyException(string message) : base(message) { }
    public ConcurrencyException(string message, Exception innerException) : base(message, innerException) { }
}

public class IdempotencyMismatchException : DomainException
{
    public IdempotencyMismatchException(string message) : base(message) { }
    public IdempotencyMismatchException(string message, Exception innerException) : base(message, innerException) { }
}

public class IdempotencyConflictException : DomainException
{
    public IdempotencyConflictException(string message) : base(message) { }
    public IdempotencyConflictException(string message, Exception innerException) : base(message, innerException) { }
}
