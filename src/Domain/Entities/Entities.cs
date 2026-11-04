using Domain.Common.Entities;
using Domain.Common.Exceptions;
using Domain.Enums;
using Domain.ValueObjects;

namespace Domain.Entities;

public class PixCharge : BaseEntity, IAggregateRoot
{
    public string TxId { get; private set; }
    public decimal Amount { get; private set; }
    public ChargeStatus Status { get; private set; }
    public string PixKeyValue { get; private set; }
    public PixKeyType PixKeyType { get; private set; }
    public string Description { get; private set; }
    public string MerchantName { get; private set; }
    public string MerchantCity { get; private set; }
    public string BrCode { get; private set; }
    public string? QrCodeSvg { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? PaidAtUtc { get; private set; }
    public string? EndToEndId { get; private set; }

    protected PixCharge()
    {
        TxId = null!;
        PixKeyValue = null!;
        Description = null!;
        MerchantName = null!;
        MerchantCity = null!;
        BrCode = null!;
    }

    public PixCharge(
        TxId txId,
        decimal amount,
        PixKey pixKey,
        string description,
        string merchantName,
        string merchantCity,
        string brCode,
        string? qrCodeSvg,
        TimeSpan? expiresIn = null)
    {
        if (amount <= 0)
            throw new DomainException("Valor da cobrança Pix deve ser maior que zero.");

        Id = Guid.NewGuid();
        TxId = txId.Value;
        Amount = amount;
        Status = ChargeStatus.Active;
        PixKeyValue = pixKey.Value;
        PixKeyType = pixKey.Type;
        Description = description;
        MerchantName = merchantName;
        MerchantCity = merchantCity;
        BrCode = brCode;
        QrCodeSvg = qrCodeSvg;
        CreatedAtUtc = DateTime.UtcNow;
        ExpiresAtUtc = CreatedAtUtc.Add(expiresIn ?? TimeSpan.FromDays(1));
    }

    public void MarkAsPaid(EndToEndId endToEndId, DateTime paidAtUtc)
    {
        if (Status == ChargeStatus.Completed)
            return; // Idempotente

        if (Status == ChargeStatus.Expired)
            throw new DomainException("Cobrança já se encontra expirada.");

        if (DateTime.UtcNow > ExpiresAtUtc)
        {
            Status = ChargeStatus.Expired;
            throw new DomainException("Cobrança expirou antes do pagamento.");
        }

        Status = ChargeStatus.Completed;
        EndToEndId = endToEndId.Value;
        PaidAtUtc = paidAtUtc;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Expire()
    {
        if (Status == ChargeStatus.Active)
        {
            Status = ChargeStatus.Expired;
            UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}

public class LedgerAccount : BaseEntity, IAggregateRoot
{
    public string AccountNumber { get; private set; }
    public AccountType Type { get; private set; }
    public decimal Balance { get; private set; }
    public string Currency { get; private set; }
    public uint Version { get; private set; }

    protected LedgerAccount()
    {
        AccountNumber = null!;
        Currency = "BRL";
    }

    public LedgerAccount(Guid id, string accountNumber, AccountType type, decimal initialBalance = 0, string currency = "BRL")
    {
        Id = id;
        AccountNumber = accountNumber;
        Type = type;
        Balance = initialBalance;
        Currency = currency;
        Version = 1;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Credit(decimal amount)
    {
        if (amount <= 0)
            throw new DomainException("Valor de crédito no ledger deve ser positivo.");

        Balance += amount;
        Version++;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Debit(decimal amount, bool? allowOverdraft = null)
    {
        if (amount <= 0)
            throw new DomainException("Valor de débito no ledger deve ser positivo.");

        var canOverdraft = allowOverdraft ?? (Type == AccountType.PixSettlementTransit);

        if (!canOverdraft && Balance < amount)
            throw new InsufficientFundsException($"Saldo insuficiente na conta {AccountNumber}. Saldo: {Balance:C}, Débito solicitado: {amount:C}.");

        Balance -= amount;
        Version++;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UnsafeDirectDebit(decimal amount)
    {
        Balance -= amount;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

public class LedgerEntry : BaseEntity
{
    public Guid TransactionId { get; private set; }
    public Guid AccountId { get; private set; }
    public EntryType Type { get; private set; }
    public decimal Amount { get; private set; }
    public string Description { get; private set; }

    protected LedgerEntry()
    {
        Description = null!;
    }

    public LedgerEntry(Guid transactionId, Guid accountId, EntryType type, decimal amount, string description)
    {
        if (amount <= 0)
            throw new DomainException("Valor do lançamento no ledger deve ser positivo.");

        Id = Guid.NewGuid();
        TransactionId = transactionId;
        AccountId = accountId;
        Type = type;
        Amount = amount;
        Description = description;
        CreatedAtUtc = DateTime.UtcNow;
    }
}

public class LedgerTransaction : BaseEntity, IAggregateRoot
{
    private readonly List<LedgerEntry> _entries = new();

    public string ReferenceType { get; private set; }
    public string ReferenceId { get; private set; }
    public IReadOnlyCollection<LedgerEntry> Entries => _entries.AsReadOnly();

    protected LedgerTransaction()
    {
        ReferenceType = null!;
        ReferenceId = null!;
    }

    public LedgerTransaction(Guid id, string referenceType, string referenceId)
    {
        Id = id;
        ReferenceType = referenceType;
        ReferenceId = referenceId;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void AddEntry(LedgerEntry entry)
    {
        _entries.Add(entry);
    }

    public void AssertBalanceInvariant()
    {
        var totalDebits = _entries.Where(e => e.Type == EntryType.Debit).Sum(e => e.Amount);
        var totalCredits = _entries.Where(e => e.Type == EntryType.Credit).Sum(e => e.Amount);

        if (totalDebits != totalCredits)
        {
            throw new DomainException($"Invariante do Ledger quebrada! Débitos ({totalDebits:F2}) != Créditos ({totalCredits:F2}).");
        }
    }
}

public class OutboxMessage : BaseEntity, IAggregateRoot
{
    public string EventType { get; private set; }
    public string PayloadJson { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public int RetryCount { get; private set; }
    public string? LastError { get; private set; }

    protected OutboxMessage()
    {
        EventType = null!;
        PayloadJson = null!;
    }

    public OutboxMessage(string eventType, string payloadJson)
    {
        Id = Guid.NewGuid();
        EventType = eventType;
        PayloadJson = payloadJson;
        CreatedAtUtc = DateTime.UtcNow;
        RetryCount = 0;
    }

    public void MarkAsProcessed()
    {
        ProcessedAtUtc = DateTime.UtcNow;
        LastError = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void RecordFailure(string error)
    {
        RetryCount++;
        LastError = error;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

public class WebhookSubscription : BaseEntity, IAggregateRoot
{
    public string MerchantId { get; private set; }
    public string TargetUrl { get; private set; }
    public string SecretKey { get; private set; }
    public bool IsActive { get; private set; }

    protected WebhookSubscription()
    {
        MerchantId = null!;
        TargetUrl = null!;
        SecretKey = null!;
    }

    public WebhookSubscription(string merchantId, string targetUrl, string? secretKey = null)
    {
        Id = Guid.NewGuid();
        MerchantId = merchantId;
        TargetUrl = targetUrl;
        SecretKey = secretKey ?? Guid.NewGuid().ToString("N");
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

public class WebhookDelivery : BaseEntity, IAggregateRoot
{
    public Guid SubscriptionId { get; private set; }
    public string TargetUrl { get; private set; }
    public string EventType { get; private set; }
    public string PayloadJson { get; private set; }
    public string Signature { get; private set; }
    public WebhookDeliveryStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime? NextRetryAtUtc { get; private set; }
    public int? LastHttpStatus { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? DeliveredAtUtc { get; private set; }

    protected WebhookDelivery()
    {
        TargetUrl = null!;
        EventType = null!;
        PayloadJson = null!;
        Signature = null!;
    }

    public WebhookDelivery(
        Guid subscriptionId,
        string targetUrl,
        string eventType,
        string payloadJson,
        string signature)
    {
        Id = Guid.NewGuid();
        SubscriptionId = subscriptionId;
        TargetUrl = targetUrl;
        EventType = eventType;
        PayloadJson = payloadJson;
        Signature = signature;
        Status = WebhookDeliveryStatus.Pending;
        AttemptCount = 0;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void RecordSuccess(int httpStatus)
    {
        AttemptCount++;
        LastHttpStatus = httpStatus;
        LastError = null;
        Status = WebhookDeliveryStatus.Delivered;
        DeliveredAtUtc = DateTime.UtcNow;
        NextRetryAtUtc = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void RecordFailure(int? httpStatus, string error, int maxRetries = 5)
    {
        AttemptCount++;
        LastHttpStatus = httpStatus;
        LastError = error;
        UpdatedAtUtc = DateTime.UtcNow;

        if (AttemptCount >= maxRetries)
        {
            Status = WebhookDeliveryStatus.DeadLettered;
            NextRetryAtUtc = null;
        }
        else
        {
            Status = WebhookDeliveryStatus.Failed;
            var delaySeconds = Math.Pow(2, AttemptCount - 1) * 2;
            NextRetryAtUtc = DateTime.UtcNow.AddSeconds(delaySeconds);
        }
    }
}

public class IdempotencyRecord
{
    public string Key { get; private set; }
    public string RequestHash { get; private set; }
    public string Route { get; private set; }
    public IdempotencyStatus Status { get; private set; }
    public int? StatusCode { get; private set; }
    public string? ResponseBody { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LockedUntilUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    protected IdempotencyRecord()
    {
        Key = null!;
        RequestHash = null!;
        Route = null!;
    }

    public IdempotencyRecord(string key, string requestHash, string route, TimeSpan? lockDuration = null)
    {
        Key = key;
        RequestHash = requestHash;
        Route = route;
        Status = IdempotencyStatus.InProgress;
        CreatedAtUtc = DateTime.UtcNow;
        LockedUntilUtc = CreatedAtUtc.Add(lockDuration ?? TimeSpan.FromSeconds(30));
    }

    public void Complete(int statusCode, string responseBody)
    {
        Status = IdempotencyStatus.Completed;
        StatusCode = statusCode;
        ResponseBody = responseBody;
        CompletedAtUtc = DateTime.UtcNow;
        LockedUntilUtc = null;
    }

    public void Fail(int statusCode, string responseBody)
    {
        Status = IdempotencyStatus.Failed;
        StatusCode = statusCode;
        ResponseBody = responseBody;
        CompletedAtUtc = DateTime.UtcNow;
        LockedUntilUtc = null;
    }

    public bool HasLockExpired() => LockedUntilUtc.HasValue && DateTime.UtcNow > LockedUntilUtc.Value;
}
