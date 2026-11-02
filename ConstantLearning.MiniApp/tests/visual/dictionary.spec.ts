import { expect, test } from '@playwright/test';
import type { components } from '../../src/api/schema';

const firstId = '0192f0a0-0000-7000-8000-0000000000e1';
const secondId = '0192f0a0-0000-7000-8000-0000000000e2';
const learner: components['schemas']['Learner'] = { id: '0192f0a0-0000-7000-8000-000000000001', first_name: 'Тест', time_zone: 'Europe/Warsaw', time_zone_confirmed: true, daily_new_words_limit: 5, pending_daily_new_words_limit: null, reminder: null, onboarded_at: '2026-11-01T09:00:00Z' };
const item: components['schemas']['DictionarySearchItem'] = { entry_id: firstId, lemma_pl: 'urząd', part_of_speech: 'noun', short_meaning_uk: 'державна установа', matched_form: 'urzedzie', word_status: 'review' };
const results: components['schemas']['DictionarySearchPage'] = { items: [item, { ...item, entry_id: secondId, lemma_pl: 'urządzać', part_of_speech: 'verb', short_meaning_uk: 'влаштовувати', word_status: 'not_started' }], has_next: true, has_prev: false, next_cursor: secondId };
const card: components['schemas']['DictionaryEntryCard'] = { entry_id: firstId, lemma_pl: 'urząd', part_of_speech: 'noun', gender: 'm', translations_uk: [{ kind: 'canonical', text_uk: 'державна установа' }, { kind: 'local', text_uk: 'ужонд' }], pronunciation_uk: 'ужонд', example_pl: 'Idę do urzędu.', example_uk: 'Я йду до державної установи.', word_status: 'review' };

for (const theme of ['light', 'dark'] as const) {
  test(`${theme}: словник, картка, loading, error та порожній стан`, async ({ page }) => {
    const errors: string[] = [];
    const requests: string[] = [];
    const chunks: string[] = [];
    page.on('pageerror', error => errors.push(error.message));
    page.on('console', message => { if (message.type() === 'error' && !message.text().includes('503')) errors.push(message.text()); });
    page.on('request', request => { if (request.url().includes('/features/dictionary/search/Dictionary')) chunks.push(request.url()); });
    await page.route('https://telegram.org/**', route => route.fulfill({ contentType: 'text/javascript', body: `window.Telegram={WebApp:{initData:"fixture=dictionary",colorScheme:"${theme}",version:"6.0"}};` }));
    await page.route('**/api/v1/learners/me', route => route.fulfill({ json: learner }));
    let release!: () => void;
    const gate = new Promise<void>(resolve => { release = resolve; });
    await page.route('**/api/v1/dictionary/search?*', async route => {
      const url = new URL(route.request().url()); const q = url.searchParams.get('q'); requests.push(q ?? '');
      if (q === 'pending') { await gate; await route.fulfill({ json: results }); }
      else if (q === 'error') await route.fulfill({ status: 503, json: { code: 'server.unavailable', message: 'Недоступно' } });
      else if (q === 'немає') await route.fulfill({ json: { ...results, items: [], has_next: false, next_cursor: null } });
      else await route.fulfill({ json: results });
    });
    await page.route('**/api/v1/dictionary/entries/*', route => route.fulfill({ json: card }));
    await page.addInitScript(theme => localStorage.setItem('constant-learning.theme.v1', theme), theme);
    await page.goto('/today'); await expect(page.getByRole('heading', { name: 'Сьогодні', exact: true })).toBeVisible(); expect(chunks).toEqual([]);
    await page.getByRole('button', { name: 'Словник', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Словник', exact: true })).toBeVisible(); await expect(page).toHaveURL(/\/dictionary$/); expect(chunks.length).toBeGreaterThan(0);
    await expect(page).toHaveScreenshot(`${theme}-dictionary-initial.png`, { fullPage: true });
    const search = page.getByRole('searchbox', { name: 'Слово для пошуку' }); await search.focus(); await search.pressSequentially('ur'); await expect(page.getByRole('button', { name: /^urząd / })).toBeVisible();
    expect(requests).toContain('u'); expect(requests).toContain('ur');
    await page.screenshot({ path: `artifacts/dictionary-${theme}-results.png`, fullPage: true }); await expect(page).toHaveScreenshot(`${theme}-dictionary-results.png`, { fullPage: true });
    await page.keyboard.press('Tab'); await page.keyboard.press('Tab'); await expect(page.getByRole('button', { name: /^urząd / })).toBeFocused(); await page.keyboard.press('Enter');
    await expect(page.getByRole('heading', { name: 'urząd', exact: true })).toBeVisible(); await expect(page.getByRole('heading', { name: 'urząd', exact: true })).toBeFocused();
    await page.screenshot({ path: `artifacts/dictionary-${theme}-card.png`, fullPage: true }); await expect(page).toHaveScreenshot(`${theme}-dictionary-card.png`, { fullPage: true });
    await page.getByRole('button', { name: '← До результатів' }).click(); await expect(page.getByRole('heading', { name: 'Результати' })).toBeFocused(); await expect(search).toHaveValue('ur');
    await search.fill('pending'); await expect(page.getByText('Шукаємо слово…')).toBeVisible();
    await expect(page).toHaveScreenshot(`${theme}-dictionary-pending.png`, { fullPage: true }); release(); await expect(page.getByRole('button', { name: /^urząd / })).toBeVisible();
    await search.fill('error'); await expect(page.getByRole('alert')).toBeVisible();
    await expect(page).toHaveScreenshot(`${theme}-dictionary-error.png`, { fullPage: true });
    await search.fill('немає'); await expect(page.getByRole('heading', { name: 'Не знайшли слово' })).toBeVisible();
    await page.getByRole('button', { name: 'Повідомити про відсутній запис' }).click(); await expect(page.getByText(/Форма повідомлення ще недоступна/)).toBeVisible();
    await page.screenshot({ path: `artifacts/dictionary-${theme}-empty.png`, fullPage: true }); await expect(page).toHaveScreenshot(`${theme}-dictionary-empty.png`, { fullPage: true });
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth); expect(overflow).toBe(false);
    const clearLabel = page.getByRole('button', { name: 'Очистити' }).locator('span').last();
    expect(await clearLabel.evaluate(element => element.scrollWidth <= element.clientWidth)).toBe(true);
    await page.setViewportSize({ width: 320, height: 700 });
    expect(await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth)).toBe(false);
    await expect(page.getByRole('button', { name: 'Повідомити про відсутній запис' })).toBeVisible();
    await page.screenshot({ path: `artifacts/dictionary-${theme}-compact.png`, fullPage: true });
    await page.reload(); await expect(page).toHaveURL(/\/dictionary$/); await expect(page.getByRole('heading', { name: 'Словник', exact: true })).toBeVisible(); await expect(page.locator('html')).toHaveAttribute('data-theme', theme);
    expect(errors).toEqual([]);
  });
}
