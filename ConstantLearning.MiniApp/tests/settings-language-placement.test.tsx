import { render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { QueryClientProvider } from '@tanstack/react-query';
import { createQueryClient } from '../src/api/query';
import { ThemeProvider } from '../src/theme/ThemeProvider';
import Settings from '../src/features/settings/Settings';
import type { Learner } from '../src/features/settings/model';
import type { components } from '../src/api/schema';

type LearnerLanguage = components['schemas']['LearnerLanguage'];

const pl: LearnerLanguage = { target_language: 'pl', display_name_uk: 'Польська', is_active: true, started_at: '2026-10-01T09:00:00Z' };
const en: LearnerLanguage = { target_language: 'en', display_name_uk: 'Англійська', is_active: false, started_at: '2026-10-15T09:00:00Z' };
const learner = { id: '0192f0a0-0000-7000-8000-000000000001', first_name: 'Тест', time_zone: 'Europe/Warsaw', time_zone_confirmed: true, daily_new_words_limit: 5, pending_daily_new_words_limit: null, reminder: null, onboarded_at: '2026-11-01T09:00:00Z', active_target_language: 'pl' } as Learner;

const clients: ReturnType<typeof createQueryClient>[] = [];
function json(body: unknown) { return new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } }); }
function mockFetch(languages: { active_target_language: string | null; items: LearnerLanguage[] }) {
  vi.stubGlobal('fetch', vi.fn<typeof fetch>().mockImplementation(request => {
    const path = new URL((request as Request).url).pathname;
    if (path === '/api/v1/languages') return Promise.resolve(json({ items: [] }));
    if (path === '/api/v1/learners/me/languages') return Promise.resolve(json(languages));
    return Promise.resolve(new Response(null, { status: 204 }));
  }));
}
function mount(profile: Learner) {
  const client = createQueryClient(); clients.push(client);
  render(<QueryClientProvider client={client}><ThemeProvider><Settings learner={profile} onBack={vi.fn()} onStart={vi.fn()} /></ThemeProvider></QueryClientProvider>);
}

afterEach(() => { clients.forEach(client => client.clear()); clients.length = 0; vi.unstubAllGlobals(); vi.restoreAllMocks(); });

describe('розміщення вибору мови в налаштуваннях', () => {
  it('ставить «Мова навчання» між «Темою» і групою «Навчання»', async () => {
    mockFetch({ active_target_language: 'pl', items: [pl, en] });
    mount(learner);

    const theme = screen.getByText('Тема');
    const language = await screen.findByRole('heading', { name: 'Мова навчання' });
    const limit = screen.getByText(/^Навчання/);

    expect(theme.compareDocumentPosition(language) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(language.compareDocumentPosition(limit) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('підписує групи ліміту й нагадувань назвою активної мови', async () => {
    mockFetch({ active_target_language: 'en', items: [{ ...pl, is_active: false }, { ...en, is_active: true }] });
    mount({ ...learner, active_target_language: 'en' });

    await waitFor(() => expect(screen.getByText('Навчання · Англійська')).toBeTruthy());
    expect(screen.getByText('Нагадування · Англійська')).toBeTruthy();
  });
});
