import http from 'k6/http';
import crypto from 'k6/crypto';
import exec from 'k6/execution';
import { check, sleep } from 'k6';
import { Trend, Counter } from 'k6/metrics';

// T12 (unlimited-learning): старт extra-слова й старт додаткової практики під навантаженням —
// 50 одночасних learner на дію, 1 запит на секунду, 2 хвилини. Seed-когорта 940000001..940000100 у
// LoadHost Program.cs має виконаний план дня (нові слова відкладено, повторень немає), тож перший
// дотик створює extra-session (201), а наступні повертають активну (200) — обидва шляхи вимірюють старт.
// Межі [PRD §6](../../docs/features/unlimited-learning/PRD.md#6-non-functional-requirements):
// старт extra-слова p95 ≤ 500 ms, старт практики p95 ≤ 800 ms.
const extraWordDuration = new Trend('extra_word_duration', true);
const extraPracticeDuration = new Trend('extra_practice_duration', true);
const failures = new Counter('extra_start_failures');
const base = __ENV.BASE_URL || 'http://host.docker.internal:18889';
const token = __ENV.BOT_TOKEN || '123456:TEST_ONLY_TOKEN_NOT_VALID';

export const options = {
  scenarios: {
    extra_word: { executor: 'constant-vus', vus: 50, duration: '2m', exec: 'extraWord', env: { COHORT_BASE: '940000000' } },
    extra_practice: { executor: 'constant-vus', vus: 50, duration: '2m', exec: 'extraPractice', env: { COHORT_BASE: '940000050' } },
  },
  thresholds: {
    extra_word_duration: ['p(95)<500'],
    extra_practice_duration: ['p(95)<800'],
    extra_start_failures: ['count==0'],
  },
};

function authorization(id) {
  const fields = { auth_date: String(Math.floor(Date.now() / 1000)), query_id: 'load', user: JSON.stringify({ id, first_name: 'Load' }) };
  const checkString = Object.keys(fields).sort().map(key => `${key}=${fields[key]}`).join('\n');
  const secret = crypto.hmac('sha256', 'WebAppData', token, 'binary');
  fields.hash = crypto.hmac('sha256', secret, checkString, 'hex');
  return 'tma ' + Object.keys(fields).map(key => `${encodeURIComponent(key)}=${encodeURIComponent(fields[key])}`).join('&');
}

function start(path, trend) {
  failures.add(0);
  const id = Number(__ENV.COHORT_BASE) + ((exec.vu.idInInstance - 1) % 50) + 1;
  const response = http.post(base + path, null, {
    headers: { Authorization: authorization(id), 'Content-Type': 'application/json' }, tags: { name: path },
  });
  trend.add(response.timings.duration);
  const ok = check(response, { 'статус 200/201': r => r.status === 200 || r.status === 201 });
  if (!ok) { failures.add(1); console.error(`Старт ${path} learner ${id}: HTTP ${response.status} ${String(response.body).slice(0, 300)}`); }
  sleep(1);
}

export function extraWord() { start('/api/v1/sessions/extra-word', extraWordDuration); }

export function extraPractice() { start('/api/v1/sessions/extra-practice', extraPracticeDuration); }
