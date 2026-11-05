import http from 'k6/http';
import crypto from 'k6/crypto';
import exec from 'k6/execution';
import { check, sleep } from 'k6';
import { Trend, Counter } from 'k6/metrics';

// T17 (multi-target-languages): перемикання активної мови й відкриття «Сьогодні» нової мови під
// навантаженням. 50 одночасних learner, кожен веде дві мови (pl активна + en) з непорожнім обліком у
// кожній. Seed-когорта 950000001..950000050 у LoadHost Program.cs. Кожна ітерація перемикає pl→en,
// відкриває «Сьогодні» en, перемикає en→pl, відкриває «Сьогодні» pl — обидва перемикання й обидва
// відкриття вимірюються. Межі [PRD §6](../../docs/features/multi-target-languages/PRD.md#6-non-functional-requirements):
// p95 перемикання активної мови ≤ 1500 ms, p95 відкриття «Сьогодні» ≤ 1500 ms.
// Ізоляція (0 відповідей не до своєї мови) перевіряється integration-тестами (TodayLanguageIsolationTests,
// ProgressIsolationTests), а не цим сценарієм латентності.
const switchDuration = new Trend('lang_switch_duration', true);
const todayDuration = new Trend('lang_today_duration', true);
const failures = new Counter('lang_switch_failures');
const base = __ENV.BASE_URL || 'http://host.docker.internal:18889';
const token = __ENV.BOT_TOKEN || '123456:TEST_ONLY_TOKEN_NOT_VALID';

export const options = {
  scenarios: {
    language_switch: { executor: 'constant-vus', vus: 50, duration: '2m', exec: 'languageSwitch', env: { COHORT_BASE: '950000000' } },
  },
  thresholds: {
    lang_switch_duration: ['p(95)<1500'],
    lang_today_duration: ['p(95)<1500'],
    lang_switch_failures: ['count==0'],
  },
};

function authorization(id) {
  const fields = { auth_date: String(Math.floor(Date.now() / 1000)), query_id: 'load', user: JSON.stringify({ id, first_name: 'Load' }) };
  const checkString = Object.keys(fields).sort().map(key => `${key}=${fields[key]}`).join('\n');
  const secret = crypto.hmac('sha256', 'WebAppData', token, 'binary');
  fields.hash = crypto.hmac('sha256', secret, checkString, 'hex');
  return 'tma ' + Object.keys(fields).map(key => `${encodeURIComponent(key)}=${encodeURIComponent(fields[key])}`).join('&');
}

function setActive(headers, language) {
  const response = http.put(base + '/api/v1/learners/me/active-language', JSON.stringify({ target_language: language }), { headers, tags: { name: 'active-language' } });
  switchDuration.add(response.timings.duration);
  if (!check(response, { 'перемикання 200': r => r.status === 200 })) { failures.add(1); console.error(`active-language ${language}: HTTP ${response.status} ${String(response.body).slice(0, 300)}`); }
}

function openToday(headers) {
  const response = http.get(base + '/api/v1/today', { headers, tags: { name: 'today' } });
  todayDuration.add(response.timings.duration);
  if (!check(response, { '«Сьогодні» 200': r => r.status === 200 })) { failures.add(1); console.error(`today: HTTP ${response.status} ${String(response.body).slice(0, 300)}`); }
}

export function languageSwitch() {
  failures.add(0);
  const id = Number(__ENV.COHORT_BASE) + ((exec.vu.idInInstance - 1) % 50) + 1;
  const headers = { Authorization: authorization(id), 'Content-Type': 'application/json' };
  setActive(headers, 'en');
  openToday(headers);
  setActive(headers, 'pl');
  openToday(headers);
  sleep(1);
}
