using FluentAssertions;

namespace IntegrationTests.Concurrency;

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

