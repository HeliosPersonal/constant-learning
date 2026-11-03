import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClientProvider } from '@tanstack/react-query';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createQueryClient } from '../src/api/query';
import { ThemeProvider } from '../src/theme/ThemeProvider';
import SessionRoute from '../src/features/session/shell/SessionRoute';
import type { SessionResponse } from '../src/features/today/api';
import type { components } from '../src/api/schema';

const item = { id: 'item', position: 1, kind: 'review' as const, prompt_uk: 'жаба', context_uk: 'Це жаба.', card: null };
const session: SessionResponse = { id: 'session', learner_day: '2026-11-02', ends_at: '2026-11-03T00:00:00Z', status: 'active', kind: 'plan', total_items: 1, completed_items: 0, notices: [], current_item: item, last_result: null };
const answer: components['schemas']['AnswerResult'] = { answer_event_id: 'event', outcome: 'mistake_typo', correct_form: 'żaba', diff: [{ op: 'substitute', actual: 'x', expected: 'ż' }, { op: 'equal', actual: 'aba', expected: 'aba' }], pronunciation_uk: 'жаба', example_pl: 'To żaba.', example_uk: 'Це жаба.', gender: 'f', dispute_allowed: true, word_status: 'review', next_review_on: '2026-11-03', next_item: { ...item, id: 'preview', prompt_uk: 'таємне' }, session_completed: true };
const result = (a = answer, dispute: components['schemas']['DisputeResult'] | null = null): SessionResponse => ({ ...session, status: 'completed', completed_items: 1, current_item: null, last_result: { item_id: 'item', answer: a, dispute } });
const json = (data: unknown, status = 200) => new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } });
function mount() { const client = createQueryClient(); const busy = vi.fn(); render(<QueryClientProvider client={client}><ThemeProvider><SessionRoute onToday={vi.fn()} onBusy={busy} /></ThemeProvider></QueryClientProvider>); return { client, busy }; }
afterEach(() => { vi.unstubAllGlobals(); sessionStorage.clear(); });
describe('Пригадування', () => {
  it('порожня відповідь заблокована, 400 розкладки залишає введений текст', async () => {
    vi.stubGlobal('fetch', vi.fn().mockImplementation((r: Request) => Promise.resolve(r.method === 'GET' ? json(session) : json({ code: 'answer.wrong_layout' }, 400)))); mount();
    const input = await screen.findByLabelText('Ваша відповідь'); fireEvent.change(input, { target: { value: '  ' } }); expect(screen.getByRole('button', { name: 'Перевірити' })).toBeDisabled();
    fireEvent.change(input, { target: { value: 'жаба' } }); fireEvent.click(screen.getByRole('button', { name: 'Перевірити' })); await screen.findByText(/Перемкніть розкладку/); expect(input).toHaveValue('жаба'); expect(input).toBeEnabled();
  });
  it.each([['correct', 'Правильно!'], ['mistake_typo', 'Цього разу є помилка.'], ['mistake_diacritic', 'Зверніть увагу на діакритичний знак.'], ['dont_know', 'Вивчимо це слово разом.'], ['synonym', 'Це теж правильне слово, але тут вивчаємо інше. Відповідь нейтральна.']] as const)('показує результат %s після відновлення', async (outcome, title) => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(json(result({ ...answer, outcome, dispute_allowed: outcome === 'mistake_typo' })))); mount(); await screen.findByRole('heading', { name: title });
    expect(screen.queryByText('таємне')).not.toBeInTheDocument(); expect(screen.getByText('To żaba.')).toBeVisible(); if (outcome !== 'mistake_typo') expect(screen.queryByRole('button', { name: 'Моя відповідь теж правильна' })).not.toBeInTheDocument();
  });
  it('блокує подвійну відповідь і перехід під час запиту; фінальний результат чекає continue', async () => {
    let resolve!: (r: Response) => void; let gets = 0; const fetcher = vi.fn().mockImplementation((r: Request) => r.method === 'GET' ? Promise.resolve(json(gets++ ? result() : session)) : r.url.endsWith('/answer') ? new Promise<Response>(done => { resolve = done; }) : Promise.resolve(json({ ...session, status: 'completed', completed_items: 1, current_item: null })));
    vi.stubGlobal('fetch', fetcher); const { busy } = mount(); const input = await screen.findByLabelText('Ваша відповідь'); fireEvent.change(input, { target: { value: 'xaba' } }); const button = screen.getByRole('button', { name: 'Перевірити' }); fireEvent.click(button); fireEvent.click(button);
    await waitFor(() => expect(resolve).toBeDefined()); expect(screen.getByRole('button', { name: '← На головну' })).toBeDisabled(); expect(busy).toHaveBeenCalledWith(true); expect(fetcher.mock.calls.filter(([r]) => r.url.endsWith('/answer'))).toHaveLength(1);
    resolve(json(answer)); await screen.findByRole('heading', { name: 'Цього разу є помилка.' }); expect(screen.queryByText('таємне')).not.toBeInTheDocument(); fireEvent.click(screen.getByRole('button', { name: 'Продовжити' })); await screen.findByRole('heading', { name: 'Заняття завершено' });
  });
  it('невизначена відповідь: відновлення та повтор передають той самий payload', async () => {
    let gets = 0; const payloads: string[] = []; const fetcher = vi.fn().mockImplementation(async (r: Request) => { if (r.method === 'GET') return ++gets === 2 ? json({}, 503) : json(session); payloads.push(await r.text()); if (payloads.length === 1) throw new Error('offline'); return json(answer); });
    vi.stubGlobal('fetch', fetcher); mount(); const input = await screen.findByLabelText('Ваша відповідь'); fireEvent.change(input, { target: { value: 'xaba' } }); fireEvent.click(screen.getByRole('button', { name: 'Перевірити' }));
    await screen.findByText(/Не вдалося перевірити стан відповіді/); expect(input).toBeDisabled(); expect(screen.queryByRole('button', { name: 'Не знаю' })).not.toBeInTheDocument(); fireEvent.click(screen.getByRole('button', { name: 'Оновити стан' })); await screen.findByRole('button', { name: 'Повторити відповідь' }); fireEvent.click(screen.getByRole('button', { name: 'Повторити відповідь' }));
    await waitFor(() => expect(payloads).toHaveLength(2)); expect(payloads[1]).toBe(payloads[0]);
  });
  it('оскарження підтверджується і показує серверну нейтралізацію без старої оцінки', async () => {
    const neutral = { outcome: 'neutralized' as const, content_report_id: 'report', message: 'Передали на перевірку' }; let sent = false;
    vi.stubGlobal('fetch', vi.fn().mockImplementation((r: Request) => { if (r.method === 'POST') { sent = true; return Promise.resolve(json(neutral)); } return Promise.resolve(json(result(answer, sent ? neutral : null))); })); mount();
    await screen.findByRole('heading', { name: 'Цього разу є помилка.' }); fireEvent.click(screen.getByRole('button', { name: 'Моя відповідь теж правильна' })); fireEvent.click(screen.getByRole('button', { name: 'Підтвердити' }));
    await screen.findByRole('heading', { name: 'Відповідь нейтралізовано' }); expect(screen.getByText('Передали на перевірку')).toBeVisible(); expect(screen.queryByText('Цього разу є помилка.')).not.toBeInTheDocument(); expect(screen.queryByRole('button', { name: 'Моя відповідь теж правильна' })).not.toBeInTheDocument(); expect(screen.getByRole('button', { name: 'Продовжити' })).toBeEnabled();
  });
  it('«Не знаю» надсилає явний прапорець і показує серверний результат', async () => {
    let gets = 0; let body = ''; vi.stubGlobal('fetch', vi.fn().mockImplementation(async (r: Request) => { if (r.method === 'GET') return json(gets++ ? result({ ...answer, outcome: 'dont_know', dispute_allowed: false }) : session); body = await r.text(); return json(answer); }));
    mount(); await screen.findByLabelText('Ваша відповідь'); fireEvent.click(screen.getByRole('button', { name: 'Не знаю' })); await screen.findByRole('heading', { name: 'Вивчимо це слово разом.' }); expect(JSON.parse(body)).toEqual({ answer: null, dont_know: true });
  });
  it('втрачену відповідь відновлює з GET без повторного POST', async () => {
    let gets = 0; const fetcher = vi.fn().mockImplementation((r: Request) => r.method === 'GET' ? Promise.resolve(json(gets++ ? result() : session)) : Promise.reject(new Error('offline'))); vi.stubGlobal('fetch', fetcher); mount();
    const input = await screen.findByLabelText('Ваша відповідь'); fireEvent.change(input, { target: { value: 'xaba' } }); fireEvent.click(screen.getByRole('button', { name: 'Перевірити' }));
    await screen.findByRole('heading', { name: 'Цього разу є помилка.' }); expect(fetcher.mock.calls.filter(([r]) => r.method === 'POST')).toHaveLength(1);
  });
  it('session.expired під час відповіді показує пояснення', async () => {
    vi.stubGlobal('fetch', vi.fn().mockImplementation((r: Request) => Promise.resolve(r.method === 'GET' ? json(session) : json({ code: 'session.expired' }, 409)))); mount(); await screen.findByLabelText('Ваша відповідь'); fireEvent.click(screen.getByRole('button', { name: 'Не знаю' })); await screen.findByText(/Час цього заняття минув/);
  });
  it('після перезапуску зберігає невизначену відповідь та дозволяє лише її повтор', async () => {
    sessionStorage.setItem('constant-learning.pending-answer.v1', JSON.stringify({ sessionId: 'session', itemId: 'item', body: { answer: 'xaba', dont_know: false } }));
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(json(session))); mount(); const input = await screen.findByLabelText('Ваша відповідь'); expect(input).toHaveValue('xaba'); expect(input).toBeDisabled(); expect(screen.getByRole('button', { name: 'Повторити відповідь' })).toBeEnabled();
  });

  it('підтверджена нейтралізація залишається видимою, якщо оновлення недоступне', async () => {
    let gets = 0; const neutral = { outcome: 'neutralized', content_report_id: 'report', message: 'Передали на перевірку' };
    vi.stubGlobal('fetch', vi.fn().mockImplementation((r: Request) => Promise.resolve(r.method === 'POST' ? json(neutral) : ++gets === 1 ? json(result()) : json({}, 503)))); mount();
    await screen.findByRole('heading', { name: 'Цього разу є помилка.' }); fireEvent.click(screen.getByRole('button', { name: 'Моя відповідь теж правильна' })); fireEvent.click(screen.getByRole('button', { name: 'Підтвердити' }));
    await screen.findByText('Передали на перевірку'); await screen.findByText(/Не вдалося оновити стан заняття/); expect(screen.queryByText('Цього разу є помилка.')).not.toBeInTheDocument(); expect(screen.queryByRole('button', { name: 'Продовжити' })).not.toBeInTheDocument();
  });

  it('втрачене continue після останнього результату можна повторити тим самим item', async () => {
    let gets = 0; let posts = 0; const urls: string[] = [];
    vi.stubGlobal('fetch', vi.fn().mockImplementation((r: Request) => { if (r.method === 'GET') return Promise.resolve(++gets === 1 ? json(result()) : json({ code: 'session.not_found' }, 404)); urls.push(r.url); return ++posts === 1 ? Promise.reject(new Error('offline')) : Promise.resolve(json({ ...session, status: 'completed', completed_items: 1, current_item: null })); })); mount();
    await screen.findByRole('heading', { name: 'Цього разу є помилка.' }); fireEvent.click(screen.getByRole('button', { name: 'Продовжити' })); await screen.findByText(/Не вдалося підтвердити перехід/); fireEvent.click(screen.getByRole('button', { name: 'Продовжити' }));
    await screen.findByRole('heading', { name: 'Заняття завершено' }); expect(urls).toHaveLength(2); expect(urls[1]).toBe(urls[0]);
  });
  it('втрачене оскарження відновлює нейтралізацію через GET без другого POST', async () => {
    const neutral = { outcome: 'neutralized' as const, content_report_id: 'report', message: 'Передали на перевірку' }; let gets = 0; const fetcher = vi.fn().mockImplementation((r: Request) => r.method === 'GET' ? Promise.resolve(json(result(answer, gets++ ? neutral : null))) : Promise.reject(new Error('offline'))); vi.stubGlobal('fetch', fetcher); mount();
    await screen.findByRole('heading', { name: 'Цього разу є помилка.' }); fireEvent.click(screen.getByRole('button', { name: 'Моя відповідь теж правильна' })); fireEvent.click(screen.getByRole('button', { name: 'Підтвердити' }));
    await screen.findByRole('heading', { name: 'Відповідь нейтралізовано' }); expect(fetcher.mock.calls.filter(([r]) => r.method === 'POST')).toHaveLength(1); expect(screen.queryByRole('button', { name: 'Моя відповідь теж правильна' })).not.toBeInTheDocument();
  });

});
