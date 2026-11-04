import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { QueryClientProvider } from '@tanstack/react-query';
import { ThemeProvider } from '../src/theme/ThemeProvider';
import { createQueryClient } from '../src/api/query';
import Stats from '../src/features/stats/Stats';
import type { SessionResponse } from '../src/features/today/api';
import { summary, weakWord, wordCard, wordsPage } from './fixtures/stats';

const practiceSession: SessionResponse = { id: '0192f0a0-0000-7000-8000-0000000000c2', learner_day: '2026-11-02', ends_at: '2026-11-02T23:00:00Z', status: 'active', kind: 'extra_practice', total_items: 12, completed_items: 0, notices: [], last_result: null, current_item: { id: '0192f0a0-0000-7000-8000-0000000000d2', position: 1, kind: 'extra_practice', prompt_uk: 'хата', context_uk: null, card: null } };
const clients: ReturnType<typeof createQueryClient>[] = [];
function json(body: unknown, status = 200) { return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }); }
function network(handler?: (request: Request) => Response | Promise<Response>) {
  const fetcher = vi.fn<typeof fetch>().mockImplementation(async input => {
    const request = input as Request;
    if (handler) { const custom = await handler(request); if (custom) return custom; }
    const path = new URL(request.url).pathname;
    if (path === '/api/v1/stats/summary') return json(summary);
    if (path === '/api/v1/stats/words') return json(wordsPage);
    if (path.startsWith('/api/v1/stats/words/')) return json(wordCard);
    if (path === '/api/v1/sessions/extra-practice') return json(practiceSession, 201);
    return json({ code: 'unexpected' }, 500);
  });
  vi.stubGlobal('fetch', fetcher); return fetcher;
}
function mount(props: Partial<React.ComponentProps<typeof Stats>> = {}) {
  const client = createQueryClient(); clients.push(client);
  return render(<QueryClientProvider client={client}><ThemeProvider><Stats onBack={vi.fn()} {...props} /></ThemeProvider></QueryClientProvider>);
}
const requests = (fetcher: ReturnType<typeof network>, path: string) => fetcher.mock.calls.map(call => call[0] as Request).filter(request => new URL(request.url).pathname === path);
const lastWords = (fetcher: ReturnType<typeof network>) => { const list = requests(fetcher, '/api/v1/stats/words'); return new URL(list[list.length - 1].url).searchParams; };

afterEach(() => { clients.forEach(client => client.clear()); clients.length = 0; vi.unstubAllGlobals(); vi.restoreAllMocks(); delete window.Telegram; });

describe('Шапка й зведення «Мої слова»', () => {
  it('показує серверні лічильники seen/recalled/review/weak', async () => {
    network(); mount();
    const header = await screen.findByLabelText('Зведення вивчених слів');
    expect(within(header).getByText('Побачено').parentElement).toHaveTextContent('42');
    expect(within(header).getByText('Пригадую').parentElement).toHaveTextContent('18');
    expect(within(header).getByText('Повторити').parentElement).toHaveTextContent('24');
  });
  it('без вивчених слів показує порожній стан', async () => {
    network(request => new URL(request.url).pathname === '/api/v1/stats/words' ? json({ items: [], has_next: false, has_prev: false, next_cursor: null }) : undefined as unknown as Response);
    mount();
    expect(await screen.findByRole('heading', { name: 'Поки що порожньо' })).toBeVisible();
  });
  it.each([401, 404])('%i не показує список і дає коректне повідомлення', async status => {
    network(() => json({ code: status === 401 ? 'auth.invalid_init_data' : 'learner.not_onboarded' }, status));
    mount();
    await screen.findByText(status === 401 ? 'Відкрийте тренер знову в Telegram, щоб продовжити.' : 'Спершу налаштуйте тренер.');
    expect(screen.queryByLabelText('Зведення вивчених слів')).not.toBeInTheDocument();
  });
});

describe('Вкладки, пошук і сортування', () => {
  it('початковий список запитує tab=all і sort=recent', async () => {
    const fetcher = network(); mount();
    await screen.findByRole('button', { name: /chata/ });
    const params = lastWords(fetcher);
    expect(params.get('tab')).toBe('all'); expect(params.get('sort')).toBe('recent');
  });
  it('вкладки «На повторення» і «Вже пригадую» фільтрують за tab', async () => {
    const fetcher = network(); mount();
    await screen.findByRole('button', { name: /chata/ });
    fireEvent.click(screen.getByRole('tab', { name: 'Повторити' }));
    await waitFor(() => expect(lastWords(fetcher).get('tab')).toBe('review'));
    fireEvent.click(screen.getByRole('tab', { name: 'Пригадую' }));
    await waitFor(() => expect(lastWords(fetcher).get('tab')).toBe('recalled'));
  });
  it('пошук додає q, а сортування — sort', async () => {
    const fetcher = network(); mount();
    await screen.findByRole('button', { name: /chata/ });
    fireEvent.change(screen.getByRole('searchbox', { name: 'Пошук серед вивчених слів' }), { target: { value: 'chat' } });
    await waitFor(() => expect(lastWords(fetcher).get('q')).toBe('chat'));
    fireEvent.change(screen.getByLabelText('Сортування'), { target: { value: 'weakest' } });
    await waitFor(() => expect(lastWords(fetcher).get('sort')).toBe('weakest'));
  });
});

describe('Рядок слова й картка', () => {
  it('рядок показує лічильники й позначає слабке слово', async () => {
    network(); mount();
    const row = await screen.findByRole('button', { name: /chata/ });
    expect(row).toHaveTextContent('4 / 3'); expect(within(row).getByLabelText('правильно 4, неправильно 3')).toBeInTheDocument();
    expect(row).toHaveTextContent('потребує уваги'); expect(row.querySelector('.stats-dot--weak')).not.toBeNull();
    expect(row.querySelector('.stats-bar s')).toHaveStyle({ width: '57%' });
  });
  it('тап на рядок відкриває картку з розбивкою помилок', async () => {
    const fetcher = network(); mount();
    fireEvent.click(await screen.findByRole('button', { name: /chata/ }));
    const breakdown = await screen.findByLabelText('Розбивка помилок');
    expect(within(breakdown).getByText('Друкарська').parentElement).toHaveTextContent('1');
    expect(within(breakdown).getByText('Діакритична').parentElement).toHaveTextContent('1');
    expect(within(breakdown).getByText('«Не знаю»').parentElement).toHaveTextContent('1');
    expect(requests(fetcher, `/api/v1/stats/words/${weakWord.entry_id}`)).toHaveLength(1);
    expect(screen.getByText('хатка')).toBeVisible();
  });
});

describe('Тренувати слабкі', () => {
  it('викликає extra-practice одним POST і відкриває заняття', async () => {
    let release!: (value: Response) => void;
    const fetcher = network(request => new URL(request.url).pathname === '/api/v1/sessions/extra-practice' ? new Promise<Response>(resolve => { release = resolve; }) : undefined as unknown as Response);
    const open = vi.fn(); mount({ onOpenSession: open });
    const button = await screen.findByRole('button', { name: 'Тренувати слабкі · 6' });
    act(() => { fireEvent.click(button); fireEvent.click(button); });
    await waitFor(() => expect(requests(fetcher, '/api/v1/sessions/extra-practice')).toHaveLength(1));
    expect(requests(fetcher, '/api/v1/sessions/extra-practice')[0].method).toBe('POST');
    await act(async () => { release(json(practiceSession, 201)); });
    await waitFor(() => expect(open).toHaveBeenCalledExactlyOnceWith(practiceSession));
  });
  it('без слабких слів кнопка заблокована', async () => {
    network(request => new URL(request.url).pathname === '/api/v1/stats/summary' ? json({ ...summary, weak_count: 0 }) : undefined as unknown as Response);
    mount({ onOpenSession: vi.fn() });
    expect(await screen.findByRole('button', { name: 'Тренувати слабкі' })).toBeDisabled();
  });
  it('409 не відкриває заняття й пояснює стан', async () => {
    const fetcher = network(request => new URL(request.url).pathname === '/api/v1/sessions/extra-practice' ? json({ code: 'extra.no_eligible_words' }, 409) : undefined as unknown as Response);
    const open = vi.fn(); mount({ onOpenSession: open });
    fireEvent.click(await screen.findByRole('button', { name: 'Тренувати слабкі · 6' }));
    await screen.findByText(/немає слів для практики/);
    expect(open).not.toHaveBeenCalled(); expect(requests(fetcher, '/api/v1/sessions/extra-practice')).toHaveLength(1);
  });
});
