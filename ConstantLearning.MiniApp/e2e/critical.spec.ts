import { createHmac } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { buildSync } from 'esbuild';
import { expect, test, type Page } from '@playwright/test';

// Docker CLI та .NET-клієнт використовують різний запис Windows named pipe.
const dockerEnv = { ...process.env };
if (dockerEnv.DOCKER_HOST?.startsWith('npipe://./')) dockerEnv.DOCKER_HOST = dockerEnv.DOCKER_HOST.replace('npipe://./', 'npipe:////./');

const telegram = buildSync({ entryPoints: ['e2e/telegram.ts'], bundle: true, write: false, format: 'iife', platform: 'browser' }).outputFiles[0].text;
function sign(id: number) {
  const fields: Record<string, string> = { auth_date: '1793437200', query_id: 'test-query', user: JSON.stringify({ id, first_name: 'Test User' }) };
  const check = Object.entries(fields).map(([key, value]) => `${key}=${value}`).join('\n');
  const secret = createHmac('sha256', 'WebAppData').update('123456:TEST_ONLY_TOKEN_NOT_VALID').digest();
  fields.hash = createHmac('sha256', secret).update(check).digest('hex');
  return new URLSearchParams(fields).toString();
}
async function open(page: Page, id: number, path = '/') {
  await page.route('https://telegram.org/js/telegram-web-app.js', route => route.fulfill({ contentType: 'application/javascript', body: '' }));
  await page.addInitScript(({ data, script }) => { sessionStorage.setItem('e2e.initData', data); (0, eval)(script); }, { data: sign(id), script: telegram });
  await page.goto(path);
}
test('AC-01: онбординг відкриває сьогодні', async ({ page }) => {
  await open(page, 900000001);
  await page.getByRole('button', { name: 'Почати навчання' }).click();
  await expect(page.getByRole('heading', { name: 'Сьогодні', exact: true })).toBeVisible();
});
test('AC-03 та AC-12: повторення перед новим словом і правильна оцінка сервера', async ({ page }) => {
  await open(page, 900000002);
  await page.getByRole('button', { name: 'Почати заняття' }).click();
  await page.getByLabel('Ваша відповідь').fill('żaba');
  const response = page.waitForResponse(r => r.url().endsWith('/answer') && r.request().method() === 'POST');
  await page.getByRole('button', { name: 'Перевірити' }).click();
  expect((await (await response).json()).outcome).toBe('correct');
  await expect(page.getByRole('heading', { name: 'Правильно!' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'kot', exact: true })).toHaveCount(0);
  await page.getByRole('button', { name: 'Продовжити' }).click();
  for (let index = 0; index < 3; index++) {
    await expect(page.getByText('НОВЕ СЛОВО')).toBeVisible();
    await page.getByRole('button', { name: 'Далі', exact: true }).click();
  }
  for (let index = 0; index < 3; index++) {
    await expect(page.getByLabel('Ваша відповідь')).toBeVisible();
    await page.getByRole('button', { name: 'Не знаю' }).click();
    await expect(page.getByRole('heading', { name: 'Вивчимо це слово разом.' })).toBeVisible();
    await page.getByRole('button', { name: 'Продовжити' }).click();
  }
  await expect(page.getByRole('heading', { name: 'Заняття завершено' })).toBeVisible();
});
test('AC-17: оскарження зберігає нейтралізацію після перезавантаження', async ({ page }) => {
  await open(page, 900000003);
  await page.getByRole('button', { name: 'Почати заняття' }).click();
  await page.getByLabel('Ваша відповідь').fill('xaba');
  const answer = page.waitForResponse(r => r.url().endsWith('/answer') && r.request().method() === 'POST');
  await page.getByRole('button', { name: 'Перевірити' }).click();
  expect((await (await answer).json()).outcome).toBe('mistake_typo');
  await page.getByRole('button', { name: 'Моя відповідь теж правильна' }).click();
  await page.getByRole('button', { name: 'Підтвердити' }).click();
  await expect(page.getByRole('heading', { name: 'Відповідь нейтралізовано' })).toBeVisible();
  const containers = execFileSync('docker', ['ps', '-q', '--filter', 'label=constant-learning.e2e=critical-flows'], { encoding: 'utf8', env: dockerEnv }).trim().split(/\s+/);
  expect(containers).toHaveLength(1);
  const sql = "select a.outcome || '|' || r.origin || '|' || r.status || '|' || (select count(*) from verification_jobs v where v.content_report_id = r.id) from answer_events a join learners l on l.id = a.learner_id join answer_neutralizations n on n.answer_event_id = a.id join content_reports r on r.id = n.content_report_id and r.answer_event_id = a.id where l.telegram_user_id = 900000003";
  const persisted = execFileSync('docker', ['exec', containers[0], 'psql', '-U', 'miniapp_test', '-d', 'miniapp_e2e', '-At', '-c', sql], { encoding: 'utf8', env: dockerEnv }).trim();
  expect(persisted).toBe('mistake_typo|answer_dispute|in_editor_queue|0');
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Відповідь нейтралізовано' })).toBeVisible();
  await page.getByRole('button', { name: 'Продовжити' }).click();
  await expect(page.getByText('НОВЕ СЛОВО')).toBeVisible();
});
test('AC-34: повідомлення з картки словника', async ({ page }) => {
  await open(page, 900000004, '/dictionary');
  await page.getByRole('searchbox', { name: 'Слово для пошуку' }).fill('kot');
  await page.getByRole('button', { name: /^kot / }).click();
  await page.getByRole('button', { name: 'Повідомити про неточність' }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel('Що потрібно перевірити?').selectOption('translation');
  await dialog.getByLabel(/Запропоноване значення/).fill('кіт');
  await dialog.getByRole('button', { name: 'Надіслати на перевірку' }).click();
  await expect(dialog.getByRole('status')).toContainText('Дякуємо, передали на перевірку');
});
