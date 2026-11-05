import { expect, test } from '@playwright/test';
import type { components } from '../../src/api/schema';
import { todaySummary } from '../fixtures/today';

type Learner = components['schemas']['Learner'];
type SettingsUpdate = components['schemas']['LearnerSettingsUpdate'];
const original: Learner = { id: '0192f0a0-0000-7000-8000-000000000001', first_name: 'Тест', time_zone: 'Europe/Warsaw', time_zone_confirmed: true, daily_new_words_limit: 5, pending_daily_new_words_limit: 10, reminder: { local_time: '19:00', days_of_week: ['mon', 'fri'] }, active_target_language: 'pl', onboarded_at: '2026-11-01T09:00:00Z' };

for (const theme of ['light', 'dark'] as const) {
  test(`${theme}: налаштування, локальна тема й автозбереження`, async ({ page }) => {
    let profile = { ...original };
    const errors: string[] = [];
    const patches: SettingsUpdate[] = [];
    page.on('pageerror', error => errors.push(error.message));
    page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
    await page.route('https://telegram.org/**', route => route.fulfill({ contentType: 'text/javascript', body: `const callbacks=new Set();window.Telegram={WebApp:{initData:"fixture=settings",colorScheme:"${theme}",version:"6.0",onEvent:(_,callback)=>callbacks.add(callback),offEvent:(_,callback)=>callbacks.delete(callback)}};window.emitFixtureTheme=(value)=>{window.Telegram.WebApp.colorScheme=value;callbacks.forEach(callback=>callback())};` }));
    await page.route('**/api/v1/learners/me', route => route.fulfill({ json: profile }));
    await page.route('**/api/v1/learners/me/access', route => route.fulfill({ json: { content_editor: true } }));
    await page.route('**/api/v1/today', route => route.fulfill({ json: todaySummary }));
    await page.route('**/api/v1/telemetry/client-timings', route => route.fulfill({ status: 204 }));
    let release!: () => void;
    const gate = new Promise<void>(resolve => { release = resolve; });
    await page.route('**/api/v1/learners/me/settings', async route => {
      const body = route.request().postDataJSON() as SettingsUpdate;
      patches.push(body);
      await gate;
      profile = { ...profile, pending_daily_new_words_limit: body.daily_new_words_limit ?? profile.pending_daily_new_words_limit, reminder: body.reminder === undefined ? profile.reminder : body.reminder };
      await route.fulfill({ json: profile });
    });
    await page.addInitScript(theme => { if (!sessionStorage.getItem('settings.seeded')) { localStorage.setItem('constant-learning.theme.v1', theme); sessionStorage.setItem('settings.seeded', 'true'); } }, theme);
    await page.goto('/settings');
    await expect(page.getByRole('heading', { name: 'Налаштування', exact: true })).toBeVisible();
    await expect(page).toHaveURL(/\/settings$/);
    await expect(page.getByRole('button', { name: 'Редактор словника' })).toBeVisible();
    await page.screenshot({ path: `artifacts/settings-${theme}-default.png`, fullPage: true });
    await expect(page).toHaveScreenshot(`${theme}-settings.png`, { fullPage: true });
    const other = theme === 'light' ? 'dark' : 'light';
    await page.getByRole('radio', { name: theme === 'light' ? /Темна/ : /Світла/ }).check();
    await expect(page.locator('html')).toHaveAttribute('data-theme', other);
    await page.getByRole('radio', { name: theme === 'light' ? /Світла/ : /Темна/ }).check();
    await expect(page.locator('html')).toHaveAttribute('data-theme', theme);
    await page.evaluate(() => { (window as Window & { emitFixtureTheme?: (value: string) => void }).emitFixtureTheme?.(document.documentElement.dataset.theme === 'light' ? 'dark' : 'light'); });
    await expect(page.locator('html')).toHaveAttribute('data-theme', theme);
    expect(patches).toEqual([]);
    // Поки днів немає, зміни не надсилаються: валідація блокує автозбереження.
    await page.getByLabel('Понеділок').uncheck(); await page.getByLabel('П’ятниця').uncheck();
    for (let i = 0; i < 7; i++) await page.getByRole('button', { name: 'Менше' }).click();
    await page.getByLabel('Час нагадування').fill('18:30');
    await expect(page.getByText('Виберіть хоча б один день або вимкніть нагадування.')).toBeVisible();
    await page.getByLabel('Час нагадування').focus();
    await page.screenshot({ path: `artifacts/settings-${theme}-error.png`, fullPage: true });
    await expect(page).toHaveScreenshot(`${theme}-settings-error.png`, { fullPage: true });
    expect(patches).toEqual([]);
    await page.getByLabel('Середа').check();
    await expect(page.getByText('Зберігаємо…')).toBeVisible();
    await page.screenshot({ path: `artifacts/settings-${theme}-pending.png`, fullPage: true });
    await expect(page).toHaveScreenshot(`${theme}-settings-pending.png`, { fullPage: true });
    release();
    await expect(page.getByText('Збережено ✓')).toBeVisible();
    expect(patches).toEqual([{ daily_new_words_limit: 3, reminder: { local_time: '18:30', days_of_week: ['wed'] } }]);
    await expect(page.getByText('Сьогодні:', { exact: false })).toContainText('5 нових слів');
    await expect(page.getByText('З наступної доби:', { exact: false })).toContainText('3 нові слова');
    await page.reload();
    await expect(page.locator('html')).toHaveAttribute('data-theme', theme);
    await expect(page.getByRole('group', { name: 'Нових слів на день' }).getByRole('status')).toContainText('3');
    await page.getByRole('button', { name: '← До «Сьогодні»' }).click();
    await expect(page.getByRole('heading', { name: 'Сьогодні', exact: true })).toBeVisible();
    await expect(page).toHaveURL(/\/today$/);
    expect(errors).toEqual([]);
  });
}
