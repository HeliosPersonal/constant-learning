import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClientProvider } from '@tanstack/react-query';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createQueryClient } from '../src/api/query';
import { ThemeProvider } from '../src/theme/ThemeProvider';
import SessionRoute from '../src/features/session/shell/SessionRoute';
import type { SessionResponse } from '../src/features/today/api';

const item = { id: 'item', position: 31, kind: 'new_word_card' as const, prompt_uk: 'дім', context_uk: null, card: { lemma_pl: 'dom', translations_uk: ['дім'], pronunciation_uk: 'дом', example_pl: 'To dom.', example_uk: 'Це дім.', gender: 'm' } };
const session: SessionResponse = { id: 'session', learner_day: '2026-11-02', ends_at: '2026-11-03T00:00:00Z', status: 'active', kind: 'plan', total_items: 40, completed_items: 2, notices: [], current_item: item, last_result: null };
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
  it('409 під час дії показує «Заняття недоступне», а не завершення', async () => {
    vi.stubGlobal('fetch', vi.fn().mockImplementation((r: Request) => Promise.resolve(r.method === 'GET' ? json(session) : json({ code: 'session.expired' }, 409)))); mount();
    await screen.findByRole('heading', { name: 'dom' }); fireEvent.click(screen.getByRole('button', { name: 'Далі' })); await screen.findByRole('heading', { name: 'Заняття недоступне' });
    expect(screen.queryByRole('heading', { name: 'Заняття завершено' })).not.toBeInTheDocument();
  });
  it('AC-08: 404 на стертій картці повертає на «Сьогодні» з повідомленням про скидання, а не показує завершення', async () => {
    const onToday = vi.fn();
    vi.stubGlobal('fetch', vi.fn().mockImplementation((r: Request) => Promise.resolve(r.method === 'GET' ? json(session) : json({ code: 'session.item_not_found' }, 404))));
    render(<QueryClientProvider client={createQueryClient()}><ThemeProvider><SessionRoute initialSession={session} onToday={onToday} /></ThemeProvider></QueryClientProvider>);
    await screen.findByRole('heading', { name: 'dom' }); fireEvent.click(screen.getByRole('button', { name: 'Далі' }));
    await waitFor(() => expect(onToday).toHaveBeenCalledWith('Заняття більше немає — можливо, прогрес скинуто.'));
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

const extraWordItem = { id: 'ex-d1', position: 1, kind: 'new_word_card' as const, prompt_uk: 'хата', context_uk: null, card: { lemma_pl: 'chata', translations_uk: ['хата'], pronunciation_uk: 'хата', example_pl: 'Mieszkam w małej chacie.', example_uk: 'Я живу в маленькій хаті.', gender: 'f' } };
const extraWord: SessionResponse = { id: 'extra-word', learner_day: '2026-11-02', ends_at: '2026-11-03T00:00:00Z', status: 'active', kind: 'extra_word', total_items: 2, completed_items: 0, notices: [], current_item: extraWordItem, last_result: null };
const extraWordDone: SessionResponse = { ...extraWord, status: 'completed', completed_items: 2, current_item: null, last_result: null };
const extraPractice: SessionResponse = { id: 'extra-practice', learner_day: '2026-11-02', ends_at: '2026-11-03T00:00:00Z', status: 'active', kind: 'extra_practice', total_items: 12, completed_items: 0, notices: [], current_item: { id: 'ex-d2', position: 1, kind: 'extra_practice', prompt_uk: 'хата', context_uk: null, card: null }, last_result: null };

describe('Extra-заняття (unlimited-learning)', () => {
  it('extra_word показує картку нового слова з діями «Далі» і «Вже знаю»', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(json(extraWord))); mount();
    expect(await screen.findByRole('heading', { name: 'chata' })).toHaveAttribute('lang', 'pl');
    expect(screen.getByText('НОВЕ СЛОВО')).toBeVisible();
    expect(screen.getByRole('button', { name: 'Далі' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Вже знаю' })).toBeEnabled();
  });
  it('AC-08: extra_practice показує вправу пригадування й поводиться як планове заняття', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(json(extraPractice))); mount();
    expect(await screen.findByRole('heading', { name: 'хата' })).toBeVisible();
    expect(screen.getByText('ПРИГАДАЙТЕ ПОЛЬСЬКОЮ')).toBeVisible();
    expect(screen.getByLabelText('Ваша відповідь')).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Перевірити' })).toBeDisabled();
  });
  it('AC-01: завершене extra_word пропонує «Ще одне слово» і «На головну» та бере наступне слово одним POST', async () => {
    const fetcher = vi.fn().mockImplementation((r: Request) => Promise.resolve(r.method === 'POST' ? json(extraWord, 201) : json(extraWordDone)));
    vi.stubGlobal('fetch', fetcher); mount();
    await screen.findByRole('heading', { name: 'Заняття завершено' });
    const more = screen.getByRole('button', { name: 'Ще одне слово' }); expect(screen.getByRole('button', { name: '← На головну' })).toBeVisible();
    fireEvent.click(more); fireEvent.click(more);
    expect(await screen.findByRole('heading', { name: 'chata' })).toBeVisible();
    expect(fetcher.mock.calls.filter(([r]) => r.method === 'POST').filter(([r]) => new URL(r.url).pathname === '/api/v1/sessions/extra-word')).toHaveLength(1);
  });
  it('завершене планове заняття не пропонує «Ще одне слово»', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(json({ ...session, status: 'completed', current_item: null }))); mount();
    await screen.findByRole('heading', { name: 'Заняття завершено' });
    expect(screen.queryByRole('button', { name: 'Ще одне слово' })).not.toBeInTheDocument();
  });
  it('помилка взяття наступного extra-слова пояснює стан і не змінює екран завершення', async () => {
    const fetcher = vi.fn().mockImplementation((r: Request) => Promise.resolve(r.method === 'POST' ? json({ code: 'extra.course_completed' }, 409) : json(extraWordDone)));
    vi.stubGlobal('fetch', fetcher); mount();
    await screen.findByRole('heading', { name: 'Заняття завершено' });
    fireEvent.click(screen.getByRole('button', { name: 'Ще одне слово' }));
    await screen.findByText('Нових слів у курсі більше немає.');
    expect(screen.getByRole('heading', { name: 'Заняття завершено' })).toBeVisible();
  });
});
