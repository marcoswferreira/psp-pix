# 📚 Documentação Técnica — Mini-PSP Pix Simulado

Bem-vindo à documentação técnica do **Mini-PSP Pix Simulado**, uma plataforma de serviços de pagamentos (Payment Service Provider - PSP) construída em **.NET 10** sob os preceitos de **Clean Architecture**, **Domain-Driven Design (DDD)** e conformidade estrita com as regulamentações do **Banco Central do Brasil (Bacen)** e padrões da **IETF**.

Este repositório foi construído para servir como demonstração prática e verificável de engenharia de software para sistemas de missão crítica, alta concorrência e conformidade regulatória.

---

## 🗺️ Índice de Navegação

### 1. Arquitetura e Engenharia de Software
* [Visão Geral da Clean Architecture & DDD](architecture/clean-architecture.md) — Camadas, inversão de dependência, agregação e entidades.
* [Diagramas C4 Model](architecture/c4-model.md) — Diagramas de Contexto, Container, Componente e Implantação (Mermaid).

### 2. Os 7 Pilares Técnicos
1. [Cobrança Imediata & BR Code EMVCo](core-pillars/01-brcode-emv.md) — Padrão EMV, tags TLV, algoritmo CRC16-CCITT puro e renderização SVG.
2. [Provedor Bancário Simulado & Engenharia de Caos](core-pillars/02-banking-provider-chaos.md) — Interface `IBankingProvider`, injeção de atrasos, timeouts, falhas simuladas e notificações duplicadas.
3. [Idempotência Rigorosa (RFC 9444)](core-pillars/03-idempotency-rfc9444.md) — Hash canônico SHA-256, detecção de conflitos (422), bloqueio de concorrência (409) e replays em cache.
4. [Ledger de Dupla Entrada & Concorrência](core-pillars/04-double-entry-ledger.md) — Princípio de equilíbrio ($\sum \text{Débitos} = \sum \text{Créditos}$), teste de corrida de gasto duplo (*Double-Spending*) e correção via Controle Otimista (`RowVersion`).
5. [Transactional Outbox Pattern & RabbitMQ](core-pillars/05-transactional-outbox.md) — Prevenção do problema de escrita dupla (*dual-write*), polling em background e mensageria assíncrona.
6. [Webhooks com Assinatura HMAC-SHA256 & DLQ](core-pillars/06-webhooks-hmac-dlq.md) — Assinatura digital no cabeçalho `X-Signature`, tolerância temporal, retentativas com backoff exponencial e Dead Letter Queue.
7. [Observabilidade Completa & Testes de Carga com k6](core-pillars/07-observability-load-testing.md) — OpenTelemetry Collector, métricas Prometheus (`PixMetrics`), traces Jaeger, dashboards Grafana e benchmarks k6.

### 3. API e Contratos
* [Guia e Referência da API REST](api/api-contracts.md) — Endpoints versionados (`/api/v1/...`), monad `ResultGeneric<T>`, cabeçalhos obrigatórios e tratamento via `ProblemDetails` (RFC 7807).

### 4. Operações e Execução
* [Runbook & Guia Operacional](runbook/operations-and-deploy.md) — Execução com Docker Compose, migrações de banco, comandos de cobertura de testes ($\ge 80\%$) e troubleshooting.

---

## 🎯 Destaques do Projeto

| Característica | Detalhes |
| :--- | :--- |
| **Framework Base** | .NET 10 (`net10.0`) C# 13 |
| **Padrão Arquitetural** | Clean Architecture / DDD seguindo convenções do [18SOAT_TechChallenge](https://github.com/marcoswferreira/18SOAT_TechChallenge) |
| **Banco de Dados** | PostgreSQL 16 (produção/container) & Entity Framework Core In-Memory (testes isolados) |
| **Mensageria & Filas** | RabbitMQ 3.13 (AMQP 0-9-1) |
| **Pilha de Observabilidade** | OpenTelemetry, Prometheus, Jaeger e Grafana |
| **Bateria de Testes** | 84 testes automatizados cobrindo Domínio, Casos de Uso, Concorrência e Integração E2E |
| **Cobertura de Código** | **82.99%** consolidada via Coverlet |
