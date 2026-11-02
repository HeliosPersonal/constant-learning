import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  workers: 1,
  retries: 0,
  use: { baseURL: 'http://127.0.0.1:18888', viewport: { width: 390, height: 844 }, locale: 'uk-UA', timezoneId: 'Europe/Warsaw', trace: 'retain-on-failure', screenshot: 'only-on-failure', video: 'retain-on-failure' },
  webServer: { command: 'dotnet run --project e2e/Host/Host.csproj', wait: { stdout: /E2E_READY/ }, timeout: 120000, reuseExistingServer: false },
});
