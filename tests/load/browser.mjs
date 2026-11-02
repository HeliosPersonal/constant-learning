import { createRequire } from 'node:module';
import { createHmac } from 'node:crypto';
import { writeFile } from 'node:fs/promises';
const require = createRequire(new URL('../../ConstantLearning.MiniApp/package.json', import.meta.url));
const { chromium } = require('@playwright/test');
const { buildSync } = require('esbuild');
const telegram = buildSync({ entryPoints: ['ConstantLearning.MiniApp/e2e/telegram.ts'], bundle: true, write: false, format: 'iife', platform: 'browser' }).outputFiles[0].text;
const base = process.env.BASE_URL || 'http://127.0.0.1:18889';
const results = process.env.RESULTS_DIR || 'tests/load/results';
function sign(id) {
  const fields = { auth_date: String(Math.floor(Date.now() / 1000)), query_id: 'load', user: JSON.stringify({ id, first_name: 'Load' }) };
  const secret = createHmac('sha256', 'WebAppData').update('123456:TEST_ONLY_TOKEN_NOT_VALID').digest();
  fields.hash = createHmac('sha256', secret).update(Object.keys(fields).sort().map(key => `${key}=${fields[key]}`).join('\n')).digest('hex');
  return new URLSearchParams(fields).toString();
}
const browser = await chromium.launch({ headless: true });
const samples = [];
let failures = 0;
const count = process.env.BROWSER_PREFLIGHT === '1' ? 1 : 50;
for (let i = 0; i < count; i++) {
  const start = Date.now();
  const context = await browser.newContext({ viewport: { width: 390, height: 844 }, locale: 'uk-UA', timezoneId: 'UTC' });
  const page = await context.newPage();
  await page.route('https://telegram.org/js/telegram-web-app.js', route => route.fulfill({ contentType: 'application/javascript', body: '' }));
  await page.addInitScript(({ data, script }) => { sessionStorage.setItem('e2e.initData', data); (0, eval)(script); }, { data: sign(910000001 + i), script: telegram });
  try {
    const timing = page.waitForResponse(response => response.url().endsWith('/api/v1/telemetry/client-timings') && response.request().method() === 'POST', { timeout: 30000 });
    await page.goto(base + '/today');
    const response = await timing;
    const payload = response.request().postDataJSON();
    if (!response.ok() || payload.metric !== 'today_ready_ms' || !Number.isFinite(payload.value_ms)) throw new Error(`invalid telemetry status ${response.status()}`);
    const resources = await page.evaluate(() => performance.getEntriesByType('resource').filter(entry => new URL(entry.name).origin === location.origin).map(entry => { const item = entry; return { path: new URL(item.name).pathname, start_ms: item.startTime, duration_ms: item.duration, response_start_ms: item.responseStart, response_end_ms: item.responseEnd }; }));
    const navigation = await page.evaluate(() => { const item = performance.getEntriesByType('navigation')[0]; return item ? { duration_ms: item.duration, response_start_ms: item.responseStart, response_end_ms: item.responseEnd, dom_content_loaded_ms: item.domContentLoadedEventEnd } : null; });
    samples.push({ open: i + 1, timestamp: new Date(start).toISOString(), value_ms: payload.value_ms, status: response.status(), navigation, resources });
  } catch (error) { failures++; samples.push({ open: i + 1, timestamp: new Date(start).toISOString(), error: String(error) }); }
  await context.close();
  await writeFile(`${results}/browser-samples.json`, JSON.stringify(samples, null, 2));
  if (i + 1 < count) await new Promise(resolve => setTimeout(resolve, Math.max(0, 15000 - (Date.now() - start))));
}
await browser.close();
if (failures) process.exitCode = 1;
