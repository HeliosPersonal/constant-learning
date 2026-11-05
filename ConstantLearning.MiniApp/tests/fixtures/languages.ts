import type { Page } from '@playwright/test';

export async function mockLanguages(page: Page) {
  await page.route('**/api/v1/languages', route => route.fulfill({ json: { items: [
    { code: 'pl', display_name_uk: 'Польська', is_enabled: true, sort_order: 0, available: true },
    { code: 'en', display_name_uk: 'Англійська', is_enabled: true, sort_order: 1, available: true },
  ] } }));
  await page.route('**/api/v1/learners/me/languages', route => route.fulfill({ json: {
    active_target_language: 'pl', items: [{ target_language: 'pl', display_name_uk: 'Польська', is_active: true, started_at: '2026-11-01T09:00:00Z' }],
  } }));
}
