namespace Domain.Enums;

public enum ChargeStatus
{
    Created = 1,
    Active = 2,
    Completed = 3,
    Expired = 4,
    Rejected = 5
}

public enum PixKeyType
{
    Cpf = 1,
    Cnpj = 2,
    Email = 3,
    Phone = 4,
    Random = 5
}

public enum AccountType
{
    Merchant = 1,
    PixSettlementTransit = 2,
    PlatformFees = 3
}

public enum EntryType
{
    Debit = 1,
    Credit = 2
}

public enum IdempotencyStatus
{
    InProgress = 1,
    Completed = 2,
    Failed = 3
}

public enum WebhookDeliveryStatus
{
    Pending = 1,
    Delivered = 2,
    Failed = 3,
    DeadLettered = 4
}

public enum ChaosAction
{
    None = 0,
    Delay = 1,
    Timeout = 2,
    Fail = 3,
    Duplicate = 4
}
