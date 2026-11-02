import { expect, test, type Page } from '@playwright/test';
import type { components } from '../../src/api/schema';
import { todaySummary } from '../fixtures/today';

const learner: components['schemas']['Learner'] = { id: '0192f0a0-0000-7000-8000-000000000001', time_zone: 'Europe/Kyiv', time_zone_confirmed: true, daily_new_words_limit: 5, pending_daily_new_words_limit: 10, reminder: { local_time: '19:00', days_of_week: ['mon'] }, onboarded_at: '2026-11-01T09:00:00Z' };

async function prepare(page: Page, theme: string, profile = learner) {
  await page.route('https://telegram.org/**', route => route.fulfill({ contentType: 'text/javascript', body: `window.Telegram={WebApp:{initData:"fixture=today",colorScheme:"${theme}",version:"6.0"}};` }));
  await page.addInitScript(theme => localStorage.setItem('constant-learning.theme.v1', theme), theme);
  await page.route('**/api/v1/learners/me', route => route.fulfill({ json: profile }));
  const timings: unknown[] = [];
  await page.route('**/api/v1/telemetry/client-timings', async route => { timings.push(route.request().postDataJSON()); await route.fulfill({ status: 204 }); });
  return timings;
}

for (const theme of ['light', 'dark'] as const) {
  test(`${theme}: прогрес, пропозиція поясу, клавіатура й одноразова готовність`, async ({ page }) => {
    const timings = await prepare(page, theme);
    const errors: string[] = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.route('**/api/v1/today', route => route.fulfill({ json: { ...todaySummary, course_replenished_notice: true } }));
    let sessions = 0;
    await page.route('**/api/v1/sessions', route => { sessions++; return route.abort(); });
    await page.goto('/today');
    const start = page.getByRole('button', { name: 'Почати заняття' });
    await expect(start).toBeEnabled(); await expect(page.getByRole('heading', { name: 'Курс поповнено' })).toBeVisible();
    await expect.poll(() => timings.length).toBe(1); expect(timings[0]).toEqual({ metric: 'today_ready_ms', value_ms: expect.any(Number) });
    await start.focus();
    await page.screenshot({ path: `artifacts/today-${theme}-progress.png`, fullPage: true });
    await expect(page).toHaveScreenshot(`${theme}-today-progress.png`, { fullPage: true });
    await page.keyboard.press('Enter'); await expect(page.getByText('Заняття зараз недоступне. Спробуйте відкрити тренер пізніше.')).toBeVisible(); expect(sessions).toBe(0);
    await page.getByRole('button', { name: 'Залишити збережений пояс' }).click(); await expect(page.getByRole('heading', { name: 'У вас інший місцевий час?' })).toHaveCount(0);
    await page.getByRole('button', { name: 'Налаштування' }).click(); await expect(page.getByRole('heading', { name: 'Налаштування' })).toBeVisible();
    await page.getByRole('button', { name: '← До «Сьогодні»' }).click(); await expect(start).toBeEnabled(); await expect(page.getByRole('heading', { name: 'Курс поповнено' })).toHaveCount(0); expect(timings).toHaveLength(1);
    await page.setViewportSize({ width: 320, height: 740 }); expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
    expect(errors).toEqual([]);
  });

  test(`${theme}: активне заняття та завершений порожній курс`, async ({ page }) => {
    const timings = await prepare(page, theme, { ...learner, time_zone: 'Europe/Warsaw' });
    let completed = false;
    let active = false;
    let sessions = 0;
    await page.route('**/api/v1/sessions{,/current}', route => { sessions++; return route.abort(); });
    await page.route('**/api/v1/today', route => route.fulfill({ json: completed ? { ...todaySummary, due_review_count: 0, course_state: 'completed', has_active_session: active } : { ...todaySummary, has_active_session: true } }));
    await page.goto('/today'); await expect(page.getByRole('button', { name: 'Продовжити заняття' })).toBeEnabled();
    await expect.poll(() => timings.length).toBe(1);
    await expect(page).toHaveScreenshot(`${theme}-today-active.png`, { fullPage: true });
    completed = true; await page.reload(); await expect(page.getByRole('heading', { name: 'Базовий курс завершено' })).toBeVisible();
    await expect(page.getByText('На зараз повторень немає. Завітайте пізніше.')).toBeVisible();
    await expect(page.getByRole('button', { name: 'На сьогодні все' })).toBeDisabled(); expect(sessions).toBe(0);
    await expect.poll(() => timings.length).toBe(2);
    await expect(page).toHaveScreenshot(`${theme}-today-completed.png`, { fullPage: true });
    active = true; await page.reload(); await expect(page.getByRole('button', { name: 'Продовжити заняття' })).toBeEnabled();
    await expect(page.getByRole('button', { name: 'На сьогодні все' })).toHaveCount(0); await expect.poll(() => timings.length).toBe(3);
    await expect(page).toHaveScreenshot(`${theme}-today-completed-active.png`, { fullPage: true });
  });

  test(`${theme}: очікування, помилка й явне відновлення`, async ({ page }) => {
    const timings = await prepare(page, theme, { ...learner, time_zone: 'Europe/Warsaw' });
    let release!: () => void;
    const pending = new Promise<void>(resolve => { release = resolve; });
    let fail = true;
    await page.route('**/api/v1/today', async route => { await pending; await route.fulfill(fail ? { status: 503, json: { code: 'server.unavailable' } } : { json: todaySummary }); });
    await page.goto('/today'); await expect(page.getByText('Збираємо ваш прогрес…')).toBeVisible(); expect(timings).toHaveLength(0);
    // SVG SMIL не зупиняється параметром animations: disabled у Playwright.
    await page.locator('.loading-state svg').evaluate(svg => { (svg as SVGSVGElement).pauseAnimations(); (svg as SVGSVGElement).setCurrentTime(0); });
    await expect(page).toHaveScreenshot(`${theme}-today-loading.png`, { fullPage: true });
    release(); await expect(page.getByText('Не вдалося завантажити прогрес. Спробуйте ще раз.')).toBeVisible(); expect(timings).toHaveLength(0);
    await expect(page).toHaveScreenshot(`${theme}-today-error.png`, { fullPage: true });
    fail = false; await page.getByRole('button', { name: 'Спробувати ще раз' }).click(); await expect(page.getByRole('button', { name: 'Почати заняття' })).toBeEnabled(); await expect.poll(() => timings.length).toBe(1);
  });

  test(`${theme}: пояс змінюється лише після підтвердження, помилка та pending`, async ({ page }) => {
    await prepare(page, theme);
    let profile = learner;
    await page.route('**/api/v1/learners/me', route => route.fulfill({ json: profile }));
    await page.route('**/api/v1/today', route => route.fulfill({ json: todaySummary }));
    let patches = 0;
    let release!: () => void;
    const pending = new Promise<void>(resolve => { release = resolve; });
    await page.route('**/api/v1/learners/me/settings', async route => {
      patches++; expect(route.request().postDataJSON()).toEqual({ time_zone: 'Europe/Warsaw' });
      if (patches === 1) await route.fulfill({ status: 503, json: { code: 'server.unavailable' } });
      else { await pending; profile = { ...learner, time_zone: 'Europe/Warsaw' }; await route.fulfill({ json: profile }); }
    });
    await page.goto('/today'); const confirm = page.getByRole('button', { name: 'Підтвердити пояс пристрою' }); await expect(confirm).toBeVisible(); expect(patches).toBe(0);
    await confirm.click(); await expect(page.getByText(/Збережений пояс не змінився/)).toBeVisible();
    await expect(page).toHaveScreenshot(`${theme}-today-zone-error.png`, { fullPage: true });
    await confirm.click(); await expect(page.getByRole('button', { name: 'Зберігаємо часовий пояс…' })).toBeDisabled();
    await expect(page).toHaveScreenshot(`${theme}-today-zone-pending.png`, { fullPage: true });
    release(); await expect(page.getByText('Часовий пояс збережено.')).toBeVisible(); await expect(page.getByRole('heading', { name: 'У вас інший місцевий час?' })).toHaveCount(0); expect(patches).toBe(2);
  });
}
