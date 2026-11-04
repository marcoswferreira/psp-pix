# Runbook Operacional & Guia de Implantação

Este runbook reúne todos os procedimentos operacionais para compilação, execução da suíte de testes, auditoria de cobertura de código, inicialização via Docker Compose e solução de problemas (*troubleshooting*).

---

## 🛠️ Pré-requisitos do Ambiente

* **.NET 10 SDK** (`net10.0`)
* **Docker** e **Docker Compose**
* **Git**
* *(Opcional)* **k6** para execução de testes de estresse

---

## 💻 Desenvolvimento Local & Bateria de Testes

### 1. Compilação da Solução Completa

```bash
export XDG_RUNTIME_DIR=/tmp
dotnet build MiniPspPix.sln
```

### 2. Execução dos 84 Testes Automatizados

Toda a suíte de testes unitários, de concorrência e integração roda de forma **100% isolada e autônoma**, sem depender de instâncias externas de banco ou mensageria:

```bash
dotnet test
```

### 3. Coleta e Verificação da Cobertura de Código ($\ge 80\%$)

A solução utiliza o coletor `coverlet.collector` associado ao arquivo de configuração `coverlet.runsettings`:

```bash
dotnet test --settings coverlet.runsettings --collect:"XPlat Code Coverage"
```

Os arquivos de relatório são gerados em formato Cobertura XML dentro de `tests/*/TestResults/*/coverage.cobertura.xml`.

Para inspecionar o percentual consolidado via terminal:

```bash
python3 -c "
import xml.etree.ElementTree as ET, glob
files = sorted(glob.glob('tests/**/coverage.cobertura.xml', recursive=True), key=lambda x: -__import__('os').path.getmtime(x))[:3]
lines = {}
for f in files:
    root = ET.parse(f).getroot()
    for pkg in root.findall('.//package'):
        name = pkg.attrib.get('name')
        if not name or name.endswith('Tests'): continue
        for cls in pkg.findall('.//class'):
            cls_name = cls.attrib.get('name', '')
            if 'Microsoft.AspNetCore.OpenApi' in cls_name or 'Generated' in cls_name: continue
            for l in cls.findall('.//line'):
                key = (cls.attrib.get('filename'), l.attrib.get('number'))
                lines[key] = lines.get(key, 0) + int(l.attrib.get('hits', 0))
covered = sum(1 for hits in lines.values() if hits > 0)
total = len(lines)
print(f'COBERTURA CONSOLIDADA: {covered}/{total} ({covered/total*100:.2f}%)')
"
```

> **Métrica Atual**: **81.49%** de cobertura global.

---

## 🐳 Execução com Docker Compose

O arquivo `docker-compose.yml` orquestra a stack completa de microsserviços e observabilidade:

```bash
docker compose up -d --build
```

### Serviços e Portas Mapeadas

| Serviço | Contêiner | Porta | Descrição |
| :--- | :--- | :--- | :--- |
| **API REST** | `minipsp-api` | `5000` | Ponto de entrada HTTP do Mini-PSP e métricas em `/metrics` |
| **PostgreSQL** | `minipsp-postgres` | `5432` | Banco relacional para o Ledger, Outbox e Cobranças |
| **RabbitMQ** | `minipsp-rabbitmq` | `5672` | Broker AMQP para publicação de eventos via Outbox |
| **RabbitMQ UI** | `minipsp-rabbitmq` | `15672` | Painel web administrativo (`guest` / `guest`) |
| **Prometheus** | `minipsp-prometheus` | `9090` | Servidor de métricas e scraping a cada 5s |
| **Grafana** | `minipsp-grafana` | `3000` | Dashboards pré-provisionados (`admin` / `admin`) |
| **Jaeger UI** | `minipsp-jaeger` | `16686` | Interface visual de tracing distribuído |
| **OTel Collector** | `minipsp-otel-collector` | `4317` | Receptor gRPC OpenTelemetry |

### Verificação do Status dos Serviços

```bash
docker compose ps
```

### Acompanhar Logs da API em Tempo Real

```bash
docker compose logs -f api
```

---

## 🗄️ Inicialização e Seed do Banco de Dados

Ao iniciar a aplicação (`Program.cs`), o método assíncrono `app.MigrateDatabaseAsync()` é executado:

1. Se conectado a um banco relacional (PostgreSQL), aplica todas as migrações pendentes (`await context.Database.MigrateAsync()`).
2. Se executando com provedor em memória (desenvolvimento/testes), assegura a criação do schema (`await context.Database.EnsureCreatedAsync()`).
3. Executa o `DatabaseSeeder.SeedAsync(...)`, inserindo as duas contas essenciais caso a tabela esteja vazia:
   * **Conta Transitória Bacen (`PixSettlementTransit`)**:
     * ID: `11111111-1111-1111-1111-111111111111`
     * Número: `TRANSIT-BACEN-001`
     * Saldo Inicial: `R$ 0,00`
   * **Conta do Comerciante Padrão (`Merchant`)**:
     * ID: `22222222-2222-2222-2222-222222222222`
     * Número: `MERCHANT-001`
     * Saldo Inicial: `R$ 1.000,00`

---

## 🔧 Solução de Problemas (Troubleshooting)

### 1. "RabbitMQ connection could not be established"

* **Causa**: O contêiner do RabbitMQ ainda está subindo ou a porta 5672 está bloqueada.
* **Comportamento Resiliente**: O `RabbitMqEventPublisher` possui um modo de fallback elegante com captura de exceções. Caso o RabbitMQ não responda, a aplicação registra o evento no log sem travar e as mensagens do Outbox aguardam no banco para a próxima retentativa.

### 2. "Conflict: A request with this Idempotency-Key is currently in-flight (409)"

* **Causa**: Uma requisição paralela com a mesma `Idempotency-Key` foi enviada antes da primeira ter concluído.
* **Ação**: O cliente deve aguardar a conclusão da primeira chamada e repetir com a mesma chave para obter a resposta cacheada (`Idempotent-Replay: true`).

### 3. "Saldo insuficiente na conta MERCHANT-001 (422)"

* **Causa**: Tentativa de saque (`POST /api/v1/payout`) com valor superior ao saldo disponível no Ledger.
* **Ação**: Realize uma simulação de pagamento inbound (`POST /api/v1/payments/simulation`) para creditar saldo na conta antes de tentar novo saque.
