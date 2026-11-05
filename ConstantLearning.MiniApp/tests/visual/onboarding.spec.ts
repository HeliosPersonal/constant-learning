import { expect, test } from '@playwright/test';
import type { components } from '../../src/api/schema';
import { todaySummary } from '../fixtures/today';

const learner: components['schemas']['Learner'] = { id: '0192f0a0-0000-7000-8000-000000000001', first_name: 'Тест', time_zone: 'Europe/Warsaw', time_zone_confirmed: true, daily_new_words_limit: 5, pending_daily_new_words_limit: null, reminder: { local_time: '19:00', days_of_week: ['mon'] }, active_target_language: 'pl', onboarded_at: '2026-11-01T09:00:00Z' };
const notOnboarded: components['schemas']['Error'] = { code: 'learner.not_onboarded', message: 'Спершу налаштуйте тренер.' };

for (const theme of ['light', 'dark'] as const) {
  test(`${theme}: онбординг, валідація та збереження`, async ({ page }) => {
    const errors: string[] = [];
    page.on('pageerror', error => errors.push(error.message));
    page.on('console', message => {
      const expectedNotOnboarded = message.location().url.endsWith('/api/v1/learners/me') && message.text().includes('404 (Not Found)');
      if (message.type() === 'error' && !expectedNotOnboarded) errors.push(message.text());
    });
    await page.route('https://telegram.org/**', route => route.fulfill({ contentType: 'text/javascript', body: '' }));
    await page.route('**/api/v1/learners/me', route => route.fulfill({ status: 404, json: notOnboarded }));
    await page.route('**/api/v1/today', route => route.fulfill({ json: todaySummary }));
    await page.route('**/api/v1/telemetry/client-timings', route => route.fulfill({ status: 204 }));
    let release!: () => void;
    const pending = new Promise<void>(resolve => { release = resolve; });
    let postCount = 0;
    await page.route('**/api/v1/learners/me/onboarding', async route => {
      postCount++;
      await pending;
      await route.fulfill({ status: 201, json: learner });
    });
    await page.addInitScript(theme => localStorage.setItem('constant-learning.theme.v1', theme), theme);
    await page.goto('/');
    await expect(page.getByRole('heading', { name: 'Ваш темп. Ваші слова.' })).toBeVisible();
    await expect(page.getByRole('link', { name: /Запит на видалення/ })).toHaveAttribute('href', 'mailto:privacy@example.test');
    await page.screenshot({ path: `artifacts/onboarding-${theme}-default.png`, fullPage: true });
    await expect(page).toHaveScreenshot(`${theme}-onboarding.png`, { fullPage: true });
    await page.locator('label').filter({ has: page.getByLabel('Нагадувати про заняття') }).click();
    await expect(page.getByLabel('Нагадувати про заняття')).toBeChecked();
    await page.getByRole('button', { name: 'Почати навчання' }).click();
    await expect(page.getByText('Виберіть хоча б один день або вимкніть нагадування.')).toBeVisible();
    await page.getByLabel('Час нагадування').focus();
    await page.screenshot({ path: `artifacts/onboarding-${theme}-error.png`, fullPage: true });
    await expect(page).toHaveScreenshot(`${theme}-onboarding-error.png`, { fullPage: true });
    await page.getByLabel('Понеділок').check();
    await page.getByRole('button', { name: 'Почати навчання' }).click();
    await expect(page.getByRole('button', { name: 'Зберігаємо налаштування…' })).toBeDisabled();
    await page.screenshot({ path: `artifacts/onboarding-${theme}-pending.png`, fullPage: true });
    await expect(page).toHaveScreenshot(`${theme}-onboarding-pending.png`, { fullPage: true });
    release();
    await expect(page.getByRole('heading', { name: 'Сьогодні' })).toBeVisible();
    await expect(page).toHaveURL(/\/today$/);
    expect(postCount).toBe(1);
    expect(errors).toEqual([]);
  });
}
