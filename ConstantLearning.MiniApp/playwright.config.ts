import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests/visual',
  fullyParallel: false,
  workers: 1,
  use: { baseURL: process.env.MINIAPP_BASE_URL ?? 'http://127.0.0.1:5173', viewport: { width: 390, height: 844 }, locale: 'uk-UA', reducedMotion: 'reduce', browserName: 'chromium' },
  expect: { toHaveScreenshot: { maxDiffPixels: 0, animations: 'disabled' } },
  webServer: process.env.MINIAPP_BASE_URL ? undefined : { command: 'npm run dev', url: 'http://127.0.0.1:5173', reuseExistingServer: false },
  reporter: [['list'], ['html', { open: 'never' }]],
});
