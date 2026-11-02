import http from 'k6/http';
import crypto from 'k6/crypto';
import exec from 'k6/execution';
import { check, sleep } from 'k6';
import { Counter } from 'k6/metrics';

const failures = new Counter('journey_failures');
const answers = new Counter('graded_answers');
const base = __ENV.BASE_URL || 'http://host.docker.internal:18889';
const token = __ENV.BOT_TOKEN || '123456:TEST_ONLY_TOKEN_NOT_VALID';
export const options = {
  scenarios: { learners: { executor: 'constant-vus', vus: 100, duration: '15m', gracefulStop: '30s' } },
  thresholds: { journey_failures: ['count==0'], checks: ['rate==1'] },
};

function authorization(id) {
  const fields = { auth_date: String(Math.floor(Date.now() / 1000)), query_id: 'load', user: JSON.stringify({ id, first_name: 'Load' }) };
  const checkString = Object.keys(fields).sort().map(key => `${key}=${fields[key]}`).join('\n');
  const secret = crypto.hmac('sha256', 'WebAppData', token, 'binary');
  fields.hash = crypto.hmac('sha256', secret, checkString, 'hex');
  return 'tma ' + Object.keys(fields).map(key => `${encodeURIComponent(key)}=${encodeURIComponent(fields[key])}`).join('&');
}
function request(method, path, body, id, expected) {
  const response = http.request(method, base + path, body === undefined ? null : JSON.stringify(body), {
    headers: { Authorization: authorization(id), 'Content-Type': 'application/json' }, tags: { name: path.replace(/[0-9a-f-]{36}/g, ':id') },
  });
  const ok = check(response, { 'успішний статус': r => expected.includes(r.status) });
  if (!ok) { failures.add(1); console.error(`HTTP ${response.status} ${path}: ${String(response.body).slice(0, 300)}`); return null; }
  try { return response.json(); } catch (_) { failures.add(1); console.error(`Некоректний JSON: ${path}`); return null; }
}
export default function () {
  failures.add(0);
  const id = 910000000 + exec.vu.idInTest;
  if (!request('GET', '/api/v1/today', undefined, id, [200])) { sleep(1); return; }
  for (let search = 0; search < 10; search++) {
    for (let n = 1; n <= 5; n++) {
      if (!request('GET', `/api/v1/dictionary/search?q=${'slowo'.slice(0, n)}&limit=20`, undefined, id, [200])) return;
      sleep(0.15);
    }
    sleep(1);
  }
  let session = request('POST', '/api/v1/sessions', undefined, id, [200, 201]);
  if (!session || session.total_items !== 20) { failures.add(1); console.error(`Очікували 20 карток: ${session?.total_items}`); return; }
  while (session && session.current_item) {
    const item = session.current_item;
    if (item.kind !== 'review' && item.kind !== 'control_repetition') { failures.add(1); console.error(`Неочікуваний вид картки: ${item.kind}`); return; }
    sleep(5);
    const path = `/api/v1/sessions/${session.id}/items/${item.id}`;
    const match = /^слово (\d+)$/.exec(item.prompt_uk);
    if (!match) { failures.add(1); console.error(`Невідомий prompt: ${item.prompt_uk}`); return; }
    let value = Number(match[1]); let suffix = '';
    for (let i = 0; i < 4; i++) { suffix = String.fromCharCode(97 + value % 26) + suffix; value = Math.floor(value / 26); }
    const typed = 'slowo' + suffix;
    const isTypo = item.position % 4 === 0;
    const text = isTypo ? typed.slice(0, -1) + (typed.endsWith('a') ? 'b' : 'a') : typed;
    const answer = request('POST', `${path}/answer`, { answer: text, dont_know: false }, id, [200]);
    if (!answer || !answer.answer_event_id || answer.outcome !== (isTypo ? 'mistake_typo' : 'correct')) { failures.add(1); console.error(`Хибна оцінка: ${answer?.outcome}`); return; }
    answers.add(1);
    session = request('POST', `${path}/continue`, undefined, id, [200]);
  }
  if (!session || session.status !== 'completed') { failures.add(1); console.error('Заняття не завершено'); }
}
