namespace MiniPspPix.Domain.Exceptions;

public class PixDomainException : Exception
{
    public PixDomainException(string message) : base(message) { }
    public PixDomainException(string message, Exception innerException) : base(message, innerException) { }
}

public class InsufficientFundsException : PixDomainException
{
    public InsufficientFundsException(string message) : base(message) { }
}

public class ConcurrencyException : PixDomainException
{
    public ConcurrencyException(string message) : base(message) { }
}

public class IdempotencyMismatchException : PixDomainException
{
    public IdempotencyMismatchException(string message) : base(message) { }
}

public class IdempotencyConflictException : PixDomainException
{
    public IdempotencyConflictException(string message) : base(message) { }
}

