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

