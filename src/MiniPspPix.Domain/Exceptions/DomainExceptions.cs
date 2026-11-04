namespace MiniPspPix.Domain.Exceptions;

public class PixDomainException : Exception
{
    public PixDomainException(string message) : base(message) { }
    public PixDomainException(string message, Exception innerException) : base(message, innerException) { }
}

public class InsufficientFundsException : PixDomainException
{
    public InsufficientFundsException(string message) : base(message) { }
    public InsufficientFundsException(string message, Exception innerException) : base(message, innerException) { }
}

public class ConcurrencyException : PixDomainException
{
    public ConcurrencyException(string message) : base(message) { }
    public ConcurrencyException(string message, Exception innerException) : base(message, innerException) { }
}

public class IdempotencyMismatchException : PixDomainException
{
    public IdempotencyMismatchException(string message) : base(message) { }
    public IdempotencyMismatchException(string message, Exception innerException) : base(message, innerException) { }
}

public class IdempotencyConflictException : PixDomainException
{
    public IdempotencyConflictException(string message) : base(message) { }
    public IdempotencyConflictException(string message, Exception innerException) : base(message, innerException) { }
}
