# Modelo C4 (C4 Model) de Arquitetura

O modelo C4 descreve a arquitetura do **Mini-PSP Pix Simulado** em quatro níveis progressivos de abstração: Contexto, Contêineres, Componentes e Implantação.

---

## 🌐 Nível 1: Diagrama de Contexto (System Context)

Apresenta como os usuários (Comerciantes e E-commerces) e sistemas externos (Câmara SPI Bacen e Endpoints de Webhook) interagem com o Mini-PSP Pix.

```mermaid
flowchart TD
    User["👤 Comerciante / Sistema de E-commerce\n(Cliente do PSP)"]
    PSP["🏦 Mini-PSP Pix Simulado\n(Sistema de Liquidação e Pagamentos Pix)"]
    BacenSPI["🏛️ Câmara SPI / Banco Central\n(Provedor Bancário Simulado)"]
    WebhookReceiver["💻 Servidor de Webhook do Comerciante\n(Sistema Externo Receptor)"]
    Observability["📊 Plataforma de Observabilidade\n(Prometheus / Grafana / Jaeger)"]

    User -->|Emite cobrança, consulta saldo e solicita payout via API REST| PSP
    PSP -->|Registra cobrança, envia ordem de payout e recebe liquidação| BacenSPI
    PSP -->|Despacha notificações de liquidação assinadas com HMAC| WebhookReceiver
    PSP -.->|Exporta métricas de negócio e traces distribuídos| Observability

    style PSP fill:#2b6cb0,stroke:#2d3748,stroke-width:2px,color:#fff
    style BacenSPI fill:#744210,stroke:#d69e2e,stroke-width:2px,color:#fff
    style WebhookReceiver fill:#2c7a7b,stroke:#319795,stroke-width:2px,color:#fff
```

---

## 📦 Nível 2: Diagrama de Contêineres (Container Diagram)

Apresenta as aplicações executáveis, bancos de dados e sistemas de mensageria que compõem o ecossistema.

```mermaid
flowchart TD
    Client["🌐 Cliente HTTP / Testes k6 / SDK"]

    subgraph MiniPspSystem ["Ecossistema Mini-PSP Pix (Docker Compose)"]
        API["🚀 MiniPspPix.API (.NET 10 ASP.NET Core)\n- Controllers REST /api/v1/...\n- RFC 9444 Idempotency Middleware\n- OpenTelemetry Metrics & Tracing"]
        
        Workers["⚙️ Background Workers\n- OutboxPublisherWorker\n- WebhookDeliveryWorker"]
        
        DB[("🗄️ PostgreSQL 16\n- PixCharges\n- LedgerAccounts\n- LedgerTransactions\n- OutboxMessages\n- IdempotencyRecords\n- WebhookDeliveries")]
        
        Broker["📨 RabbitMQ Broker (AMQP)\n- Topic Exchange: pix.events\n- Fila: pix.webhook.delivery\n- Fila Morta: pix.webhook.dlq"]
        
        OTel["📡 OpenTelemetry Collector\nRecebe OTLP via porta 4317"]
        Prom["📈 Prometheus\nScrape de métricas na porta 9090"]
        Graf["📊 Grafana\nDashboards na porta 3000"]
        Jaeg["🔍 Jaeger\nTracing distribuído na porta 16686"]
    end

    Client -->|HTTPS/HTTP Chamadas REST| API
    API -->|Leitura e escrita transacional ACID| DB
    Workers -->|Polling de lotes pendentes no Outbox| DB
    Workers -->|Publicação de eventos de domínio| Broker
    API -.->|Exporta Traces OTLP| OTel
    API -.->|Expõe /metrics| Prom
    OTel -->|Encaminha métricas| Prom
    OTel -->|Encaminha spans| Jaeg
    Prom -->|Consulta de métricas| Graf
```

---

## 🧩 Nível 3: Diagrama de Componentes (Component Diagram - API & Application)

Detalha os blocos internos presentes na solução .NET e como as requisições fluem através dos casos de uso e serviços de domínio.

```mermaid
flowchart TD
    subgraph Presentation ["Camada de Apresentação (MiniPspPix.API)"]
        IdempMw["IdempotencyMiddleware\n(RFC 9444)"]
        ChargesCtrl["ChargesController\n(/api/v1/charges)"]
        PaymentsCtrl["PaymentsController\n(/api/v1/payments/simulation)"]
        PayoutCtrl["PayoutController\n(/api/v1/payout)"]
        LedgerCtrl["LedgerController\n(/api/v1/ledger)"]
        ExHandler["ExceptionHandler\n(ProblemDetails RFC 7807)"]
        Metrics["PixMetrics\n(System.Diagnostics.Metrics)"]
    end

    subgraph ApplicationLayer ["Camada de Aplicação (Application)"]
        CreateCobUC["CreatePixChargeUseCase"]
        PaySimUC["ProcessInboundPaymentUseCase"]
        PayoutUC["RequestPayoutUseCase"]
        LedgerUC["GetAccountBalanceUseCase\nGetAccountStatementUseCase"]
        WebhookUC["RegisterWebhookUseCase\nDispatchWebhookUseCase"]
    end

    subgraph DomainAndInfra ["Domínio e Infraestrutura"]
        BrCodeSvc["BrCodeService\n(EMV TLV & CRC16-CCITT)"]
        HmacSvc["HmacSignerService\n(HMAC-SHA256)"]
        BankProv["SimulatedBankingProvider\n(Chaos Injection SPI)"]
        LedgerRepo["LedgerRepository\n(Double-Entry & RowVersion)"]
        ChargeRepo["ChargeRepository"]
        OutboxRepo["OutboxRepository"]
        IdempStore["IdempotencyStore"]
    end

    IdempMw --> IdempStore
    IdempMw --> ChargesCtrl
    IdempMw --> PayoutCtrl

    ChargesCtrl --> CreateCobUC
    PaymentsCtrl --> PaySimUC
    PayoutCtrl --> PayoutUC
    LedgerCtrl --> LedgerUC

    CreateCobUC --> BrCodeSvc
    CreateCobUC --> ChargeRepo
    CreateCobUC --> OutboxRepo

    PaySimUC --> BankProv
    PaySimUC --> ChargeRepo
    PaySimUC --> LedgerRepo
    PaySimUC --> OutboxRepo

    PayoutUC --> LedgerRepo
    PayoutUC --> BankProv
    PayoutUC --> OutboxRepo

    ChargesCtrl -.-> Metrics
    PaymentsCtrl -.-> Metrics
    PayoutCtrl -.-> Metrics
    ExHandler -.-> Metrics
```

---

## 🚀 Nível 4: Diagrama de Implantação (Deployment Diagram)

Apresenta a topologia de nós e contêineres gerenciados pelo `docker-compose.yml`:

```mermaid
flowchart LR
    subgraph Host ["Máquina Host / Ambiente de Execução"]
        subgraph DockerBridge ["Rede Virtual Docker (Bridge)"]
            subgraph ContAPI ["Contêiner: minipsp-api"]
                NetApp[".NET 10 Web API\nURL: http://localhost:5000"]
            end

            subgraph ContPG ["Contêiner: minipsp-postgres"]
                PGServer["PostgreSQL 16 Alpine\nPorta: 5432\nVolume: postgres-data"]
            end

            subgraph ContRMQ ["Contêiner: minipsp-rabbitmq"]
                RMQServer["RabbitMQ 3.13 Management\nPortas: 5672 (AMQP), 15672 (UI)"]
            end

            subgraph ContOTel ["Contêiner: minipsp-otel-collector"]
                OTelCollector["OTel Collector Contrib\nPortas: 4317 (gRPC), 4318 (HTTP)"]
            end

            subgraph ContProm ["Contêiner: minipsp-prometheus"]
                PromServer["Prometheus Server\nPorta: 9090"]
            end

            subgraph ContGraf ["Contêiner: minipsp-grafana"]
                GrafServer["Grafana Dashboard\nPorta: 3000"]
            end

            subgraph ContJaeg ["Contêiner: minipsp-jaeger"]
                JaegServer["Jaeger All-in-One\nPorta: 16686"]
            end
        end
    end

    NetApp -->|Porta 5432| PGServer
    NetApp -->|Porta 5672| RMQServer
    NetApp -->|Porta 4317| OTelCollector
    PromServer -->|Scrape /metrics:5000| NetApp
    OTelCollector -->|Exporta spans| JaegServer
    OTelCollector -->|Exporta métricas| PromServer
    GrafServer -->|Consulta métricas:9090| PromServer
```

