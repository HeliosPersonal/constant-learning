import { expect, test } from '@playwright/test';

test('повільний Telegram визначає тему до першого рендера без збереженого вибору', async ({ page }) => {
  let release!: () => void;
  const gate = new Promise<void>(resolve => { release = resolve; });
  await page.emulateMedia({ colorScheme: 'light' });
  await page.addInitScript(() => {
    const observer = new MutationObserver(() => {
      if (document.getElementById('root')?.firstElementChild) {
        document.documentElement.dataset.firstVisibleTheme = document.documentElement.dataset.theme;
        observer.disconnect();
      }
    });
    observer.observe(document, { childList: true, subtree: true });
  });
  await page.route('https://telegram.org/**', async route => {
    await gate;
    await route.fulfill({ contentType: 'text/javascript', body: 'window.Telegram={WebApp:{colorScheme:"dark",version:"6.0"}};' });
  });
  const scriptRequested = page.waitForRequest('https://telegram.org/js/telegram-web-app.js');
  const navigation = page.goto('/');
  await scriptRequested;
  await expect(page.locator('#root')).toHaveCount(0);
  release();
  await navigation;
  await expect(page.getByRole('heading', { name: 'Знайдіть свій ритм.' })).toBeVisible();
  await expect(page.locator('html')).toHaveAttribute('data-first-visible-theme', 'dark');
});

test('збережена тема діє у head без завантаження React', async ({ page }) => {
  await page.route('https://telegram.org/**', route => route.fulfill({ contentType: 'text/javascript', body: '' }));
  await page.route(/\/(?:src\/bootstrap\.ts|assets\/index-[^/]+\.js)(?:\?.*)?$/, route => route.fulfill({ contentType: 'text/javascript', body: '' }));
  await page.addInitScript(() => localStorage.setItem('constant-learning.theme.v1', 'dark'));
  await page.goto('/');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await expect(page.locator('#root')).toBeEmpty();
  expect(await page.locator('body').evaluate(element => getComputedStyle(element).backgroundColor)).toBe('rgb(45, 53, 59)');
});

for (const theme of ['light', 'dark'] as const) {
  test(`${theme}: екран, фокус, помилка й portal`, async ({ page }) => {
    const failures: string[] = [];
    page.on('pageerror', error => failures.push(error.message));
    await page.route('https://telegram.org/**', route => route.fulfill({ contentType: 'text/javascript', body: '' }));
    await page.addInitScript(theme => {
      if (!sessionStorage.getItem('miniapp.visual.seeded')) {
        localStorage.setItem('constant-learning.theme.v1', theme);
        sessionStorage.setItem('miniapp.visual.seeded', 'true');
      }
      const observer = new MutationObserver(() => {
        if (document.getElementById('root')?.firstElementChild) {
          document.documentElement.dataset.firstVisibleTheme = document.documentElement.dataset.theme;
          observer.disconnect();
        }
      });
      observer.observe(document, { childList: true, subtree: true });
    }, theme);
    await page.goto('/');
    await expect(page.getByRole('heading', { name: 'Знайдіть свій ритм.' })).toBeVisible();
    await expect(page.locator('html')).toHaveAttribute('data-first-visible-theme', theme);
    if (process.env.MINIAPP_BASE_URL) await page.screenshot({ path: `artifacts/backend-${theme}-shell.png`, fullPage: true });
    await expect(page).toHaveScreenshot(`${theme}-shell.png`, { fullPage: true });
    await page.getByText('Попередній перегляд компонентів').click();
    await page.getByLabel('Ваш варіант польською').fill('spokój');
    await page.getByLabel('Стан прикладу').selectOption('error');
    await page.getByLabel('Ваш варіант польською').focus();
    if (process.env.MINIAPP_BASE_URL) await page.screenshot({ path: `artifacts/backend-${theme}-states.png`, fullPage: true });
    await expect(page).toHaveScreenshot(`${theme}-states.png`, { fullPage: true });
    await page.getByRole('button', { name: 'Про оформлення' }).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    const portalColors = await dialog.evaluate(element => ({ background: getComputedStyle(element).backgroundColor, surface: getComputedStyle(element).getPropertyValue('--cl-surface').trim(), button: getComputedStyle(element.querySelector('button')!).backgroundColor }));
    expect(portalColors.surface).toBe(theme === 'dark' ? '#343f44' : '#fffbef');
    expect(portalColors.background).toBe(theme === 'dark' ? 'rgb(52, 63, 68)' : 'rgb(255, 251, 239)');
    expect(portalColors.button).toBe(theme === 'dark' ? 'rgb(167, 192, 128)' : 'rgb(82, 101, 47)');
    if (process.env.MINIAPP_BASE_URL) await page.screenshot({ path: `artifacts/backend-${theme}-portal.png` });
    await expect(page).toHaveScreenshot(`${theme}-portal.png`);
    await page.keyboard.press('Escape');
    await expect(dialog).not.toBeVisible();
    await page.getByRole('radio', { name: theme === 'light' ? /Темна/ : /Світла/ }).check();
    await expect(page.getByLabel('Ваш варіант польською')).toHaveValue('spokój');
    await expect(page.getByText('Картка 1 із 5')).toBeVisible();
    await page.reload();
    await expect(page.locator('html')).toHaveAttribute('data-theme', theme === 'light' ? 'dark' : 'light');
    expect(failures).toEqual([]);
  });
}
