# Referência de Contratos da API REST (v1.0)

A API do **Mini-PSP Pix Simulado** segue os princípios de design RESTful, versionamento explícito via segmento de URL (`/api/v1/...`), envelopes padronizados baseados no padrão **Result Monad** do [18SOAT_TechChallenge](https://github.com/marcoswferreira/18SOAT_TechChallenge) e tratamento de erros com **ProblemDetails (RFC 7807)**.

---

## 📋 Convenções Globais

* **URL Base Local**: `http://localhost:5000/api/v1`
* **Especificação OpenAPI**: `http://localhost:5000/openapi/v1.json`
* **Cabeçalhos Comuns**:
  * `Content-Type: application/json`
  * `Idempotency-Key: <chave-unica-uuid>` (Recomendado/Obrigatório em operações mutantes de cobrança e saque).
  * `Idempotent-Replay: true` (Retornado pelo servidor quando a resposta provém do cache de idempotência).

### Padrão de Envelope de Sucesso (`ResultGeneric<T>`)
```json
{
  "value": { ... },
  "isSuccess": true,
  "errors": []
}
```

### Padrão de Erro (`ProblemDetails` RFC 7807)
```json
{
  "type": "about:blank",
  "title": "Saldo Insuficiente",
  "status": 422,
  "detail": "Saldo insuficiente na conta MERCHANT-001. Saldo: R$ 100,00, Débito solicitado: R$ 500,00.",
  "instance": "/api/v1/payout"
}
```

---

## 🔌 Endpoints da API

### 1. Criar Cobrança Pix Imediata
* **Rota**: `POST /api/v1/charges`
* **Cabeçalho**: `Idempotency-Key: <string>`
* **Status de Sucesso**: `201 Created` (`Location: /api/v1/charges/{txId}`)

#### Request Body:
```json
{
  "amount": 150.50,
  "pixKey": "12345678901",
  "description": "Assinatura Mensal",
  "merchantName": "LOJA TECH",
  "merchantCity": "SAO PAULO",
  "txId": "TX1234567890123456" // Opcional (gera automaticamente se omitido)
}
```

#### Response Body (`201 Created`):
```json
{
  "value": {
    "txId": "TX1234567890123456",
    "amount": 150.50,
    "pixKey": "12345678901",
    "description": "Assinatura Mensal",
    "merchantName": "LOJA TECH",
    "merchantCity": "SAO PAULO",
    "brCode": "00020126330014br.gov.bcb.pix0111123456789015204000053039865406150.505802BR5909LOJA TECH6009SAO PAULO62200516TX12345678901234566304C1A2",
    "qrCodeSvg": "<svg viewBox=\"0 0 280 280\" ...>...</svg>",
    "status": "Active",
    "expiresAtUtc": "2026-11-04T10:48:46Z",
    "paidAtUtc": null,
    "endToEndId": null
  },
  "isSuccess": true,
  "errors": []
}
```

---

### 2. Consultar Cobrança Pix por TxId
* **Rota**: `GET /api/v1/charges/{txId}`
* **Status**: `200 OK` ou `404 Not Found`

#### Response Body (`200 OK`):
```json
{
  "value": {
    "txId": "TX1234567890123456",
    "amount": 150.50,
    "pixKey": "12345678901",
    "description": "Assinatura Mensal",
    "status": "Completed",
    "paidAtUtc": "2026-11-04T09:49:10Z",
    "endToEndId": "E1234567820261104094900000000001"
  },
  "isSuccess": true,
  "errors": []
}
```

---

### 3. Simulação de Pagamento Bancário (Inbound SPI)
* **Rota**: `POST /api/v1/payments/simulation`
* **Status**: `200 OK` ou `422 Unprocessable Entity`

#### Request Body:
```json
{
  "txId": "TX1234567890123456",
  "amount": 150.50,
  "chaos": {
    "action": 0, // 0=None, 1=Delay, 2=Timeout, 3=Fail, 4=Duplicate
    "delayMilliseconds": 0
  }
}
```

#### Response Body (`200 OK`):
```json
{
  "value": {
    "success": true,
    "txId": "TX1234567890123456",
    "endToEndId": "E9876543220261104095012345678901",
    "amount": 150.50,
    "paidAtUtc": "2026-11-04T09:50:00Z",
    "message": "Pagamento liquidado com sucesso e lançado no Ledger."
  },
  "isSuccess": true,
  "errors": []
}
```

---

### 4. Solicitação de Payout (Saque Pix do Lojista)
* **Rota**: `POST /api/v1/payout`
* **Cabeçalho**: `Idempotency-Key: <string>`
* **Status**: `200 OK`, `409 Conflict` ou `422 Unprocessable Entity`

#### Request Body:
```json
{
  "merchantAccountId": "22222222-2222-2222-2222-222222222222",
  "amount": 75.00,
  "destinationPixKey": "fornecedor@pix.com.br",
  "description": "Pagamento Fornecedor Lote 1",
  "chaos": null
}
```

#### Response Body (`200 OK`):
```json
{
  "value": {
    "success": true,
    "payoutId": "c4b9b778-8cb2-4a0e-bc4b-70c8d1976001",
    "endToEndId": "E1234567820261104095111223344556",
    "amount": 75.00,
    "remainingBalance": 925.00,
    "message": "Payout Pix concluído com sucesso."
  },
  "isSuccess": true,
  "errors": []
}
```

---

### 5. Consultar Saldo no Ledger
* **Rota**: `GET /api/v1/ledger/accounts/{id}/balance`
* **Status**: `200 OK` ou `404 Not Found`

#### Response Body (`200 OK`):
```json
{
  "value": {
    "id": "22222222-2222-2222-2222-222222222222",
    "accountNumber": "MERCHANT-001",
    "type": 2, // 1=PixSettlementTransit, 2=Merchant
    "balance": 1075.50,
    "currency": "BRL",
    "version": 4
  },
  "isSuccess": true,
  "errors": []
}
```

---

### 6. Consultar Extrato do Ledger (Lançamentos Contábeis)
* **Rota**: `GET /api/v1/ledger/accounts/{id}/statement`
* **Status**: `200 OK` ou `404 Not Found`

#### Response Body (`200 OK`):
```json
{
  "value": [
    {
      "id": "311c6d32-d1d7-4028-9844-32c1e4c75951",
      "transactionId": "732f1437-9755-4a7b-a25e-cf85350929ee",
      "type": 2, // 1=Debit, 2=Credit
      "amount": 150.50,
      "description": "Liquidação Pix TxId TX1234567890123456",
      "createdAtUtc": "2026-11-04T09:50:00Z"
    },
    {
      "id": "893c5240-5a39-4d87-9bb3-5e92160e1d53",
      "transactionId": "8f8b63a9-a65c-42b4-82a1-fa43e49e0ec8",
      "type": 1, // 1=Debit
      "amount": 75.00,
      "description": "Payout Pix para fornecedor@pix.com.br",
      "createdAtUtc": "2026-11-04T09:51:00Z"
    }
  ],
  "isSuccess": true,
  "errors": []
}
```

---

### 7. Cadastrar Webhook do Comerciante
* **Rota**: `POST /api/v1/webhooks`
* **Status**: `200 OK`

#### Request Body:
```json
{
  "merchantId": "MERCHANT-001",
  "targetUrl": "https://api.sualoja.com.br/pix/webhook",
  "secretKey": "sua_chave_secreta_opcional" // Se omitido, o PSP gera uma chave segura
}
```

#### Response Body (`200 OK`):
```json
{
  "value": {
    "id": "1e3c8804-633d-4c3e-bbfe-88c9a387532a",
    "merchantId": "MERCHANT-001",
    "targetUrl": "https://api.sualoja.com.br/pix/webhook",
    "secretKey": "sua_chave_secreta_opcional",
    "isActive": true,
    "createdAtUtc": "2026-11-04T09:52:00Z"
  },
  "isSuccess": true,
  "errors": []
}
```

