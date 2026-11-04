import { expect, test } from '@playwright/test';
const learner = { id: 'learner', time_zone: 'Europe/Warsaw', time_zone_confirmed: true, daily_new_words_limit: 5, pending_daily_new_words_limit: null, reminder: { local_time: '19:00', days_of_week: ['mon'] }, onboarded_at: '2026-11-01T09:00:00Z' };
const item = { id: 'item', position: 1, kind: 'review', prompt_uk: 'жаба', context_uk: 'Це жаба.', card: null };
const session = { id: 'session', learner_day: '2026-11-02', ends_at: '2026-11-03T00:00:00Z', status: 'active', total_items: 1, completed_items: 0, notices: [], current_item: item, last_result: null };
const answer = { answer_event_id: 'event', outcome: 'mistake_typo', correct_form: 'żaba', diff: [{ op: 'substitute', actual: 'x', expected: 'ż' }, { op: 'equal', actual: 'aba', expected: 'aba' }], pronunciation_uk: 'жаба', example_pl: 'To żaba.', example_uk: 'Це жаба.', gender: 'f', dispute_allowed: true, word_status: 'review', next_review_on: '2026-11-03', next_item: null, session_completed: true };
for (const theme of ['light', 'dark']) for (const state of ['diff', 'diacritic', 'synonym', 'neutralized', 'pending', 'error']) test(`${theme}: ${state}`, async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 740 });
  await page.route('https://telegram.org/**', route => route.fulfill({ contentType: 'text/javascript', body: `window.Telegram={WebApp:{initData:"fixture",colorScheme:"${theme}",version:"6.0"}};` }));
  await page.addInitScript(value => localStorage.setItem('constant-learning.theme.v1', value), theme);
  await page.route('**/api/v1/learners/me', route => route.fulfill({ json: learner }));
  const outcome = state === 'diacritic' ? 'mistake_diacritic' : state === 'synonym' ? 'synonym' : 'mistake_typo';
  const current = state === 'pending' || state === 'error' ? session : { ...session, status: 'completed', completed_items: 1, current_item: null, last_result: { item_id: 'item', answer: { ...answer, outcome, dispute_allowed: outcome === 'mistake_typo' }, dispute: state === 'neutralized' ? { outcome: 'neutralized', content_report_id: 'report', message: 'Передали на перевірку' } : null } };
  await page.route('**/api/v1/sessions/current', route => route.fulfill({ json: current }));
  let release!: () => void; const pending = new Promise<void>(resolve => { release = resolve; });
  await page.route('**/answer', async route => { if (state === 'pending') await pending; await route.fulfill({ status: 400, json: { code: 'answer.wrong_layout' } }); });
  await page.goto('/session');
  if (state === 'pending' || state === 'error') { await page.getByLabel('Ваша відповідь').fill('жаба'); await page.getByRole('button', { name: 'Перевірити' }).click(); if (state === 'pending') await expect(page.getByText('Перевіряємо відповідь…')).toBeVisible(); else await expect(page.getByText(/Перемкніть розкладку/)).toBeVisible(); }
  else await expect(page.getByText(state === 'neutralized' ? 'Передали на перевірку' : 'To żaba.')).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await expect(page).toHaveScreenshot(`${theme}-recall-${state}.png`, { fullPage: true });
  release();
});

 test('кнопка браузера назад не закриває заняття під час відповіді', async ({ page }) => {
  await page.route('https://telegram.org/**', route => route.fulfill({ contentType: 'text/javascript', body: 'window.Telegram={WebApp:{initData:"fixture",colorScheme:"light",version:"6.0"}};' }));
  await page.route('**/api/v1/learners/me', route => route.fulfill({ json: learner }));
  await page.route('**/api/v1/today', route => route.fulfill({ json: { learner_day: '2026-11-02', due_review_count: 1, recalled_words_count: 0, completed_sessions_last_7_days: 0, has_active_session: false, course_state: 'active', course_replenished_notice: false, new_words_available_today: true, extra_actions: { plan_done: false, extra_new_word: { available: false, reason: 'plan_not_done' }, extra_practice: { available: false, reason: 'plan_not_done' } } } }));
  await page.route('**/api/v1/telemetry/client-timings', route => route.fulfill({ status: 204 }));
  await page.route('**/api/v1/sessions', route => route.fulfill({ json: session, status: 201 }));
  await page.route('**/api/v1/sessions/current', route => route.fulfill({ json: session }));
  let release!: () => void; const pending = new Promise<void>(resolve => { release = resolve; });
  await page.route('**/answer', async route => { await pending; await route.fulfill({ status: 400, json: { code: 'answer.wrong_layout' } }); });
  await page.goto('/today'); await page.getByRole('button', { name: 'Почати заняття' }).click(); await page.getByLabel('Ваша відповідь').fill('жаба'); await page.getByRole('button', { name: 'Перевірити' }).click();
  await expect(page.getByText('Перевіряємо відповідь…')).toBeVisible(); await page.goBack(); await expect(page).toHaveURL(/\/session$/); await expect(page.getByText('Перевіряємо відповідь…')).toBeVisible(); release(); await expect(page.getByText(/Перемкніть розкладку/)).toBeVisible();
});
