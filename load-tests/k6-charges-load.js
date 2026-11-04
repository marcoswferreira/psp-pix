import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';

// Métricas customizadas
export const successfulCharges = new Counter('successful_charges');
export const settledCharges = new Counter('settled_charges');
export const idempotencyReplays = new Counter('idempotency_replays');
export const errorRate = new Rate('custom_error_rate');
export const chargeCreationDuration = new Trend('charge_creation_duration_ms');

export const options = {
  stages: [
    { duration: '10s', target: 20 },  // Rampa de aquecimento para 20 VUs
    { duration: '30s', target: 50 },  // Carga constante de 50 VUs simultâneos
    { duration: '10s', target: 0 },   // Rampa de desaceleração
  ],
  thresholds: {
    http_req_failed: ['rate<0.01'],              // Menos de 1% de erros HTTP
    http_req_duration: ['p(95)<150', 'p(99)<300'], // SLA de latência: p95 < 150ms, p99 < 300ms
    custom_error_rate: ['rate<0.01'],
  },
};

const BASE_URL = __ENV.API_URL || 'http://localhost:5000';

function randomTxId() {
  return 'TX' + Math.random().toString(36).substring(2, 12).toUpperCase() + Date.now().toString(36).toUpperCase();
}

function randomKey() {
  return 'idemp-' + Math.random().toString(36).substring(2, 15) + Math.random().toString(36).substring(2, 15);
}

export default function () {
  const txId = randomTxId();
  const idempotencyKey = randomKey();

  // =========================================================================
  // ETAPA 1: Emissão de Cobrança Pix Imediata (POST /api/v1/charges)
  // =========================================================================
  const cobPayload = JSON.stringify({
    amount: parseFloat((Math.random() * 200 + 10).toFixed(2)),
    pixKey: 'lojista@minipsp.com.br',
    description: 'Pedido E-commerce k6',
    merchantName: 'PSP MERCHANT BR',
    merchantCity: 'SAO PAULO',
    txId: txId
  });

  const cobParams = {
    headers: {
      'Content-Type': 'application/json',
      'Idempotency-Key': idempotencyKey,
    },
  };

  const startCob = Date.now();
  const resCob = http.post(`${BASE_URL}/api/v1/charges`, cobPayload, cobParams);
  chargeCreationDuration.add(Date.now() - startCob);

  const cobOk = check(resCob, {
    'POST /api/v1/charges retornou 201': (r) => r.status === 201,
    'BR Code gerado com sucesso': (r) => r.json('value.brCode') !== undefined && r.json('value.brCode').startsWith('000201'),
    'SVG do QR Code gerado': (r) => r.json('value.qrCodeSvg') !== undefined,
  });

  if (cobOk) {
    successfulCharges.add(1);
  } else {
    errorRate.add(1);
  }

  // =========================================================================
  // ETAPA 2: Teste de Idempotência (Replay idêntico da mesma chamada)
  // =========================================================================
  const resReplay = http.post(`${BASE_URL}/api/v1/charges`, cobPayload, cobParams);
  const replayOk = check(resReplay, {
    'Replay idempotente retornou 201': (r) => r.status === 201,
    'Header Idempotent-Replay presente': (r) => r.headers['Idempotent-Replay'] === 'true',
  });

  if (replayOk) {
    idempotencyReplays.add(1);
  }

  // =========================================================================
  // ETAPA 3: Consulta de Cobrança Pix (GET /api/v1/charges/{txId})
  // =========================================================================
  const resGet = http.get(`${BASE_URL}/api/v1/charges/${txId}`);
  check(resGet, {
    'GET /api/v1/charges/{txId} retornou 200': (r) => r.status === 200,
    'Status inicial da cobrança é Active': (r) => r.json('value.status') === 'Active',
  });

  // =========================================================================
  // ETAPA 4: Simulação de Pagamento Bancário Recebido (Inbound Notification)
  // =========================================================================
  const inbPayload = JSON.stringify({
    txId: txId,
    amount: parseFloat(resCob.json('value.amount')),
  });

  const resInb = http.post(`${BASE_URL}/api/v1/payments/simulation`, inbPayload, {
    headers: { 'Content-Type': 'application/json' },
  });

  const inbOk = check(resInb, {
    'POST /api/v1/payments/simulation retornou 200': (r) => r.status === 200,
    'Pagamento liquidado com sucesso': (r) => r.json('value.success') === true,
  });

  if (inbOk) {
    settledCharges.add(1);
  } else {
    errorRate.add(1);
  }

  sleep(0.1);
}
