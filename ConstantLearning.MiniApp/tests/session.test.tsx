import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClientProvider } from '@tanstack/react-query';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createQueryClient } from '../src/api/query';
import { ThemeProvider } from '../src/theme/ThemeProvider';
import SessionRoute from '../src/features/session/shell/SessionRoute';
import type { SessionResponse } from '../src/features/today/api';

const item = { id: 'item', position: 31, kind: 'new_word_card' as const, prompt_uk: 'дім', context_uk: null, card: { lemma_pl: 'dom', translations_uk: ['дім'], pronunciation_uk: 'дом', example_pl: 'To dom.', example_uk: 'Це дім.', gender: 'm' } };
const session: SessionResponse = { id: 'session', learner_day: '2026-11-02', ends_at: '2026-11-03T00:00:00Z', status: 'active', total_items: 40, completed_items: 2, notices: [], current_item: item, last_result: null };
const json = (data: unknown, status = 200) => new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } });
function mount(initialSession?: SessionResponse, client = createQueryClient()) { render(<QueryClientProvider client={client}><ThemeProvider><SessionRoute initialSession={initialSession} onToday={vi.fn()} /></ThemeProvider></QueryClientProvider>); return client; }
afterEach(() => { vi.unstubAllGlobals(); });
describe('Каркас заняття', () => {
  it('відновлює серверний стан і показує повну картку та лічильники, а не position', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(json(session))); mount();
    expect(await screen.findByRole('heading', { name: 'dom' })).toHaveAttribute('lang', 'pl');
    expect(screen.getByText('Опрацьовано 2 із 40')).toBeVisible(); expect(screen.getByText('дом · чоловічий рід')).toBeVisible();
  });
  it('при повторному вході приховує стару картку до отримання актуального стану', async () => {
    let release!: (value: Response) => void; vi.stubGlobal('fetch', vi.fn().mockImplementation(() => new Promise<Response>(r => { release = r; })));
    const client = createQueryClient(); client.setQueryData(['session', 'current'], session); mount(session, client);
    expect(screen.queryByRole('heading', { name: 'dom' })).not.toBeInTheDocument();
    await waitFor(() => expect(release).toBeDefined()); release(json({ ...session, current_item: { ...item, card: { ...item.card, lemma_pl: 'kot' } } }));
    expect(await screen.findByRole('heading', { name: 'kot' })).toBeVisible();
  });
  it.each(['acknowledge', 'already-known'] as const)('блокує подвійне натискання %s і отримує серверний прогрес', async action => {
    let release!: (value: Response) => void; let gets = 0;
    const fetcher = vi.fn().mockImplementation((request: Request) => request.method === 'GET' ? Promise.resolve(json({ ...session, completed_items: gets++ ? 3 : 2 })) : new Promise<Response>(r => { release = r; }));
    vi.stubGlobal('fetch', fetcher); mount(session); await screen.findByRole('heading', { name: 'dom' });
    const button = screen.getByRole('button', { name: action === 'acknowledge' ? 'Далі' : 'Вже знаю' }); fireEvent.click(button); fireEvent.click(button);
    await waitFor(() => expect(release).toBeDefined()); expect(fetcher.mock.calls.filter(([r]) => r.method === 'POST')).toHaveLength(1);
    release(json({ next_item: null, session_completed: false })); await screen.findByText('Опрацьовано 3 із 40');
  });
  it('помилка дії та помилка оновлення залишають картку заблокованою до успішного GET', async () => {
    let gets = 0; const fetcher = vi.fn().mockImplementation((r: Request) => r.method === 'POST' ? Promise.resolve(json({}, 503)) : Promise.resolve(++gets === 2 ? json({}, 503) : json(session)));
    vi.stubGlobal('fetch', fetcher); mount(); await screen.findByRole('heading', { name: 'dom' }); fireEvent.click(screen.getByRole('button', { name: 'Далі' }));
    await screen.findByText(/Не вдалося зберегти дію/); expect(screen.getByRole('button', { name: 'Далі' })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: 'Оновити стан' })); await screen.findByText(/Не вдалося оновити заняття/); expect(screen.getByRole('button', { name: 'Далі' })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: 'Оновити стан' })); await waitFor(() => expect(screen.getByRole('button', { name: 'Далі' })).toBeEnabled());
  });
  it('підтверджене завершення та наступний 404 показують підсумок', async () => {
    let gets = 0; vi.stubGlobal('fetch', vi.fn().mockImplementation((r: Request) => Promise.resolve(r.method === 'POST' ? json({ session_completed: true }) : ++gets === 1 ? json(session) : json({ code: 'session.not_found' }, 404))));
    mount(); await screen.findByRole('heading', { name: 'dom' }); fireEvent.click(screen.getByRole('button', { name: 'Вже знаю' })); await screen.findByRole('heading', { name: 'Заняття завершено' });
  });
  it('відкритий результат має пріоритет над preview', async () => {
    const data = { ...session, last_result: { item_id: 'old', answer: { answer_event_id: 'event', outcome: 'correct', correct_form: 'dom', diff: [], pronunciation_uk: 'дом', example_pl: 'To dom.', example_uk: 'Це дім.', gender: 'm', dispute_allowed: false, word_status: 'review', next_review_on: null, next_item: null, session_completed: false }, dispute: null } }; vi.stubGlobal('fetch', vi.fn().mockResolvedValue(json(data))); mount();
    await screen.findByRole('heading', { name: 'Правильно!' }); expect(screen.queryByRole('heading', { name: 'dom' })).not.toBeInTheDocument();
  });
  it('показує повідомлення сервера про ліміт', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(json({ ...session, notices: ['new_words_limit_reached', 'course_completed'] }))); mount();
    expect(await screen.findByText(/Денний ліміт нових слів вичерпано/)).toBeVisible(); expect(screen.getByText(/Базовий курс завершено/)).toBeVisible();
  });
  it.each([404, 409])('%i під час дії не видає за завершення', async status => {
    vi.stubGlobal('fetch', vi.fn().mockImplementation((r: Request) => Promise.resolve(r.method === 'GET' ? json(session) : json({ code: status === 404 ? 'session.item_not_found' : 'session.expired' }, status)))); mount();
    await screen.findByRole('heading', { name: 'dom' }); fireEvent.click(screen.getByRole('button', { name: 'Далі' })); await screen.findByRole('heading', { name: 'Заняття недоступне' });
    expect(screen.queryByRole('heading', { name: 'Заняття завершено' })).not.toBeInTheDocument();
  });
  it('404 при звичайному відкритті не видає за завершення', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(json({ code: 'session.not_found' }, 404))); mount(); await screen.findByRole('heading', { name: 'Активного заняття немає' });
    expect(screen.queryByRole('heading', { name: 'Заняття завершено' })).not.toBeInTheDocument();
  });
  it('без активного заняття пропонує почати нове, а не показує помилку', async () => {
    const fetcher = vi.fn().mockImplementation((r: Request) => Promise.resolve(r.method === 'POST' ? json(session, 201) : json({ code: 'session.not_found' }, 404)));
    vi.stubGlobal('fetch', fetcher); mount(); fireEvent.click(await screen.findByRole('button', { name: 'Почати заняття' }));
    expect(await screen.findByRole('heading', { name: 'dom' })).toBeVisible(); expect(fetcher.mock.calls.filter(([r]) => r.method === 'POST')).toHaveLength(1);
  });
  it('показує повідомлення, якщо новий старт не вдався, і дозволяє повторити', async () => {
    let posts = 0; const fetcher = vi.fn().mockImplementation((r: Request) => Promise.resolve(r.method === 'POST' ? (posts++ ? json(session, 201) : json({}, 503)) : json({ code: 'session.not_found' }, 404)));
    vi.stubGlobal('fetch', fetcher); mount(); fireEvent.click(await screen.findByRole('button', { name: 'Почати заняття' }));
    await screen.findByText('Не вдалося відкрити заняття. Спробуйте ще раз.'); fireEvent.click(screen.getByRole('button', { name: 'Почати заняття' }));
    expect(await screen.findByRole('heading', { name: 'dom' })).toBeVisible();
  });
});
