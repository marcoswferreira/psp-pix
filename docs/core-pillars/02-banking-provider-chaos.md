# Pilar 2: Provedor Bancário Simulado & Engenharia de Caos

Em sistemas de pagamento reais, a comunicação com as câmaras de liquidação do Banco Central (SPI/Dict) e bancos liquidantes está sujeita a falhas transitórias, quedas de conexão, latências imprevisíveis e notificações duplicadas.

Para viabilizar testes de resiliência e demonstrar como o sistema reage a falhas graves sem depender de sandboxes bancárias reais instáveis, foi projetada a abstração `IBankingProvider` com o adaptador `SimulatedBankingProvider`.

---

## 🏛️ Contrato `IBankingProvider`

A interface expõe os 3 fluxos essenciais de um PSP conectado ao arranjo Pix:

```csharp
public interface IBankingProvider
{
    // Registra emissão da cobrança na câmara/participante liquidante
    Task<BankChargeResult> RegisterChargeAsync(BankChargeRequest request, CancellationToken ct = default);

    // Envia ordem de liquidação de saída (Payout / Saque para chave de destino)
    Task<BankPayoutResult> SendPayoutAsync(BankPayoutRequest request, CancellationToken ct = default);

    // Simula a notificação inbound enviada pelo Bacen quando o pagador efetua a transferência
    Task<SimulatedInboundPaymentResult> SimulateInboundPaymentAsync(SimulatedInboundPaymentRequest request, CancellationToken ct = default);
}
```

---

## ⚡ Modos de Caos (`ChaosOptions`)

Todas as requisições para o provedor aceitam um objeto opcional `ChaosOptions`:

```csharp
public record ChaosOptions(
    ChaosAction Action = ChaosAction.None,
    int DelayMilliseconds = 0,
    string? FailureReason = null,
    string? BankErrorCode = null
);
```

### 1. `ChaosAction.None` (Caminho Feliz)
* Opera de forma determinística em menos de 1ms.
* Gera identificadores bancários realistas (`BANK-TX-...`, `PAYOUT-SPI-...`) e o `EndToEndId` conforme especificação Bacen.

### 2. `ChaosAction.Delay` (Latência Artificial)
* Aplica `Task.Delay(DelayMilliseconds)` antes de responder.
* **Cenário de Teste**: Avaliar timeouts de gateways de pagamento clientes, comportamento de filas e degradação de p95/p99 em testes de carga k6.

### 3. `ChaosAction.Timeout` (Quebra de SLA SPI)
* Lança imediatamente uma `System.TimeoutException`:
  > *"Timeout bancário simulado: a câmara SPI não respondeu dentro do SLA de 1000ms."*
* **Cenário de Teste**: Comprovar que o PSP aborta a operação de forma segura, sem debitar a conta do lojista caso a confirmação do SPI não seja recebida.

### 4. `ChaosAction.Fail` (Rejeição Regulatória)
* Retorna `Success: false` preenchendo o código de erro oficial do Bacen:
  * `AB03`: Chave Pix de destino inexistente ou conta encerrada.
  * `CH01`: Rejeição na emissão da cobrança por indisponibilidade do DICT.
  * `DS04`: Transação bloqueada preventivamente pelo mecanismo especial de devolução (MED).
* **Cenário de Teste**: Testar reversão de saldos no Ledger e respostas `422 Unprocessable Entity` padronizadas para o cliente.

### 5. `ChaosAction.Duplicate` (Notificação Duplicada)
* Simula a entrega quase simultânea de duas notificações de liquidação bancária com o mesmo `TxId` e `EndToEndId`.
* **Cenário de Teste**: Provar que o PSP absorve a segunda notificação de forma totalmente idempotente, sem duplicar o crédito de saldo no comerciante e sem gerar inconsistência contábil no Ledger.

---

## 🧪 Exemplo de Uso na API

Para testar a resiliência do sistema durante uma simulação de liquidação inbound, basta enviar o bloco de caos no payload:

```json
POST /api/v1/payments/simulation
{
  "txId": "TX1234567890123456",
  "amount": 250.00,
  "chaos": {
    "action": 4, // 1=Delay, 2=Timeout, 3=Fail, 4=Duplicate
    "delayMilliseconds": 0
  }
}
```

A API absorve a duplicata, processa o primeiro crédito no Ledger e devolve na segunda mensagem:
```json
{
  "value": {
    "success": true,
    "message": "Cobrança já havia sido liquidada anteriormente (Replay Idempotente)."
  },
  "isSuccess": true,
  "errors": []
}
```
