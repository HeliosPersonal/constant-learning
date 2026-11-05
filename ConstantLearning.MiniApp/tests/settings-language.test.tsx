import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { QueryClientProvider } from '@tanstack/react-query';
import { createQueryClient } from '../src/api/query';
import { ThemeProvider } from '../src/theme/ThemeProvider';
import { LanguageSection } from '../src/features/settings/language/LanguageSection';
import type { components } from '../src/api/schema';

type TargetLanguage = components['schemas']['TargetLanguage'];
type LearnerLanguage = components['schemas']['LearnerLanguage'];
type LearnerLanguages = components['schemas']['LearnerLanguages'];

const pl: TargetLanguage = { code: 'pl', display_name_uk: 'Польська', is_enabled: true, sort_order: 0, available: true };
const en: TargetLanguage = { code: 'en', display_name_uk: 'Англійська', is_enabled: true, sort_order: 1, available: true };
const de: TargetLanguage = { code: 'de', display_name_uk: 'Німецька', is_enabled: true, sort_order: 2, available: false };
const catalog: TargetLanguage[] = [pl, en, de];
const studiedPl: LearnerLanguage = { target_language: 'pl', display_name_uk: 'Польська', is_active: true, started_at: '2026-10-01T09:00:00Z' };
const studiedEn: LearnerLanguage = { target_language: 'en', display_name_uk: 'Англійська', is_active: false, started_at: '2026-10-15T09:00:00Z' };

const clients: ReturnType<typeof createQueryClient>[] = [];
function json(body: unknown, status = 200) { return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }); }

// Маршрутизація мок-сервера за методом і шляхом. `languages` повертається з посиланням, тож тест може
// підмінити стан мов learner-а після мутації (щоб повторний GET після інвалідації бачив новий активний).
function server(state: { languages: LearnerLanguages }) {
  return (request: Request): Response => {
    const url = new URL(request.url);
    if (request.method === 'GET' && url.pathname === '/api/v1/languages') return json({ items: catalog });
    if (request.method === 'GET' && url.pathname === '/api/v1/learners/me/languages') return json(state.languages);
    if (request.method === 'POST' && url.pathname === '/api/v1/telemetry/client-timings') return new Response(null, { status: 204 });
    throw new Error(`unexpected request ${request.method} ${url.pathname}`);
  };
}
function mockFetch(handler: (request: Request) => Response | Promise<Response>) {
  const fetcher = vi.fn<typeof fetch>().mockImplementation(request => Promise.resolve(handler(request as Request)));
  vi.stubGlobal('fetch', fetcher); return fetcher;
}
function mount() {
  const client = createQueryClient(); clients.push(client);
  render(<QueryClientProvider client={client}><ThemeProvider><LanguageSection /></ThemeProvider></QueryClientProvider>);
  return client;
}
async function readBody(request: Request) { return await request.clone().json(); }

afterEach(() => { clients.forEach(client => client.clear()); clients.length = 0; vi.unstubAllGlobals(); vi.restoreAllMocks(); delete window.Telegram; });

describe('AC-03: перемикання активної мови', () => {
  it('перемикає активну мову, шле телеметрію language_switch_ms і скидає кеш «Сьогодні»/learner', async () => {
    const state: { languages: LearnerLanguages } = { languages: { active_target_language: 'pl', items: [studiedPl, studiedEn] } };
    const requests: Request[] = [];
    const fetcher = mockFetch(request => {
      requests.push(request);
      if (request.method === 'PUT' && new URL(request.url).pathname === '/api/v1/learners/me/active-language') {
        state.languages = { active_target_language: 'en', items: [{ ...studiedPl, is_active: false }, { ...studiedEn, is_active: true }] };
        return json({ active_target_language: 'en' });
      }
      return server(state)(request);
    });
    const client = mount();
    const invalidate = vi.spyOn(client, 'invalidateQueries');

    const group = await screen.findByRole('radiogroup', { name: 'Активна мова навчання' });
    fireEvent.click(within(group).getByRole('radio', { name: /Англійська/ }));

    await waitFor(() => expect(requests.some(r => r.method === 'PUT')).toBe(true));
    const put = requests.find(r => r.method === 'PUT')!;
    expect(new URL(put.url).pathname).toBe('/api/v1/learners/me/active-language');
    expect(await readBody(put)).toEqual({ target_language: 'en' });

    await waitFor(() => expect(requests.some(r => r.method === 'POST' && new URL(r.url).pathname === '/api/v1/telemetry/client-timings')).toBe(true));
    const telemetry = requests.find(r => r.method === 'POST' && new URL(r.url).pathname === '/api/v1/telemetry/client-timings')!;
    expect((await readBody(telemetry)).metric).toBe('language_switch_ms');

    await waitFor(() => {
      expect(invalidate).toHaveBeenCalledWith({ queryKey: ['today'] });
      expect(invalidate).toHaveBeenCalledWith({ queryKey: ['current-learner'] });
      expect(invalidate).toHaveBeenCalledWith({ queryKey: ['learner', 'languages'] });
    });
    expect(fetcher).toHaveBeenCalled();
  });

  it('активна мова позначена ✓ і не викликає PUT при повторному кліку', async () => {
    const state: { languages: LearnerLanguages } = { languages: { active_target_language: 'pl', items: [studiedPl, studiedEn] } };
    const fetcher = mockFetch(server(state));
    mount();
    const group = await screen.findByRole('radiogroup', { name: 'Активна мова навчання' });
    const active = within(group).getByRole('radio', { checked: true });
    expect(active).toHaveTextContent('Польська');
    fireEvent.click(active);
    expect(fetcher.mock.calls.every(call => (call[0] as Request).method === 'GET')).toBe(true);
  });
});

describe('AC-01: додавання мови', () => {
  it('починає нову мову через POST із target_language і шле телеметрію', async () => {
    const state: { languages: LearnerLanguages } = { languages: { active_target_language: 'pl', items: [studiedPl] } };
    const requests: Request[] = [];
    mockFetch(request => {
      requests.push(request);
      if (request.method === 'POST' && new URL(request.url).pathname === '/api/v1/learners/me/languages') {
        state.languages = { active_target_language: 'en', items: [{ ...studiedPl, is_active: false }, { ...studiedEn, is_active: true }] };
        return json({ ...studiedEn, is_active: true }, 201);
      }
      return server(state)(request);
    });
    mount();

    const addGroup = (await screen.findByText('Додати мову')).parentElement!;
    fireEvent.click(within(addGroup).getByRole('button', { name: /Англійська/ }));

    await waitFor(() => expect(requests.some(r => r.method === 'POST' && new URL(r.url).pathname === '/api/v1/learners/me/languages')).toBe(true));
    const post = requests.find(r => r.method === 'POST' && new URL(r.url).pathname === '/api/v1/learners/me/languages')!;
    expect(await readBody(post)).toEqual({ target_language: 'en' });
    await waitFor(() => expect(requests.some(r => new URL(r.url).pathname === '/api/v1/telemetry/client-timings')).toBe(true));
  });
});

describe('AC-02: недоступна мова', () => {
  it('недоступну мову не можна почати — кнопка вимкнена й показано пояснення', async () => {
    const state: { languages: LearnerLanguages } = { languages: { active_target_language: 'pl', items: [studiedPl, studiedEn] } };
    mockFetch(server(state));
    mount();

    const addGroup = (await screen.findByText('Додати мову')).parentElement!;
    const unavailable = within(addGroup).getByRole('button', { name: /Німецька/ });
    expect(unavailable).toBeDisabled();
    expect(unavailable).toHaveTextContent('скоро');
    expect(screen.getByText(/Деякі мови поки недоступні/)).toBeVisible();
  });
});

describe('AC-07: learner ще без жодної мови', () => {
  it('показує порожній стан без лічильників інших мов і пропонує обрати мову', async () => {
    const state: { languages: LearnerLanguages } = { languages: { active_target_language: null, items: [] } };
    mockFetch(server(state));
    mount();

    expect(await screen.findByText(/Ви ще не почали жодної мови/)).toBeVisible();
    expect(screen.queryByRole('radiogroup')).not.toBeInTheDocument();
    // усі мови каталогу пропонуються до старту
    const addGroup = screen.getByText('Додати мову').parentElement!;
    expect(within(addGroup).getByRole('button', { name: /Польська/ })).toBeEnabled();
    expect(within(addGroup).getByRole('button', { name: /Англійська/ })).toBeEnabled();
  });
});
