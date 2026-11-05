import { expect, test } from '@playwright/test';
const learner = { id: 'learner', time_zone: 'Europe/Warsaw', time_zone_confirmed: true, daily_new_words_limit: 5, pending_daily_new_words_limit: null, reminder: { local_time: '19:00', days_of_week: ['mon'] }, onboarded_at: '2026-11-01T09:00:00Z' };
const item = { id: 'item', position: 31, kind: 'new_word_card', prompt_uk: 'дім', context_uk: null, card: { lemma_pl: 'dom', translations_uk: ['дім'], pronunciation_uk: 'дом', example_pl: 'To jest dom.', example_uk: 'Це дім.', gender: 'm' } };
const session = { id: 'session', learner_day: '2026-11-02', ends_at: '2026-11-03T00:00:00Z', status: 'active', target_language: 'pl', total_items: 40, completed_items: 2, notices: [], current_item: item, last_result: null };
for (const theme of ['light', 'dark']) test(`${theme}: картка, відновлення, очікування й закінчення`, async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 740 });
  await page.route('https://telegram.org/**', route => route.fulfill({ contentType: 'text/javascript', body: `window.Telegram={WebApp:{initData:"fixture",colorScheme:"${theme}",version:"6.0"}};` }));
  await page.addInitScript(value => localStorage.setItem('constant-learning.theme.v1', value), theme);
  await page.route('**/api/v1/learners/me', route => route.fulfill({ json: learner }));
  await page.route('**/api/v1/sessions/current', route => route.fulfill({ json: session }));
  let release!: () => void; const pending = new Promise<void>(resolve => { release = resolve; });
  await page.route('**/acknowledge', async route => { await pending; await route.fulfill({ status: 409, json: { code: 'session.expired', message: '' } }); });
  await page.goto('/session'); await expect(page.getByRole('heading', { name: 'dom' })).toBeVisible();
  await expect(page).toHaveScreenshot(`${theme}-session-card.png`, { fullPage: true });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.reload(); await expect(page.getByText('Опрацьовано 2 із 40')).toBeVisible();
  await page.getByRole('button', { name: 'Далі' }).click(); await expect(page.getByRole('status').filter({ hasText: 'Зберігаємо' })).toBeVisible();
  release(); await expect(page.getByText(/Час цього заняття минув/)).toBeVisible();
  await expect(page).toHaveScreenshot(`${theme}-session-expired.png`, { fullPage: true });
});

for (const theme of ['light', 'dark']) test(`${theme}: підсумок завершеного заняття`, async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 740 });
  await page.route('https://telegram.org/**', route => route.fulfill({ contentType: 'text/javascript', body: `window.Telegram={WebApp:{initData:"fixture",colorScheme:"${theme}",version:"6.0"}};` }));
  await page.addInitScript(value => localStorage.setItem('constant-learning.theme.v1', value), theme);
  await page.route('**/api/v1/learners/me', route => route.fulfill({ json: learner }));
  let complete = false;
  await page.route('**/api/v1/sessions/current', route => route.fulfill(complete ? { status: 404, json: { code: 'session.not_found' } } : { json: session }));
  await page.route('**/already-known', route => { complete = true; return route.fulfill({ json: { session_completed: true, next_item: null, replacement_added: false, control_repetition_on: '2026-11-09' } }); });
  await page.goto('/session'); await page.getByRole('button', { name: 'Вже знаю' }).click();
  await expect(page.getByRole('heading', { name: 'Заняття завершено' })).toBeVisible();
  await expect(page).toHaveScreenshot(`${theme}-session-summary.png`, { fullPage: true });
});
