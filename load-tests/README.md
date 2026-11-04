# Testes de Carga com k6 — Mini-PSP Pix

Este diretório contém os scripts de benchmark e teste de estresse distribuído utilizando o **Grafana k6**.

---

## 🎯 Cenário de Teste (`k6-charges-load.js`)

O teste simula o ciclo de vida completo de pagamentos Pix em alta concorrência:
1. **Emissão de Cobrança Pix Imediata (`POST /v1/pix/cob`)**:
   - Envio do header `Idempotency-Key` único.
   - Geração de BR Code EMVCo e cálculo de CRC16 em tempo real.
   - Geração de QR Code vetorial SVG.
2. **Validação de Replay Idempotente**:
   - Reenvio imediato da mesma requisição com a mesma chave.
   - Validação do retorno em cache (`Idempotent-Replay: true`) em menos de 10ms.
3. **Consulta de Cobrança (`GET /v1/pix/cob/{txid}`)**:
   - Validação da persistência e estado ativo.
4. **Liquidação e Dupla Entrada Contábil (`POST /v1/pix/simulation/inbound-payment`)**:
   - Simulação de notificação de liquidação do Banco Central/SPI.
   - Débito na conta transitória e crédito na conta do lojista no Ledger.
   - Gravação de evento no Transactional Outbox.

---

## 🚀 Como Executar

### Opção A: Executando via Docker (Sem instalar o k6 na máquina)
```bash
docker run --rm -i --net=host grafana/k6 run - < load-tests/k6-charges-load.js
```

### Opção B: Executando com o binário local do k6
```bash
k6 run load-tests/k6-charges-load.js
```

Para apontar para outra URL (ex: ambiente Docker):
```bash
k6 run -e API_URL=http://localhost:5000 load-tests/k6-charges-load.js
```

---

## 📊 Metas de SLA & Thresholds Configurados

| Métrica | Meta / Threshold | Significado |
| :--- | :--- | :--- |
| **Taxa de Erro HTTP** | `< 0.01` (< 1%) | Zero tolerância a erros 500 no core banking |
| **Latência p95** | `< 150 ms` | 95% das emissões e liquidações abaixo de 150ms |
| **Latência p99** | `< 300 ms` | 99% das requisições em menos de 300ms |
| **Throughput Sustentado** | `> 300 req/s` | Capacidade medida em hardware padrão |
