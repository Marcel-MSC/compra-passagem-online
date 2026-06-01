import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';

const baseUrl = __ENV.BASE_URL || 'http://localhost:5102';
const tripId = __ENV.TRIP_ID || '11111111-1111-1111-1111-111111111111';
const contentionVus = Number(__ENV.CONTENTION_VUS || 5);

function isSeatAvailable(seat) {
  return seat.status === 0 || seat.status === 'Available';
}

const holdSuccess = new Counter('hold_success');
const holdConflict = new Counter('hold_conflict');
const holdErrors = new Rate('hold_errors');
const holdInventoryEmpty = new Counter('hold_inventory_empty');
const searchDuration = new Trend('search_duration', true);

export const options = {
  scenarios: {
    search_load: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '30s', target: Number(__ENV.SEARCH_VUS || 20) },
        { duration: '1m', target: Number(__ENV.SEARCH_VUS || 20) },
        { duration: '20s', target: 0 },
      ],
      exec: 'searchTrips',
    },
    hold_load: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '20s', target: Number(__ENV.HOLD_VUS || 30) },
        { duration: '1m', target: Number(__ENV.HOLD_VUS || 30) },
        { duration: '20s', target: 0 },
      ],
      exec: 'holdSeat',
      startTime: '10s',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.05'],
    http_req_duration: ['p(95)<2000'],
    hold_errors: ['rate<0.10'],
    hold_success: ['count>0'],
  },
  setupTimeout: '30s',
};

export function setup() {
  const seatsRes = http.get(`${baseUrl}/api/trips/${tripId}/seats`, {
    tags: { name: 'setup_get_seats' },
  });

  if (seatsRes.status !== 200) {
    throw new Error(
      `Preflight falhou: GET /seats retornou ${seatsRes.status}. Verifique API em ${baseUrl}.`,
    );
  }

  const seats = JSON.parse(seatsRes.body);
  const available = seats.filter(isSeatAvailable).length;

  if (available === 0) {
    throw new Error(
      'Nenhum assento Available para hold_load. Rode: .\\scripts\\reset-test-data.ps1',
    );
  }

  return { availableSeats: available };
}

function tomorrowDate() {
  const d = new Date();
  d.setUTCDate(d.getUTCDate() + 1);
  return d.toISOString().slice(0, 10);
}

function pickSeat(available) {
  // Primeiros VUs disputam o mesmo assento (409 esperado); demais espalham carga.
  if (__VU <= contentionVus) {
    return available[0];
  }
  return available[(__VU + __ITER) % available.length];
}

export function searchTrips() {
  const date = tomorrowDate();
  const url = `${baseUrl}/api/trips?from=Sao%20Paulo&to=Rio%20de%20Janeiro&date=${date}`;
  const res = http.get(url, { tags: { name: 'search_trips' } });
  searchDuration.add(res.timings.duration);
  check(res, { 'search status 200': (r) => r.status === 200 });
  sleep(0.3);
}

export function holdSeat() {
  const seatsRes = http.get(`${baseUrl}/api/trips/${tripId}/seats`, {
    tags: { name: 'get_seats' },
  });

  if (seatsRes.status !== 200) {
    holdErrors.add(1);
    sleep(0.5);
    return;
  }

  const seats = JSON.parse(seatsRes.body);
  const available = seats.filter(isSeatAvailable);
  if (available.length === 0) {
    holdInventoryEmpty.add(1);
    sleep(2);
    return;
  }

  const seat = pickSeat(available);
  const payload = JSON.stringify({
    tripId: tripId,
    seatId: seat.id,
    userId: `k6-user-${__VU}-${__ITER}`,
  });

  const res = http.post(`${baseUrl}/api/reservations`, payload, {
    headers: { 'Content-Type': 'application/json' },
    tags: { name: 'create_reservation' },
  });

  if (res.status === 201) {
    holdSuccess.add(1);
    holdErrors.add(0);
  } else if (res.status === 409) {
    holdConflict.add(1);
    holdErrors.add(0);
  } else {
    holdErrors.add(1);
  }

  check(res, {
    'hold created or conflict': (r) => r.status === 201 || r.status === 409,
  });

  sleep(0.5);
}

export function handleSummary(data) {
  const setupSeats = data.setup_data?.availableSeats ?? 'n/a';
  const holdOk = data.metrics.hold_success?.values?.count ?? 0;
  const hold409 = data.metrics.hold_conflict?.values?.count ?? 0;
  const holdErrRate = (data.metrics.hold_errors?.values?.rate ?? 0) * 100;
  const inventoryEmpty = data.metrics.hold_inventory_empty?.values?.count ?? 0;

  const lines = [
    '',
    '=== Resumo humano (k6) ===',
    `Assentos no setup: ${setupSeats}`,
    `Requisições totais: ${data.metrics.http_reqs?.values?.count ?? 'n/a'}`,
    `Taxa de falha HTTP: ${((data.metrics.http_req_failed?.values?.rate ?? 0) * 100).toFixed(2)}%`,
    `Latência p95: ${(data.metrics.http_req_duration?.values?.['p(95)'] ?? 0).toFixed(0)} ms`,
    `Reservas OK (hold_success): ${holdOk}`,
    `Conflitos 409 (hold_conflict): ${hold409}`,
    `Inventário vazio (hold_inventory_empty): ${inventoryEmpty}`,
    `Erros reais de hold (hold_errors): ${holdErrRate.toFixed(1)}%`,
    '',
    'Interpretação:',
    '- hold_success > 0 = fluxo de reserva funcionando.',
    '- hold_conflict > 0 nos primeiros VUs = contenção esperada (CONTENTION_VUS).',
    '- hold_inventory_empty alto cedo = normal; com Workers + TTL 1 min (Development) assentos podem voltar no meio do teste.',
    '- hold_errors alto = falha real (GET seats, 5xx, 4xx fora de 409) — investigar API/Redis/PostgreSQL.',
    '- p95 acima do threshold = considerar mais réplicas de API ou cache.',
    '',
  ];

  return {
    stdout: lines.join('\n'),
  };
}
