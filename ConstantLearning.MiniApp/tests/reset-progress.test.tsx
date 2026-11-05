import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { QueryClientProvider } from '@tanstack/react-query';
import { createQueryClient } from '../src/api/query';
import { ThemeProvider } from '../src/theme/ThemeProvider';
import { App } from '../src/App';
import Settings from '../src/features/settings/Settings';
import { learnerQueryKey } from '../src/features/onboarding/api';
import { todayQueryKey, type SessionResponse } from '../src/features/today/api';
import type { Learner } from '../src/features/settings/model';
import { todaySummary } from './fixtures/today';

const learner: Learner = { id: '0192f0a0-0000-7000-8000-000000000001', first_name: 'Тест', time_zone: 'Europe/Warsaw', time_zone_confirmed: true, daily_new_words_limit: 5, pending_daily_new_words_limit: null, reminder: { local_time: '19:00', days_of_week: ['mon', 'fri'] }, onboarded_at: '2026-11-01T09:00:00Z' };
const card = { id: 'item', position: 3, kind: 'new_word_card' as const, prompt_uk: 'дім', context_uk: null, card: { lemma_pl: 'dom', translations_uk: ['дім'], pronunciation_uk: 'дом', example_pl: 'To dom.', example_uk: 'Це дім.', gender: 'm' } };
const session: SessionResponse = { id: 'session', learner_day: '2026-11-02', ends_at: '2026-11-03T00:00:00Z', status: 'active', kind: 'plan', target_language: 'pl', total_items: 5, completed_items: 2, notices: [], current_item: card, last_result: null };

const clients: ReturnType<typeof createQueryClient>[] = [];
function json(body: unknown, status = 200) { return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }); }
// Settings тепер містить <LanguageSection>, що на монтуванні вантажить мови — маршрутизуємо ці GET-и
// на порожні відповіді, щоб тести скидання прогресу рахували лише свій POST reset.
function mockFetch(handler: (request: Request) => Response | Promise<Response>) {
  const fetcher = vi.fn<typeof fetch>().mockImplementation(request => {
    const path = new URL((request as Request).url).pathname;
    if (path === '/api/v1/languages') return Promise.resolve(json({ items: [] }));
    if (path === '/api/v1/learners/me/languages') return Promise.resolve(json({ active_target_language: null, items: [] }));
    if (path === '/api/v1/telemetry/client-timings') return Promise.resolve(new Response(null, { status: 204 }));
    return Promise.resolve(handler(request as Request));
  });
  vi.stubGlobal('fetch', fetcher); return fetcher;
}
// Скидання прогресу — лише POST на /progress/reset; мовні GET-и не рахуються.
const resets = (fetcher: ReturnType<typeof mockFetch>) => fetcher.mock.calls
  .filter(call => (call[0] as Request).method === 'POST' && new URL((call[0] as Request).url).pathname === '/api/v1/learners/me/progress/reset');
function mountSettings() {
  const back = vi.fn(); const start = vi.fn(); const client = createQueryClient(); clients.push(client);
  render(<QueryClientProvider client={client}><ThemeProvider><Settings learner={learner} onBack={back} onStart={start} /></ThemeProvider></QueryClientProvider>);
  return { back, start, client };
}
function openResetDialog() { fireEvent.click(screen.getByRole('button', { name: 'Скинути прогрес…' })); return screen.getByRole('dialog'); }
afterEach(() => { clients.forEach(client => client.clear()); clients.length = 0; vi.unstubAllGlobals(); vi.restoreAllMocks(); window.history.replaceState(null, '', '/'); });

describe('AC-02: діалог підтвердження скидання', () => {
  it('перелічує стерте, збережене й незворотність та дає вибір «Скасувати»/«Стерти прогрес»', () => {
    const fetcher = mockFetch(() => json(learner)); mountSettings();
    const dialog = openResetDialog();
    expect(within(dialog).getByText(/стан усіх вивчених слів/)).toBeVisible();
    expect(within(dialog).getByText(/усі заняття/)).toBeVisible();
    expect(within(dialog).getByText(/усі відповіді/)).toBeVisible();
    expect(within(dialog).getByText(/денний ліміт нових слів/)).toBeVisible();
    expect(within(dialog).getByText(/часовий пояс і нагадування/)).toBeVisible();
    expect(within(dialog).getByText(/надіслані скарги на контент/)).toBeVisible();
    expect(within(dialog).getByText('Цю дію не можна скасувати.')).toBeVisible();
    expect(within(dialog).getByRole('button', { name: 'Скасувати' })).toBeVisible();
    expect(within(dialog).getByRole('button', { name: 'Стерти прогрес' })).toBeVisible();
    expect(resets(fetcher)).toHaveLength(0);
  });
});

describe('AC-03: відмова від скидання', () => {
  it('«Скасувати» закриває діалог, нічого не стирає й лишає налаштування без змін', async () => {
    const fetcher = mockFetch(() => json(learner)); const { back } = mountSettings();
    const dialog = openResetDialog();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Скасувати' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(resets(fetcher)).toHaveLength(0); expect(back).not.toHaveBeenCalled();
    expect(screen.getByRole('form', { name: 'Налаштування профілю' })).toBeVisible();
  });
});

describe('AC-01/AC-09: підтверджене скидання', () => {
  it('стирає прогрес, скидає кеш learner і «Сьогодні» та повертає на «Сьогодні» з повідомленням', async () => {
    const fetcher = mockFetch(() => json({ erased: true })); const { back, client } = mountSettings();
    client.setQueryData(learnerQueryKey, learner); client.setQueryData(todayQueryKey, todaySummary);
    const dialog = openResetDialog();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Стерти прогрес' }));
    await waitFor(() => expect(back).toHaveBeenCalledWith('Прогрес скинуто. Починаємо з чистого аркуша.'));
    const request = resets(fetcher)[0][0] as Request;
    expect(request.method).toBe('POST'); expect(request.url).toBe(`${window.location.origin}/api/v1/learners/me/progress/reset`);
    expect(client.getQueryState(learnerQueryKey)?.isInvalidated).toBe(true);
    expect(client.getQueryState(todayQueryKey)?.isInvalidated).toBe(true);
  });
  it('повторне скидання порожнього прогресу повідомляє про відсутність змін (AC-09)', async () => {
    mockFetch(() => json({ erased: false })); const { back } = mountSettings();
    fireEvent.click(within(openResetDialog()).getByRole('button', { name: 'Стерти прогрес' }));
    await waitFor(() => expect(back).toHaveBeenCalledWith('Прогрес уже був порожнім — нічого не змінено.'));
  });
});

describe('AC-04: збій скидання', () => {
  it('500 лишає learner на місці з повідомленням, що нічого не змінено', async () => {
    mockFetch(() => json({ code: 'progress_reset.failed', message: 'Серверне пояснення' }, 500)); const { back } = mountSettings();
    const dialog = openResetDialog();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Стерти прогрес' }));
    await within(dialog).findByText('Не вдалося скинути прогрес. Нічого не змінено — спробуйте ще раз.');
    expect(back).not.toHaveBeenCalled(); expect(screen.getByRole('dialog')).toBeVisible();
  });
});

describe('AC-08: стерта картка повертає на «Сьогодні»', () => {
  it('відповідь на картку зниклого заняття веде на «Сьогодні» з повідомленням про скидання', async () => {
    window.history.replaceState(null, '', '/session');
    const client = createQueryClient(); clients.push(client);
    mockFetch(request => {
      const url = new URL(request.url);
      if (url.pathname === '/api/v1/sessions/current') return json(session);
      if (url.pathname.endsWith('/acknowledge')) return json({ code: 'session.item_not_found' }, 404);
      if (url.pathname === '/api/v1/today') return json(todaySummary);
      if (url.pathname === '/api/v1/telemetry/client-timings') return new Response(null, { status: 204 });
      return json(learner);
    });
    render(<QueryClientProvider client={client}><ThemeProvider><App /></ThemeProvider></QueryClientProvider>);
    fireEvent.click(await screen.findByRole('button', { name: 'Далі' }));
    expect(await screen.findByText('Заняття більше немає — можливо, прогрес скинуто.')).toBeVisible();
    await screen.findByRole('heading', { name: 'Сьогодні' });
    expect(window.location.pathname).toBe('/today');
  });
});
