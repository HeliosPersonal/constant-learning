import { createHmac } from 'node:crypto';
const fields = { auth_date: String(Math.floor(Date.now() / 1000)), query_id: 'load', user: JSON.stringify({ id: 910000001, first_name: 'Load' }) };
const secret = createHmac('sha256', 'WebAppData').update('123456:TEST_ONLY_TOKEN_NOT_VALID').digest();
fields.hash = createHmac('sha256', secret).update(Object.keys(fields).sort().map(k => `${k}=${fields[k]}`).join('\n')).digest('hex');
const headers = { Authorization: 'tma ' + new URLSearchParams(fields), 'Content-Type': 'application/json' };
async function call(path, method = 'GET', body) { const response = await fetch('http://127.0.0.1:18889' + path, { method, headers, body: body && JSON.stringify(body) }); const text = await response.text(); console.log(path, response.status, text.slice(0, 600)); if (!response.ok) throw new Error(text); return text ? JSON.parse(text) : null; }
await call('/api/v1/today');
await call('/api/v1/dictionary/search?q=slowo&limit=20');
const session = await call('/api/v1/sessions', 'POST');
if (session.total_items !== 20) throw new Error('unexpected total');
const item = session.current_item;
let value = Number(/^слово (\d+)$/.exec(item.prompt_uk)?.[1]);
let suffix = '';
for (let i = 0; i < 4; i++) { suffix = String.fromCharCode(97 + value % 26) + suffix; value = Math.floor(value / 26); }
const path = `/api/v1/sessions/${session.id}/items/${item.id}`;
const answer = await call(path + '/answer', 'POST', { answer: 'slowo' + suffix, dont_know: false });
if (answer.outcome !== 'correct') throw new Error('unexpected outcome');
await call(path + '/continue', 'POST');
