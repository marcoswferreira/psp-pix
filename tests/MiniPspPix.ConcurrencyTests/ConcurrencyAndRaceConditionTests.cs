using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MiniPspPix.Application.Idempotency;
using MiniPspPix.Domain.Exceptions;
using MiniPspPix.Domain.Ledger;
using MiniPspPix.Infrastructure.Ledger;
using MiniPspPix.Infrastructure.Persistence;

namespace MiniPspPix.ConcurrencyTests;

/// <summary>
/// Simulação de um serviço financeiro ingênuo (sem controle de versão ou locks)
/// que sofre da vulnerabilidade clássica de Check-Then-Act / Race Condition.
/// </summary>
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
        // 1. Leitura do saldo (Check)
        var currentBalance = _balance;
        if (currentBalance < amount)
            return false;

        // 2. Latência de I/O ou comunicação externa (janela onde a corrida ocorre)
        await Task.Delay(25);

        // 3. Atualização ingênua (Act) - Lost Update / Race Condition
        _balance = currentBalance - amount;
        return true;
    }
}

public class RaceConditionDemonstrationTests
{
    [Fact]
    public async Task WithoutConcurrencyControl_TwoParallelRequests_ShouldBothSpendSameBalance_CausingDoubleSpending()
    {
        // =========================================================================
        // CENÁRIO A: DEMONSTRAÇÃO DA CONDIÇÃO DE CORRIDA (BUG)
        // Saldo disponível: R$ 100,00.
        // Duas requisições concorrentes tentam gastar R$ 100,00 simultaneamente.
        // =========================================================================
        var service = new NaiveAccountService(initialBalance: 100.00m);

        // Dispara duas requisições paralelas
        var task1 = Task.Run(() => service.TryWithdrawAsync(100.00m));
        var task2 = Task.Run(() => service.TryWithdrawAsync(100.00m));

        var results = await Task.WhenAll(task1, task2);

        // Ambas as requisições verificaram o saldo de R$ 100,00 antes de qualquer uma debitar.
        // Ambas foram aprovadas (True), consumindo R$ 200,00 de uma conta que só tinha R$ 100,00!
        results[0].Should().BeTrue("a requisição 1 viu saldo de R$ 100 e aprovou.");
        results[1].Should().BeTrue("a requisição 2 também viu saldo de R$ 100 antes da liquidação da primeira e aprovou.");

        // O saldo final termina em R$ 0,00 mas R$ 200,00 foram retirados (Double-Spending)!
        var approvedCount = results.Count(r => r);
        approvedCount.Should().Be(2,
            because: "sem controle otimista (row version) ou lock, ocorre a condição de corrida onde duas requisições gastam o mesmo saldo.");
    }
}

public class ConcurrencyProtectionTests
{
    [Fact]
    public async Task WithOptimisticConcurrencyControl_ParallelDebits_ShouldPreventDoubleSpending()
    {
        // =========================================================================
        // CENÁRIO B: CORREÇÃO ARQUITETURAL COM CONTROLE OTIMISTA (ROW VERSION)
        // Saldo inicial: R$ 100,00.
        // 10 requisições simultâneas tentam gastar R$ 100,00 através de RecordDoubleEntryTransferAsync.
        // =========================================================================
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<PixDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var sourceAccountId = Guid.NewGuid();
        var destAccountId = Guid.NewGuid();

        using (var db = new PixDbContext(options))
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
                using var db = new PixDbContext(options);
                var ledgerService = new EfCoreLedgerService(db);
                var tx = await ledgerService.RecordDoubleEntryTransferAsync(
                    sourceAccountId,
                    destAccountId,
                    100.00m,
                    "PixPayout",
                    Guid.NewGuid().ToString(),
                    "Saque protegido com row version"
                );
                successfulRequests.Add(tx);
            }
            catch (Exception ex)
            {
                failedRequests.Add(ex);
            }
        });

        await Task.WhenAll(tasks);

        // 1. Exatamente 1 saque de R$ 100 deve ter sido aprovado.
        successfulRequests.Count.Should().Be(1,
            because: "o controle de versão de linha (RowVersion) impede que duas requisições gastem o mesmo saldo.");

        // 2. As outras 9 requisições são rejeitadas por conflito de concorrência ou saldo insuficiente.
        failedRequests.Count.Should().Be(9);
        failedRequests.Should().AllSatisfy(ex =>
            ex.Should().Match<Exception>(e => e is ConcurrencyException || e is InsufficientFundsException));

        using (var db = new PixDbContext(options))
        {
            var finalSource = await db.LedgerAccounts.FindAsync(sourceAccountId);
            var finalDest = await db.LedgerAccounts.FindAsync(destAccountId);

            // 3. O saldo final do cliente é rigorosamente R$ 0,00 (NUNCA negativo!).
            finalSource!.Balance.Should().Be(0.00m);
            finalDest!.Balance.Should().Be(100.00m);

            // 4. Balanço do Ledger: total de débitos == total de créditos
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
        var options = new DbContextOptionsBuilder<PixDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        const string idempotencyKey = "key-parallel-concurrency-test";
        const string requestHash = "hash-123456";
        const int numberOfParallelRequests = 10;

        var acquiredResults = new ConcurrentBag<bool>();

        var tasks = Enumerable.Range(0, numberOfParallelRequests).Select(async _ =>
        {
            using var db = new PixDbContext(options);
            var store = new EfCoreIdempotencyStore(db);
            var acquired = await store.TryAcquireLockAsync(
                idempotencyKey,
                requestHash,
                "/v1/pix/cob",
                TimeSpan.FromSeconds(30)
            );
            acquiredResults.Add(acquired);
        });

        await Task.WhenAll(tasks);

        // Exatamente 1 thread deve vencer a aquisição do lock
        acquiredResults.Count(a => a).Should().Be(1);
        acquiredResults.Count(a => !a).Should().Be(9);
    }
}
