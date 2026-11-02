import { expect, test, type Page } from '@playwright/test';
import { answerChange, appliedPage, change, closedReport, disputeReport, missingReport, queuePage, report, rollbackResult } from '../fixtures/editor';

async function prepare(page: Page, theme: string) {
  await page.route('https://telegram.org/**', route => route.fulfill({ contentType: 'text/javascript', body: `window.Telegram={WebApp:{initData:"fixture=editor",colorScheme:"${theme}",version:"6.0"}};` }));
  await page.addInitScript(theme => localStorage.setItem('constant-learning.theme.v1', theme), theme);
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  const profileRequests: string[] = [];
  page.on('request', request => { if (request.url().endsWith('/learners/me')) profileRequests.push(request.url()); });
  return { errors, profileRequests };
}
async function applied(page: Page) {
  await page.route('**/api/v1/editor/content-reports?*', route => route.fulfill({ json: queuePage }));
  await page.goto('/editor'); await page.getByRole('button', { name: 'Застосовані зміни', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Відкотити зміну' })).toBeVisible();
}

for (const theme of ['light', 'dark'] as const) {
  test(`${theme}: черга, рішення, чернетка, помилка й очікування`, async ({ page }) => {
    const { errors, profileRequests } = await prepare(page, theme);
    let accepted = false;
    await page.route('**/api/v1/editor/content-reports?*', route => route.fulfill({ json: { ...queuePage, items: accepted ? [missingReport, disputeReport] : [report, missingReport, disputeReport] } }));
    let posts = 0;
    let release!: () => void; const pending = new Promise<void>(resolve => { release = resolve; });
    await page.route(`**/api/v1/editor/content-reports/${report.id}/decision`, async route => {
      posts++; expect(route.request().postDataJSON()).toEqual({ decision: 'approve', corrected_value: 'державний орган' });
      if (posts === 1) await route.fulfill({ status: 503, json: { code: 'server.unavailable' } });
      else { await pending; accepted = true; await route.fulfill({ json: closedReport(report.id) }); }
    });
    await page.goto('/editor'); const inspect = page.getByRole('button', { name: 'Розглянути' }).first(); await expect(inspect).toBeVisible();
    await page.screenshot({ path: `artifacts/editor-${theme}-queue.png`, fullPage: true }); await expect(page).toHaveScreenshot(`${theme}-editor-queue.png`, { fullPage: true });
    await inspect.focus(); await page.keyboard.press('Enter'); const dialog = page.getByRole('dialog');
    await expect(dialog.getByRole('heading', { level: 2 })).toBeFocused(); expect(await dialog.evaluate(element => element.parentElement === document.body)).toBe(true);
    await expect(page).toHaveScreenshot(`${theme}-editor-decision.png`);
    await dialog.getByRole('button', { name: 'Повернутися до черги' }).focus(); await page.keyboard.press('Tab'); await expect(dialog.getByRole('button', { name: 'Закрити' })).toBeFocused();
    const corrected = dialog.getByLabel(/Остаточне значення/); await corrected.fill('державний орган');
    await page.setViewportSize({ width: 320, height: 740 }); expect(await dialog.evaluate(element => element.scrollWidth <= element.clientWidth)).toBe(true); await page.setViewportSize({ width: 390, height: 844 });
    await dialog.getByRole('button', { name: 'Повернутися до черги' }).click(); await expect(inspect).toBeFocused(); await page.keyboard.press('Enter'); await expect(corrected).toHaveValue('державний орган');
    await dialog.getByRole('button', { name: 'Схвалити зміну' }).click(); await expect(dialog.getByRole('alert')).toContainText('Ваш вибір залишився на екрані'); await expect(dialog.locator('.editor-message')).toBeFocused();
    await expect(page).toHaveScreenshot(`${theme}-editor-decision-error.png`);
    await dialog.getByRole('button', { name: 'Схвалити зміну' }).click(); await expect(dialog.getByRole('button', { name: 'Зберігаємо рішення…' })).toBeDisabled(); await expect(dialog.getByRole('button', { name: 'Закрити' })).toBeDisabled(); await page.keyboard.press('Escape'); await expect(dialog).toBeVisible();
    await expect(page).toHaveScreenshot(`${theme}-editor-decision-pending.png`);
    release(); await expect(page.getByText('Зміну застосовано. Вона доступна в списку застосованих змін.')).toBeVisible(); await expect(dialog).toHaveCount(0); expect(posts).toBe(2);
    await page.getByRole('region', { name: 'Повідомлення: відсутнє слово' }).getByRole('button', { name: 'Розглянути' }).click(); await expect(dialog.getByRole('heading', { level: 2 })).toHaveText('Розглянути відсутнє слово'); await expect(dialog.getByRole('option', { name: 'Схвалити зміну' })).toHaveCount(0);
    await dialog.getByLabel('Рішення', { exact: true }).selectOption('reject'); await dialog.getByLabel('Причина відхилення').selectOption('phrase_not_lemma');
    await expect(page).toHaveScreenshot(`${theme}-editor-missing.png`);
    await page.keyboard.press('Escape'); await expect(dialog).toHaveCount(0);
    expect(profileRequests).toEqual([]); expect(errors).toEqual([]);
  });

  test(`${theme}: застосовані зміни, підтвердження, помилка й очікування відкату`, async ({ page }) => {
    const { errors } = await prepare(page, theme);
    let rolledBack = false;
    await page.route('**/api/v1/editor/applied-changes?*', route => route.fulfill({ json: { ...appliedPage, items: [{ ...change, rollback_allowed: !rolledBack }, answerChange] } }));
    let posts = 0; let release!: () => void; const pending = new Promise<void>(resolve => { release = resolve; });
    await page.route(`**/api/v1/editor/applied-changes/${change.id}/rollback`, async route => {
      posts++; if (posts === 1) await route.fulfill({ status: 503, json: { code: 'server.unavailable' } });
      else { await pending; rolledBack = true; await route.fulfill({ json: rollbackResult }); }
    });
    await applied(page); await expect(page.getByText('Прийнятий варіант відповіді')).toBeVisible(); await expect(page.getByText('Варіанта ще не було')).toBeVisible();
    await page.screenshot({ path: `artifacts/editor-${theme}-applied.png`, fullPage: true }); await expect(page).toHaveScreenshot(`${theme}-editor-applied.png`, { fullPage: true });
    const trigger = page.getByRole('button', { name: 'Відкотити зміну' }); await trigger.click(); const dialog = page.getByRole('dialog'); await expect(dialog.getByRole('heading', { level: 2 })).toBeFocused(); expect(posts).toBe(0);
    await expect(page).toHaveScreenshot(`${theme}-editor-rollback-confirm.png`);
    await dialog.getByRole('button', { name: 'Залишити без змін' }).click(); await expect(trigger).toBeFocused(); expect(posts).toBe(0); await page.keyboard.press('Enter');
    await dialog.getByRole('button', { name: 'Підтвердити відкат' }).click(); await expect(dialog.getByRole('alert')).toContainText('Нічого не підтверджено');
    await expect(page).toHaveScreenshot(`${theme}-editor-rollback-error.png`);
    await dialog.getByRole('button', { name: 'Підтвердити відкат' }).click(); await expect(dialog.getByRole('button', { name: 'Відновлюємо версію…' })).toBeDisabled(); await page.keyboard.press('Escape'); await expect(dialog).toBeVisible();
    await expect(page).toHaveScreenshot(`${theme}-editor-rollback-pending.png`);
    release(); await expect(page.getByText(/Попередню версію відновлено/)).toBeVisible(); await expect(page.getByRole('button', { name: 'Відкотити зміну' })).toHaveCount(0); expect(posts).toBe(2); expect(errors).toEqual([]);
  });

  test(`${theme}: застарілий відкат перечитує серверний список`, async ({ page }) => {
    await prepare(page, theme); let stale = false;
    await page.route('**/api/v1/editor/applied-changes?*', route => route.fulfill({ json: { ...appliedPage, items: [{ ...change, rollback_allowed: !stale }] } }));
    await page.route(`**/api/v1/editor/applied-changes/${change.id}/rollback`, async route => { stale = true; await route.fulfill({ status: 409, json: { code: 'editor.rollback_not_current' } }); });
    await applied(page); await page.getByRole('button', { name: 'Відкотити зміну' }).click(); await page.getByRole('button', { name: 'Підтвердити відкат' }).click();
    await expect(page.getByRole('dialog')).toHaveCount(0); await expect(page.getByText(/Відкотити можна лише поточну версію запису. Список оновлено/)).toBeVisible(); await expect(page.getByRole('button', { name: 'Відкат недоступний' })).toBeDisabled();
    await expect(page).toHaveScreenshot(`${theme}-editor-rollback-stale.png`, { fullPage: true });
  });

  test(`${theme}: loading, помилка читання й порожня черга`, async ({ page }) => {
    await prepare(page, theme); let release!: () => void; const pending = new Promise<void>(resolve => { release = resolve; }); let fail = true;
    await page.route('**/api/v1/editor/content-reports?*', async route => { await pending; await route.fulfill(fail ? { status: 503, json: { code: 'server.unavailable' } } : { json: { ...queuePage, items: [] } }); });
    await page.goto('/editor'); await expect(page.getByText('Завантажуємо чергу…')).toBeVisible(); await page.locator('.loading-state svg').evaluate(svg => { (svg as SVGSVGElement).pauseAnimations(); (svg as SVGSVGElement).setCurrentTime(0); });
    await expect(page).toHaveScreenshot(`${theme}-editor-loading.png`, { fullPage: true });
    release(); await expect(page.getByText('Не вдалося завантажити список. Спробуйте ще раз.')).toBeVisible();
    await expect(page).toHaveScreenshot(`${theme}-editor-error.png`, { fullPage: true });
    fail = false; await page.getByRole('button', { name: 'Спробувати ще раз' }).click(); await expect(page.getByRole('heading', { name: 'Усе розглянуто' })).toBeVisible();
    await expect(page).toHaveScreenshot(`${theme}-editor-empty.png`, { fullPage: true });
  });

  test(`${theme}: без ролі немає даних чи захищених дій`, async ({ page }) => {
    const { profileRequests } = await prepare(page, theme); let reads = 0;
    await page.route('**/api/v1/editor/content-reports?*', async route => { reads++; await route.fulfill({ status: 403, json: { code: 'editor.role_required' } }); });
    await page.goto('/editor'); await expect(page.getByRole('heading', { name: 'Потрібен доступ редактора' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Розглянути' })).toHaveCount(0); await expect(page.getByRole('button', { name: 'Застосовані зміни' })).toHaveCount(0); expect(profileRequests).toEqual([]); expect(reads).toBe(1);
    await expect(page).toHaveScreenshot(`${theme}-editor-forbidden.png`, { fullPage: true });
  });
}
