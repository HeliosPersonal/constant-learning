import { expect, test, type Locator } from '@playwright/test';
import type { components } from '../../src/api/schema';

const learner: components['schemas']['Learner'] = { id: '0192f0a0-0000-7000-8000-000000000001', first_name: 'Тест', time_zone: 'Europe/Warsaw', time_zone_confirmed: true, daily_new_words_limit: 5, pending_daily_new_words_limit: null, reminder: null, onboarded_at: '2026-11-01T09:00:00Z' };
const entry: components['schemas']['DictionaryEntryCard'] = { entry_id: '0192f0a0-0000-7000-8000-0000000000e1', lemma_pl: 'urząd', part_of_speech: 'noun', gender: 'm', translations_uk: [{ kind: 'canonical', text_uk: 'державна установа' }], pronunciation_uk: 'ужонд', example_pl: 'Idę do urzędu.', example_uk: 'Я йду до державної установи.', word_status: 'review' };
const accepted: components['schemas']['ContentReportAccepted'] = { id: '0192f0a0-0000-7000-8000-0000000000d2', status: 'pending_verification', message: 'Дякуємо, передали на перевірку' };
const results: components['schemas']['DictionarySearchPage'] = { items: [{ entry_id: entry.entry_id, lemma_pl: entry.lemma_pl, part_of_speech: 'noun', short_meaning_uk: 'державна установа', matched_form: 'urzedzie', word_status: 'review' }], has_next: false, has_prev: false, next_cursor: null };

async function expectMessageUnobscured(dialog: Locator) {
  const geometry = () => dialog.evaluate(element => {
    const message = element.querySelector('.content-report-message')!.getBoundingClientRect();
    const toolbar = element.querySelector('.content-report-toolbar')!.getBoundingClientRect();
    const viewportBottom = Math.min(element.getBoundingClientRect().bottom, window.innerHeight);
    return { topGap: message.top - toolbar.bottom, bottomGap: viewportBottom - message.bottom };
  });
  await expect.poll(async () => (await geometry()).topGap).toBeGreaterThanOrEqual(8);
  await expect.poll(async () => (await geometry()).bottomGap).toBeGreaterThanOrEqual(8);
}

for (const theme of ['light', 'dark'] as const) {
  test(`${theme}: повідомлення з картки, ліміт, повтор та відсутнє слово`, async ({ page }) => {
    const errors: string[] = []; const bodies: components['schemas']['ContentReportCreate'][] = []; const chunks: string[] = [];
    page.on('pageerror', error => errors.push(error.message));
    page.on('console', message => { if (message.type() === 'error' && !message.text().includes('429')) errors.push(message.text()); });
    page.on('request', request => { if (request.url().includes('/report/ContentReportDialog')) chunks.push(request.url()); });
    await page.route('https://telegram.org/**', route => route.fulfill({ contentType: 'text/javascript', body: `window.Telegram={WebApp:{initData:"fixture=report",colorScheme:"${theme}",version:"6.0"}};` }));
    await page.route('**/api/v1/learners/me', route => route.fulfill({ json: learner }));
    await page.route('**/api/v1/dictionary/search?*', route => route.fulfill({ json: new URL(route.request().url()).searchParams.get('q')?.includes('НЕМАЄ') ? { ...results, items: [] } : results }));
    await page.route('**/api/v1/dictionary/entries/*', route => route.fulfill({ json: entry }));
    let release!: () => void; const gate = new Promise<void>(resolve => { release = resolve; });
    await page.route('**/api/v1/content-reports', async route => {
      const body = route.request().postDataJSON() as components['schemas']['ContentReportCreate']; bodies.push(body);
      if (bodies.length === 1) await route.fulfill({ status: 429, json: { code: 'report.daily_limit_reached', message: 'Ліміт вичерпано.' } });
      else { if (bodies.length === 2) await gate; await route.fulfill({ status: 202, json: { ...accepted, status: body.origin === 'missing_word' ? 'in_editor_queue' : 'pending_verification' } }); }
    });
    await page.addInitScript(theme => localStorage.setItem('constant-learning.theme.v1', theme), theme);
    await page.goto('/dictionary'); const search = page.getByRole('searchbox'); await search.fill('urzedzie'); await page.getByRole('button', { name: /urząd/ }).click(); await expect(page.getByRole('heading', { name: 'urząd', exact: true })).toBeVisible(); expect(chunks).toEqual([]);
    await page.getByRole('button', { name: 'Повідомити про неточність' }).click(); const dialog = page.getByRole('dialog'); await expect(dialog).toBeVisible(); expect(chunks.length).toBeGreaterThan(0);
    await expect(dialog.getByRole('heading', { name: 'Повідомити про неточність' })).toBeFocused();
    const background = await dialog.evaluate(element => getComputedStyle(element).backgroundColor); expect(background).toBe(theme === 'light' ? 'rgb(255, 251, 239)' : 'rgb(52, 63, 68)');
    await expect(page).toHaveScreenshot(`${theme}-report-form.png`);
    await dialog.getByRole('button', { name: 'Надіслати на перевірку' }).click();
    await expect(dialog.getByRole('alert')).toHaveText('!Виберіть поле, якого стосується повідомлення.');
    await expectMessageUnobscured(dialog); expect(bodies).toHaveLength(0);
    await dialog.getByLabel('Що потрібно перевірити?').selectOption('translation'); await dialog.getByLabel(/Запропоноване значення/).fill('державна установа'); await dialog.getByLabel(/Коментар \(необов’язково\)/).fill('<b>Перевірте цей переклад</b>'); expect(await dialog.locator('b').count()).toBe(0);
    await dialog.getByRole('button', { name: 'Надіслати на перевірку' }).click(); await expect(dialog.getByRole('alert')).toContainText('після 00:00 за вашим часом'); await expect(dialog.getByLabel('Що потрібно перевірити?')).toHaveValue('translation'); await expect(dialog.getByLabel(/Коментар \(необов’язково\)/)).toHaveValue('<b>Перевірте цей переклад</b>'); expect(bodies).toHaveLength(1);
    await expectMessageUnobscured(dialog);
    await page.screenshot({ path: `artifacts/content-report-${theme}-quota.png` }); await expect(page).toHaveScreenshot(`${theme}-report-quota.png`);
    await dialog.getByRole('button', { name: 'Назад до словника' }).click(); await expect(page.getByRole('heading', { name: 'urząd', exact: true })).toBeVisible(); await expect(search).toHaveValue('urzedzie');
    await page.getByRole('button', { name: 'Повідомити про неточність' }).click(); await expect(dialog.getByLabel(/Запропоноване значення/)).toHaveValue('державна установа');
    await dialog.getByRole('button', { name: 'Надіслати на перевірку' }).click(); await expect(dialog.getByRole('button', { name: 'Надсилаємо повідомлення…' })).toBeDisabled(); await expect(dialog.getByRole('button', { name: 'Назад до словника' })).toBeDisabled(); await page.keyboard.press('Escape'); await expect(dialog).toBeVisible();
    await expect(page).toHaveScreenshot(`${theme}-report-pending.png`); release(); await expect(dialog.getByRole('status')).toHaveText('✓Дякуємо, передали на перевірку'); expect(bodies).toHaveLength(2); expect(bodies[1]).toEqual(bodies[0]);
    await expect(dialog.getByLabel('Результат повідомлення')).toBeFocused();
    await expectMessageUnobscured(dialog);
    await page.screenshot({ path: `artifacts/content-report-${theme}-accepted.png` }); await expect(page).toHaveScreenshot(`${theme}-report-accepted.png`);
    await dialog.getByRole('button', { name: 'Повернутися до словника' }).click(); await expect(page.getByText('державна установа', { exact: true })).toBeVisible(); await expect(page.getByRole('button', { name: 'Повідомити про неточність' })).toBeFocused();
    await page.getByRole('button', { name: 'Повідомити про неточність' }).click(); await dialog.getByRole('button', { name: 'Надіслати на перевірку' }).click(); await expect(dialog.getByRole('status')).toContainText('Дякуємо, передали на перевірку'); expect(bodies).toHaveLength(3); expect(bodies[2]).toEqual(bodies[0]); await dialog.getByRole('button', { name: 'Повернутися до словника' }).click();
    await search.fill('  НЕМАЄ słowa  '); await page.getByRole('button', { name: 'Повідомити про відсутній запис' }).click(); await expect(dialog.getByRole('heading')).toHaveText('Повідомити про відсутній запис'); await expect(dialog.getByRole('combobox')).toHaveCount(0);
    await expect(page).toHaveScreenshot(`${theme}-report-missing.png`);
    await page.setViewportSize({ width: 320, height: 700 }); expect(await dialog.evaluate(element => element.scrollWidth > element.clientWidth)).toBe(false); await page.screenshot({ path: `artifacts/content-report-${theme}-compact.png` });
    await dialog.getByLabel(/Коментар \(необов’язково\)/).fill('Немає у словнику'); await dialog.getByRole('button', { name: 'Надіслати на перевірку' }).click(); await expect(dialog.getByRole('status')).toContainText('Дякуємо, передали на перевірку'); expect(bodies[3]).toEqual({ origin: 'missing_word', entry_id: null, field: null, proposed_value: null, comment: 'Немає у словнику', search_query: '  НЕМАЄ słowa  ' });
    await dialog.getByRole('button', { name: 'Повернутися до словника' }).click(); await expect(search).toHaveValue('  НЕМАЄ słowa  '); expect(errors).toEqual([]);
  });
}
