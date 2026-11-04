# Pilar 4: Ledger de Dupla Entrada & Concorrência (Double-Spending)

Em sistemas financeiros, a integridade do saldo dos clientes não pode depender de operações simples de incremento ou decremento soltas em colunas de banco de dados (`UPDATE accounts SET balance = balance - 100`).

O **Mini-PSP Pix Simulado** implementa um **Ledger Contábil de Dupla Entrada (*Double-Entry Bookkeeping*)**, complementado por uma suíte de testes comparativa que expõe a vulnerabilidade de corrida de gasto duplo (*Double-Spending*) e comprova a eficácia da solução com **Controle Otimista de Concorrência (*Optimistic Concurrency Control - OCC*)**.

---

## ⚖️ Princípio das Partidas Dobradas (Double-Entry)

Cada movimentação financeira é representada por uma `LedgerTransaction` contendo no mínimo dois lançamentos (`LedgerEntry`):

* Pelo menos um **Débito** (`EntryType.Debit`).
* Pelo menos um **Crédito** (`EntryType.Credit`).
* **Invariante Matemática Imutável**:
  $$\sum \text{Valores a Débito} = \sum \text{Valores a Crédito}$$

Se uma transação violar essa invariante contábil, o método `AssertBalanceInvariant()` aborta a transação e impede a persistência no banco:

```csharp
public void AssertBalanceInvariant()
{
    var totalDebits = _entries.Where(e => e.Type == EntryType.Debit).Sum(e => e.Amount);
    var totalCredits = _entries.Where(e => e.Type == EntryType.Credit).Sum(e => e.Amount);

    if (totalDebits != totalCredits)
    {
        throw new DomainException($"Invariante do Ledger quebrada! Débitos ({totalDebits:F2}) != Créditos ({totalCredits:F2}).");
    }
}
```

### Contas do Sistema

1. **Conta Transitória de Liquidação Bacen (`PixSettlementTransit`)**:
   Representa a contraparte centralizada da câmara de liquidação instantânea (SPI). É uma conta de espelho (*clearing account*) que absorve o contra-lançamento de entradas e saídas.
2. **Conta do Comerciante (`Merchant`)**:
   Representa os fundos disponíveis do lojista. Não permite saldo negativo (`allowOverdraft = false`), lançando `InsufficientFundsException` caso o débito exceda os fundos disponíveis.

---

## ⚔️ A Condição de Corrida: Gasto Duplo (*Check-Then-Act*)

Considere o cenário clássico de um serviço financeiro ingênuo (`NaiveAccountService`):

1. **Check**: Lê o saldo atual do comerciante (`saldo = 100.00`).
2. **Janela de I/O / Latência de Rede**: Pequeno delay entre a validação de regras e a escrita no banco (25ms).
3. **Act**: Atualiza o saldo (`saldo = saldo - 100.00`).

Quando duas requisições paralelas chegam quase simultaneamente:

* A **Requisição 1** lê o saldo de R$ 100,00 e aprova a operação.
* A **Requisição 2** lê o mesmo saldo de R$ 100,00 **antes** da Requisição 1 terminar a escrita e também aprova.
* **Resultado Catastrófico**: O lojista saca **R$ 200,00** de uma conta que possuía apenas **R$ 100,00**!

### Demonstração Verificável no Teste

```csharp
[Fact]
public async Task WithoutConcurrencyControl_TwoParallelRequests_ShouldBothSpendSameBalance_CausingDoubleSpending()
{
    var service = new NaiveAccountService(initialBalance: 100.00m);

    var task1 = Task.Run(() => service.TryWithdrawAsync(100.00m));
    var task2 = Task.Run(() => service.TryWithdrawAsync(100.00m));

    var results = await Task.WhenAll(task1, task2);

    results[0].Should().BeTrue();
    results[1].Should().BeTrue();

    // Comprovado: 2 saques aprovados para apenas R$ 100 de saldo inicial!
    results.Count(r => r).Should().Be(2);
}
```

---

## 🛡️ A Solução Arquitetural: Controle Otimista (`RowVersion`)

Para sanar a vulnerabilidade sem a penalidade de gargalos de locks pessimistas de tabela inteira (`SELECT ... FOR UPDATE`), a entidade `LedgerAccount` adota uma versão de concorrência (`uint Version`):

```csharp
// Em LedgerAccount.cs
public uint Version { get; private set; }

public void Debit(decimal amount, bool? allowOverdraft = null)
{
    // ... validações ...
    Balance -= amount;
    Version++;
    UpdatedAtUtc = DateTime.UtcNow;
}
```

No mapeamento EF Core (`EntityMappings.cs`):

```csharp
builder.Property(a => a.Version)
    .IsRowVersion()
    .IsConcurrencyToken();
```

### Como Funciona

Ao executar o `SaveChangesAsync()`, o EF Core emite um comando condicional no banco de dados:

```sql
UPDATE "LedgerAccounts"
SET "Balance" = @newBalance, "Version" = @newVersion
WHERE "Id" = @id AND "Version" = @originalVersion;
```

Se outra requisição já tiver incrementado a versão no milissegundo anterior:

1. Nenhuma linha é afetada (`Rows Affected = 0`).
2. O EF Core detecta a quebra e lança uma `DbUpdateConcurrencyException`.
3. O repositório intercepta e lança uma `ConcurrencyException`.
4. O middleware global retorna `409 Conflict` ou `422 Unprocessable Entity` com `ProblemDetails`.

### Prova no Teste com 10 Requisições Paralelas

No teste `WithOptimisticConcurrencyControl_ParallelDebits_ShouldPreventDoubleSpending`:

* Saldo inicial: R$ 100,00.
* **10 requisições simultâneas** tentam debitar R$ 100,00 através de `RecordDoubleEntryTransferAsync`.
* **Resultado Comprovado**:
  * **Exatamente 1 requisição** é aprovada com sucesso.
  * **As outras 9 requisições** são rejeitadas por conflito de versão (`ConcurrencyException`) ou saldo insuficiente.
  * O saldo final da conta do comerciante termina rigorosamente em **R$ 0,00** (nunca negativo).
  * O balanço do Ledger encerra com $\sum \text{Débitos} = 100.00$ e $\sum \text{Créditos} = 100.00$.
