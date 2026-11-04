import { expect, test, type Page } from '@playwright/test';
import { summary, weakWord, wordCard, wordsPage } from '../fixtures/stats';
import type { LearnedWordStat } from '../../src/features/stats/api';

const learner = { id: '0192f0a0-0000-7000-8000-000000000001', time_zone: 'Europe/Kyiv', time_zone_confirmed: true, daily_new_words_limit: 5, pending_daily_new_words_limit: null, reminder: { local_time: '19:00', days_of_week: ['mon'] }, onboarded_at: '2026-11-01T09:00:00Z' };
const extra: LearnedWordStat[] = [
  { ...weakWord, entry_id: '0192f0a0-0000-7000-8000-0000000000e3', lemma_pl: 'urząd', transcription_uk: 'ужонд', translation_uk: 'установа', correct_count: 3, incorrect_count: 4, status: 'review', is_weak: true },
  { ...weakWord, entry_id: '0192f0a0-0000-7000-8000-0000000000e4', lemma_pl: 'wiedzieć', transcription_uk: 'вєдзєчь', translation_uk: 'знати, вміти розуміти й пам’ятати довгий переклад', correct_count: 7, incorrect_count: 1, status: 'recalled', is_weak: false },
];

async function prepare(page: Page, theme: string) {
  await page.route('https://telegram.org/**', route => route.fulfill({ contentType: 'text/javascript', body: `window.Telegram={WebApp:{initData:"fixture=stats",colorScheme:"${theme}",version:"6.0"}};` }));
  await page.addInitScript(theme => localStorage.setItem('constant-learning.theme.v1', theme), theme);
  await page.route('**/api/v1/learners/me', route => route.fulfill({ json: learner }));
  await page.route('**/api/v1/telemetry/client-timings', route => route.fulfill({ status: 204 }));
  await page.route('**/api/v1/stats/summary', route => route.fulfill({ json: summary }));
  await page.route(/\/api\/v1\/stats\/words(\?.*)?$/, route => route.fulfill({ json: { ...wordsPage, items: [...wordsPage.items, ...extra], has_next: true, next_cursor: 'next' } }));
  await page.route(/\/api\/v1\/stats\/words\/[^?]+/, route => route.fulfill({ json: wordCard }));
}

for (const theme of ['light', 'dark'] as const) {
  test(`${theme}: мої слова — список і картка слова`, async ({ page }) => {
    const errors: string[] = [];
    page.on('pageerror', error => errors.push(error.message));
    await prepare(page, theme);
    await page.goto('/stats');
    await expect(page.getByRole('heading', { name: 'Мої слова', exact: true })).toBeVisible();
    await expect(page.getByRole('button', { name: /chata/ })).toBeVisible();
    await expect(page).toHaveScreenshot(`${theme}-stats-list.png`, { fullPage: true });
    await page.getByRole('button', { name: /chata/ }).click();
    await expect(page.getByRole('heading', { name: 'chata' })).toBeVisible();
    await expect(page).toHaveScreenshot(`${theme}-stats-word.png`, { fullPage: true });
    await page.setViewportSize({ width: 320, height: 740 });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
    expect(errors).toEqual([]);
  });
}
