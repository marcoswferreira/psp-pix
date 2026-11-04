using System.Collections.Concurrent;
using Domain.Common.Exceptions;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Database.DbContexts;
using Infrastructure.Database.Repositories;
using Microsoft.EntityFrameworkCore;

namespace IntegrationTests.Concurrency;

public class NaiveAccountService
{
    private decimal _balance;

    public NaiveAccountService(decimal initialBalance)
    {
        _balance = initialBalance;
    }

    public decimal GetBalance() => _balance;

    public async Task<bool> TryWithdrawAsync(decimal amount)
    {
        // 1. Check (leitura do saldo)
        var currentBalance = _balance;
        if (currentBalance < amount)
            return false;

        // 2. Latência simulada de rede / banco
        await Task.Delay(25);

        // 3. Act (atualização ingênua sem lock ou versionamento)
        _balance = currentBalance - amount;
        return true;
    }
}

public class RaceConditionDemonstrationTests
{
    [Fact]
    public async Task WithoutConcurrencyControl_TwoParallelRequests_ShouldBothSpendSameBalance_CausingDoubleSpending()
    {
        // Saldo disponível: R$ 100,00.
        // Duas requisições paralelas tentam sacar R$ 100,00 simultaneamente.
        var service = new NaiveAccountService(initialBalance: 100.00m);

        var task1 = Task.Run(() => service.TryWithdrawAsync(100.00m));
        var task2 = Task.Run(() => service.TryWithdrawAsync(100.00m));

        var results = await Task.WhenAll(task1, task2);

        // Ambas aprovaram porque leram o saldo antes de uma delas persistir a dedução
        results[0].Should().BeTrue("a requisição 1 viu saldo de R$ 100 e aprovou.");
        results[1].Should().BeTrue("a requisição 2 também viu saldo de R$ 100 antes da primeira debitar e aprovou.");

        var approvedCount = results.Count(r => r);
        approvedCount.Should().Be(2, because: "sem OCC (RowVersion) ocorre a corrida de gasto duplo.");
    }
}

public class ConcurrencyProtectionTests
{
    [Fact]
    public async Task WithOptimisticConcurrencyControl_ParallelDebits_ShouldPreventDoubleSpending()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var sourceAccountId = Guid.NewGuid();
        var destAccountId = Guid.NewGuid();

        using (var db = new ApplicationDbContext(options))
        {
            db.LedgerAccounts.AddRange(
                new LedgerAccount(sourceAccountId, "MERCHANT-SAFE-001", AccountType.Merchant, 100.00m),
                new LedgerAccount(destAccountId, "TRANSIT-SAFE-001", AccountType.PixSettlementTransit, 0m)
            );
            await db.SaveChangesAsync();
        }

        const int numberOfParallelRequests = 10;
        var successfulRequests = new ConcurrentBag<LedgerTransaction>();
        var failedRequests = new ConcurrentBag<Exception>();

        var tasks = Enumerable.Range(0, numberOfParallelRequests).Select(async _ =>
        {
            try
            {
                using var db = new ApplicationDbContext(options);
                var ledgerRepo = new LedgerRepository(db);
                var tx = await ledgerRepo.RecordDoubleEntryTransferAsync(
                    sourceAccountId,
                    destAccountId,
                    100.00m,
                    "PixPayout",
                    Guid.NewGuid().ToString(),
                    "Saque com OCC"
                );
                successfulRequests.Add(tx);
            }
            catch (Exception ex)
            {
                failedRequests.Add(ex);
            }
        });

        await Task.WhenAll(tasks);

        // Exatamente 1 saque de R$ 100 é aprovado
        successfulRequests.Count.Should().Be(1, because: "o controle otimista impede saque concorrente do mesmo saldo.");
        failedRequests.Count.Should().Be(9);
        failedRequests.Should().AllSatisfy(ex =>
            ex.Should().Match<Exception>(e => e is ConcurrencyException || e is InsufficientFundsException));

        using (var db = new ApplicationDbContext(options))
        {
            var finalSource = await db.LedgerAccounts.FindAsync(sourceAccountId);
            var finalDest = await db.LedgerAccounts.FindAsync(destAccountId);

            finalSource!.Balance.Should().Be(0.00m);
            finalDest!.Balance.Should().Be(100.00m);

            var totalDebits = await db.LedgerEntries.Where(e => e.Type == EntryType.Debit).SumAsync(e => e.Amount);
            var totalCredits = await db.LedgerEntries.Where(e => e.Type == EntryType.Credit).SumAsync(e => e.Amount);
            totalDebits.Should().Be(totalCredits);
            totalDebits.Should().Be(100.00m);
        }
    }

    [Fact]
    public async Task ParallelIdempotencyKey_ShouldAllowOnlyOneWinner()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        const string idempotencyKey = "key-parallel-concurrency-test";
        const string requestHash = "hash-123456";
        const int numberOfParallelRequests = 10;

        var acquiredResults = new ConcurrentBag<bool>();

        var tasks = Enumerable.Range(0, numberOfParallelRequests).Select(async _ =>
        {
            using var db = new ApplicationDbContext(options);
            var store = new IdempotencyStore(db);
            var acquired = await store.TryAcquireLockAsync(
                idempotencyKey,
                requestHash,
                "/api/v1/charges",
                TimeSpan.FromSeconds(30)
            );
            acquiredResults.Add(acquired);
        });

        await Task.WhenAll(tasks);

        acquiredResults.Count(a => a).Should().Be(1);
        acquiredResults.Count(a => !a).Should().Be(9);
    }
}

