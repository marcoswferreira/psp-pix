# Mini-PSP Pix Simulado

> **Um Payment Service Provider (PSP) Pix simulado, construído em .NET 10, adotando a arquitetura limpa, cobrindo os pilares críticos de engenharia de software para fintechs e sistemas de pagamentos de alta escala.**

Projetado para demonstrar domínio prático de:

- **BR Code EMV e CRC16** conforme especificação pública do Banco Central do Brasil (Bacen).
- **Provedor Bancário Simulado (`IBankingProvider`)** com injeção de caos (delays, timeouts, falhas e notificações duplicadas).
- **Idempotência Rigorosa (RFC 9444 / IETF)** com resposta repetível em cache e detecção de payloads conflitantes.
- **Ledger de Dupla Entrada (Double-Entry Ledger)** com **testes comparativos de concorrência** demonstrando o bug de *Double-Spending* e a correção via controle otimista (`RowVersion`).
- **Transactional Outbox Pattern** com publicação resiliente no RabbitMQ.
- **Webhooks Assinados com HMAC-SHA256**, backoff exponencial e Dead Letter Queue (DLQ).
- **Observabilidade Completa** (OpenTelemetry, Prometheus, Grafana, Jaeger) e **Teste de Carga com k6**.
- **Cobertura de Testes Superior a 80%** (82.99% medido via Coverlet com 84 testes passando).

---

## 📖 Documentação Técnica Completa

Para aprofundar em cada detalhe de engenharia, arquitetura e decisões de projeto, consulte nossa pasta de documentação detalhada:

- 📚 [**Índice Geral de Documentação**](docs/README.md)
- 🏛️ [**Clean Architecture & DDD**](docs/architecture/clean-architecture.md)
- 🗺️ [**Diagramas C4 Model (Contexto, Contêiner, Componente e Implantação)**](docs/architecture/c4-model.md)
- 🏷️ [**Pilar 1: Cobrança Imediata & BR Code EMVCo / CRC16**](docs/core-pillars/01-brcode-emv.md)
- ⚡ [**Pilar 2: Provedor Bancário Simulado & Engenharia de Caos**](docs/core-pillars/02-banking-provider-chaos.md)
- 🔄 [**Pilar 3: Idempotência Rigorosa (RFC 9444 / IETF)**](docs/core-pillars/03-idempotency-rfc9444.md)
- ⚖️ [**Pilar 4: Ledger de Dupla Entrada & Concorrência (Double-Spending)**](docs/core-pillars/04-double-entry-ledger.md)
- 📦 [**Pilar 5: Transactional Outbox Pattern & RabbitMQ**](docs/core-pillars/05-transactional-outbox.md)
- 🔐 [**Pilar 6: Webhooks com Assinatura HMAC-SHA256 & DLQ**](docs/core-pillars/06-webhooks-hmac-dlq.md)
- 📊 [**Pilar 7: Observabilidade (OTel/Prometheus/Grafana) & k6**](docs/core-pillars/07-observability-load-testing.md)
- 📋 [**Referência de Contratos da API REST (v1.0)**](docs/api/api-contracts.md)
- 🛠️ [**Runbook Operacional & Guia de Implantação**](docs/runbook/operations-and-deploy.md)

---

## 🏛️ Arquitetura da Solução

O projeto segue estritamente o padrão Clean Architecture / DDD estabelecido no projeto [18SOAT_TechChallenge](https://github.com/marcoswferreira/18SOAT_TechChallenge):

```
mini-psp-pix-simulado/
├── MiniPspPix.sln
├── Dockerfile
├── docker-compose.yml
├── coverlet.runsettings
│
├── docs/                           # Documentação técnica completa (C4, Pilares, API, Runbook)
│
├── src/
│   ├── Domain/                     # Entidades puras, Value Objects, Enums e Interfaces
│   ├── Application/                # Casos de uso, DTOs e ResultGeneric<T> padrão
│   ├── Infrastructure/             # EF Core, Mappings, Repositórios, RabbitMQ, Workers e Provedor Simulado
│   └── MiniPspPix.API/             # Controllers versionados (api/v1/...), ExceptionHandler e OpenTelemetry
│
├── tests/
│   ├── Domain.UnitTests/           # 37 testes unitários (Entidades, Value Objects e Regras de Negócio)
│   ├── Application.UnitTests/      # 34 testes unitários com NSubstitute (Use Cases, BrCode, HMAC, Caos)
│   └── IntegrationTests/           # 13 testes de integração end-to-end com CustomWebApplicationFactory e Concorrência
│
├── load-tests/
│   ├── k6-charges-load.js          # Script de carga distribuída k6 contra rotas versionadas api/v1/charges
│   └── README.md                   # Instruções e métricas de benchmark
│
└── observability/
    ├── otel-collector-config.yaml  # Configuração do OTel Collector
    ├── prometheus.yml              # Scraping de métricas
    └── grafana/                    # Dashboards e datasources pré-provisionados
```

---

## ⚡ Quickstart em 2 Minutos

### 1. Rodar Todos os Testes Automatizados (Sem Dependências Externas)

Toda a suíte de testes unitários, de concorrência e de integração funciona **100% de forma isolada** usando provedores em memória:

```bash
dotnet test
```

### 2. Rodar a Cobertura de Código (Mínimo de 80% Garantido)

```bash
dotnet test --settings coverlet.runsettings --collect:"XPlat Code Coverage"
```

> **Resultado**: **82.99%** de cobertura consolidada (**100% na API**, **100% na Aplicação**, **96.3% na Infraestrutura**, **71.2% no Domínio**).

### 3. Subir o Ecossistema Completo com Docker Compose

PostgreSQL, RabbitMQ com Management, OpenTelemetry Collector, Prometheus, Jaeger, Grafana e API:

```bash
docker compose up -d
```

| Serviço | URL | Credenciais |
| :--- | :--- | :--- |
| **Mini-PSP API** | `http://localhost:5000` | - |
| **OpenAPI Spec** | `http://localhost:5000/openapi/v1.json` | - |
| **RabbitMQ Management** | `http://localhost:15672` | `guest` / `guest` |
| **Grafana Dashboards** | `http://localhost:3000` | `admin` / `admin` |
| **Prometheus Metrics** | `http://localhost:9090` | - |
| **Jaeger Traces UI** | `http://localhost:16686` | - |

---

## 🔍 Os 7 Pilares Demonstrados

### 1. Cobrança Imediata & BR Code EMV (Bacen Pix)

- Implementação estrita do manual de iniciação do Pix do Banco Central do Brasil.
- Tags EMVCo: `00` (Formato), `01` (Iniciação), `26` (Merchant Account com GUI `br.gov.bcb.pix`), `52` (MCC `0000`), `53` (Moeda `986` - BRL), `54` (Valor formatado), `58` (`BR`), `59` (Nome sanitizado), `60` (Cidade sanitizada), `62` (`TxId`) e `63` (`CRC16`).
- **Algoritmo CRC16-CCITT puro** (Polinômio `0x1021`, inicial `0xFFFF`), validado contra os vetores de teste públicos do Bacen (`D3E6`).
- Geração instantânea de SVG vetorial responsivo para renderização direta em navegadores.

### 2. Provedor Bancário Simulado (`IBankingProvider`) com Injeção de Caos

- Interface `IBankingProvider` com adaptador desacoplado `SimulatedBankingProvider`.
- Permite injetar comportamentos anômalos em tempo de execução via `ChaosOptions`:
  - `Delay`: Latência artificial para teste de resiliência e SLAs de clientes.
  - `Timeout`: Lança `TimeoutException` simulando interrupção de socket com a câmara SPI.
  - `Fail`: Simula códigos de erro do Bacen (ex: `AB03` para chave inexistente).
  - `Duplicate`: Dispara duas notificações de liquidação quase simultâneas para validar que o PSP absorve duplicatas sem duplicar créditos contábeis.

### 3. Idempotência Rigorosa (RFC 9444)

- Middleware intercepta o header `Idempotency-Key`.
- Gera hash determinístico SHA-256 com canonicidade de JSON (chaves ordenadas alfabeticamente).
- **Replay Idêntico**: Mesma chave e mesmo payload retorna o resultado gravado com o header `Idempotent-Replay: true`.
- **Detecção de Conflito**: Mesma chave reutilizada com payload alterado rejeita com `422 Unprocessable Entity`.
- **Proteção Concorrente**: Bloqueio com status `InProgress` para evitar processamento paralelo duplicado (`409 Conflict`).

### 4. Ledger de Dupla Entrada & Teste Comparativo de Concorrência

O princípio contábil bancário onde nenhuma quantia é criada ou destruída ($\sum \text{Débitos} = \sum \text{Créditos}$):

- Conta Transitória Bacen (`PixSettlementTransit`) e Saldo Disponível (`MerchantAccount`).
- **Suíte de Testes Comparativa Verificável**:
  - `WithoutConcurrencyControl_TwoParallelRequests_ShouldBothSpendSameBalance_CausingDoubleSpending`: Demonstra a condição de corrida em sistemas ingênuos, onde duas requisições paralelas gastam os mesmos R$ 100,00 de um saldo de R$ 100,00.
  - `WithOptimisticConcurrencyControl_ParallelDebits_ShouldPreventDoubleSpending`: Prova que com a correção via controle de versão de linha (`RowVersion`), 10 requisições simultâneas resultam em **exatamente 1 aprovação e 9 rejeições**, garantindo que o saldo final termine rigorosamente em **R$ 0,00** e o balanço do ledger feche em zero.

### 5. Transactional Outbox Pattern & Filas RabbitMQ

- Resolve o problema de escrita dupla (*dual-write problem*): a cobrança e o evento de domínio (`pix.charge.created`, `pix.payment.received`, `pix.payout.completed`) são persistidos na **mesma transação ACID** no PostgreSQL.
- O `OutboxPublisherWorker` (`BackgroundService`) lê os lotes em segundo plano, publica no RabbitMQ com **Publisher Confirms** e marca as mensagens como processadas.

### 6. Webhooks com Assinatura HMAC-SHA256, Retry Exponencial e DLQ

- Notificação de eventos para os lojistas cadastrados com cabeçalho de assinatura digital:
  `X-Signature: t={timestamp},v1={hmac_sha256}`
- Proteção contra *tampering* e *replay attacks* com janela de tolerância de 5 minutos.
- Em caso de falha do endpoint de destino, o `WebhookDeliveryWorker` executa retentativas com **backoff exponencial** ($2^0 \times 2s, 2^1 \times 2s, 2^2 \times 2s \dots$).
- Após 5 falhas consecutivas, a mensagem é encaminhada para a **Dead Letter Queue (`pix.webhook.dlq`)**.

### 7. Observabilidade e Teste de Carga (k6)

- Instrumentação nativa OpenTelemetry com métricas personalizadas:
  - `pix_charges_created_total`
  - `pix_payments_processed_total`
  - `pix_payouts_requested_total`
  - `pix_idempotent_replays_total`
  - `pix_idempotent_conflicts_total`
  - `pix_double_spending_attempts_total`
  - `pix_webhooks_dispatched_total`
- Script de estresse com k6 (`load-tests/k6-charges-load.js`) pronto para rodar:

  ```bash
  docker run --rm -i --net=host grafana/k6 run - < load-tests/k6-charges-load.js
  ```

---

## 📌 Exemplos de Uso da API

### Criar Cobrança Pix com BR Code

```bash
curl -X POST http://localhost:5000/api/v1/charges \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: cob-req-001" \
  -d '{
    "amount": 150.00,
    "pixKey": "12345678901",
    "description": "Pedido #1024",
    "merchantName": "LOJA VIRTUAL",
    "merchantCity": "SAO PAULO"
  }'
```

### Simular Liquidação Bancária (Inbound Pix)

```bash
curl -X POST http://localhost:5000/api/v1/payments/simulation \
  -H "Content-Type: application/json" \
  -d '{
    "txId": "SEU_TXID_AQUI",
    "amount": 150.00
  }'
```

### Solicitar Payout (Saque Pix)

```bash
curl -X POST http://localhost:5000/api/v1/payout \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: payout-req-001" \
  -d '{
    "merchantAccountId": "22222222-2222-2222-2222-222222222222",
    "amount": 50.00,
    "destinationPixKey": "fornecedor@pix.com",
    "description": "Pagamento a Fornecedor"
  }'
```

### Consultar Saldo no Ledger

```bash
curl http://localhost:5000/api/v1/ledger/accounts/22222222-2222-2222-2222-222222222222/balance
```

---

## 👨‍💻 Autor

Desenvolvido por **Marcos W. Ferreira** como projeto de referência arquitetural para sistemas de pagamentos de missão crítica.
