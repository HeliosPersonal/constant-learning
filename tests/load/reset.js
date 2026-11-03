import http from 'k6/http';
import crypto from 'k6/crypto';
import exec from 'k6/execution';
import { check } from 'k6';
import { Trend, Counter } from 'k6/metrics';

// T09: 50 послідовних скидань прогресу, кожне на окремому learner із повним графом найгіршого
// випадку (2000 слів / 20000 відповідей, seed-когорта 930000001..930000050 у LoadHost Program.cs).
// Межа [PRD §6](../../docs/features/reset-progress/PRD.md#6-non-functional-requirements): p95 ≤ 2000 ms.
const resetDuration = new Trend('reset_duration', true);
const failures = new Counter('reset_failures');
const base = __ENV.BASE_URL || 'http://host.docker.internal:18889';
const token = __ENV.BOT_TOKEN || '123456:TEST_ONLY_TOKEN_NOT_VALID';

export const options = {
  scenarios: { reset: { executor: 'per-vu-iterations', vus: 1, iterations: 50, maxDuration: '10m' } },
  thresholds: { reset_duration: ['p(95)<2000'], reset_failures: ['count==0'] },
};

function authorization(id) {
  const fields = { auth_date: String(Math.floor(Date.now() / 1000)), query_id: 'load', user: JSON.stringify({ id, first_name: 'Load' }) };
  const checkString = Object.keys(fields).sort().map(key => `${key}=${fields[key]}`).join('\n');
  const secret = crypto.hmac('sha256', 'WebAppData', token, 'binary');
  fields.hash = crypto.hmac('sha256', secret, checkString, 'hex');
  return 'tma ' + Object.keys(fields).map(key => `${encodeURIComponent(key)}=${encodeURIComponent(fields[key])}`).join('&');
}

export default function () {
  failures.add(0);
  const id = 930000001 + exec.scenario.iterationInTest;
  const response = http.post(base + '/api/v1/learners/me/progress/reset', null, {
    headers: { Authorization: authorization(id), 'Content-Type': 'application/json' }, tags: { name: '/api/v1/learners/me/progress/reset' },
  });
  resetDuration.add(response.timings.duration);
  let body = null;
  try { body = response.json(); } catch (_) { body = null; }
  const ok = check(response, { 'статус 200': r => r.status === 200, 'erased true': () => body !== null && body.erased === true });
  if (!ok) { failures.add(1); console.error(`Скидання learner ${id}: HTTP ${response.status} ${String(response.body).slice(0, 300)}`); }
}
