import { StrictMode } from 'react';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { QueryClientProvider } from '@tanstack/react-query';
import { ThemeProvider } from '../src/theme/ThemeProvider';
import { themeStore } from '../src/theme/store';
import { createQueryClient } from '../src/api/query';
import { Today } from '../src/features/today/Today';
import { createTodayTiming, todayTiming } from '../src/features/today/timing';
import { todayQueryKey, type SessionResponse } from '../src/features/today/api';
import { learnerQueryKey, type Learner } from '../src/features/onboarding/api';
import { todaySummary } from './fixtures/today';

const learner: Learner = { id: '0192f0a0-0000-7000-8000-000000000001', time_zone: 'Europe/Warsaw', time_zone_confirmed: true, daily_new_words_limit: 5, pending_daily_new_words_limit: 10, reminder: { local_time: '19:00', days_of_week: ['mon'] }, onboarded_at: '2026-11-01T09:00:00Z' };
const active: SessionResponse = { id: '0192f0a0-0000-7000-8000-000000000002', learner_day: '2026-11-02', ends_at: '2026-11-02T23:00:00Z', status: 'active', total_items: 5, completed_items: 2, notices: [], last_result: null, current_item: { id: '0192f0a0-0000-7000-8000-000000000003', position: 3, kind: 'review', prompt_uk: 'дім', context_uk: null, card: null } };
const clients: ReturnType<typeof createQueryClient>[] = [];
function json(body: unknown, status = 200) { return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }); }
function network(handler?: (request: Request) => Response | Promise<Response>) {
  const fetcher = vi.fn<typeof fetch>().mockImplementation(async input => {
    const request = input as Request;
    if (new URL(request.url).pathname === '/api/v1/telemetry/client-timings') return new Response(null, { status: 204 });
    return handler ? handler(request) : json(todaySummary);
  });
  vi.stubGlobal('fetch', fetcher); return fetcher;
}
function mount(props: Partial<React.ComponentProps<typeof Today>> = {}, client = createQueryClient(), strict = false) {
  clients.push(client);
  const content = <QueryClientProvider client={client}><ThemeProvider><Today learner={learner} getDeviceTimeZone={() => 'Europe/Warsaw'} {...props} /></ThemeProvider></QueryClientProvider>;
  const view = render(strict ? <StrictMode>{content}</StrictMode> : content);
  return { ...view, client };
}
const requests = (fetcher: ReturnType<typeof network>, path: string) => fetcher.mock.calls.map(call => call[0] as Request).filter(request => new URL(request.url).pathname === path);
// Кожний тест моделює окреме відкриття документа, повторні mount усередині — той самий документ.
beforeEach(() => { vi.spyOn(todayTiming, 'ready').mockImplementation(createTodayTiming().ready); });
afterEach(() => { clients.forEach(client => client.clear()); clients.length = 0; vi.unstubAllGlobals(); vi.restoreAllMocks(); delete window.Telegram; });

describe('Прогрес і стани дня', () => {
  it('AC-28: показує серверні 8 повторень, 12 пригаданих слів і 3 завершені заняття без локального перерахунку', async () => {
    const fetcher = network(); mount();
    const progress = await screen.findByLabelText('Ваш навчальний прогрес');
    expect(within(progress).getByText('Слів до повторення').parentElement).toHaveTextContent('8');
    expect(within(progress).getByText('Уже вдається пригадати').parentElement).toHaveTextContent('12');
    expect(within(progress).getByText('Завершених занять за 7 днів').parentElement).toHaveTextContent('3');
    expect(screen.getByText(/2 листопада/)).toBeVisible();
    expect(requests(fetcher, '/api/v1/today')).toHaveLength(1);
  });
  it('поки запит очікує, кнопка заняття й метрика відсутні', () => {
    const fetcher = network(() => new Promise<Response>(() => {})); mount();
    expect(screen.getByText('Збираємо ваш прогрес…')).toBeVisible();
    expect(screen.queryByRole('button', { name: 'Почати заняття' })).not.toBeInTheDocument();
    expect(requests(fetcher, '/api/v1/telemetry/client-timings')).toHaveLength(0);
  });
  it('завершений курс без повторень і активного заняття блокує початок без API, але повідомляє про готовий екран', async () => {
    const fetcher = network(() => json({ ...todaySummary, due_review_count: 0, recalled_words_count: 0, completed_sessions_last_7_days: 0, course_state: 'completed' })); const open = vi.fn(); const { client } = mount({ onOpenSession: open });
    await screen.findByRole('heading', { name: 'Базовий курс завершено' });
    expect(screen.getByText('На зараз повторень немає. Завітайте пізніше.')).toBeVisible();
    const button = screen.getByRole('button', { name: 'На сьогодні все' }); expect(button).toBeDisabled(); expect(button).toHaveAccessibleDescription('На зараз повторень немає. Завітайте пізніше.');
    fireEvent.click(button); expect(requests(fetcher, '/api/v1/sessions')).toHaveLength(0); expect(requests(fetcher, '/api/v1/sessions/current')).toHaveLength(0); expect(open).not.toHaveBeenCalled();
    await waitFor(() => expect(requests(fetcher, '/api/v1/telemetry/client-timings')).toHaveLength(1));
    await act(async () => { await client.invalidateQueries({ queryKey: todayQueryKey }); });
    expect(requests(fetcher, '/api/v1/telemetry/client-timings')).toHaveLength(1);
  });
  it('нуль повторень у незавершеному курсі не блокує нові слова', async () => {
    network(() => json({ ...todaySummary, due_review_count: 0 })); mount();
    expect(await screen.findByRole('button', { name: 'Почати заняття' })).toBeEnabled();
    expect(screen.getByText('На зараз повторень немає. У занятті можуть бути нові слова за вашим темпом.')).toBeVisible();
  });
  it('завершений курс без належних повторень дозволяє продовжити наявне заняття', async () => {
    const fetcher = network(request => new URL(request.url).pathname === '/api/v1/today' ? json({ ...todaySummary, due_review_count: 0, course_state: 'completed', has_active_session: true }) : json(active)); const open = vi.fn(); mount({ onOpenSession: open });
    const button = await screen.findByRole('button', { name: 'Продовжити заняття' }); expect(button).toBeEnabled(); expect(screen.queryByRole('button', { name: 'На сьогодні все' })).not.toBeInTheDocument();
    fireEvent.click(button); await waitFor(() => expect(open).toHaveBeenCalledExactlyOnceWith(active));
    expect(requests(fetcher, '/api/v1/sessions/current')).toHaveLength(1); expect(requests(fetcher, '/api/v1/sessions')).toHaveLength(0);
  });
  it('поповнення показує один раз також після повторного монтування кешованого дня', async () => {
    network(() => json({ ...todaySummary, course_replenished_notice: true })); const first = mount();
    await screen.findByRole('heading', { name: 'Курс поповнено' });
    act(() => themeStore.setTheme('dark')); expect(screen.getAllByText('Курс поповнено')).toHaveLength(1);
    first.unmount(); mount({}, first.client);
    await screen.findByRole('button', { name: 'Почати заняття' });
    expect(screen.queryByRole('heading', { name: 'Курс поповнено' })).not.toBeInTheDocument();
  });
  it('тимчасова помилка має лише явний повтор', async () => {
    let attempts = 0; const fetcher = network(() => ++attempts === 1 ? json({ code: 'server.unavailable' }, 503) : json(todaySummary)); mount();
    await screen.findByText('Не вдалося завантажити прогрес. Спробуйте ще раз.'); expect(requests(fetcher, '/api/v1/today')).toHaveLength(1);
    fireEvent.click(screen.getByRole('button', { name: 'Спробувати ще раз' })); await screen.findByLabelText('Ваш навчальний прогрес'); expect(requests(fetcher, '/api/v1/today')).toHaveLength(2);
  });
  it.each([401, 404])('%i не розкриває кешований прогрес, не повторює запити й дає правильну дію', async status => {
    const fetcher = network(() => json({ code: status === 401 ? 'auth.invalid_init_data' : 'learner.not_onboarded' }, status)); const start = vi.fn(); mount({ onStart: start });
    await screen.findByText(status === 401 ? 'Відкрийте тренер знову в Telegram, щоб продовжити.' : 'Спершу налаштуйте тренер.');
    expect(screen.queryByLabelText('Ваш навчальний прогрес')).not.toBeInTheDocument(); expect(screen.queryByRole('button', { name: 'Спробувати ще раз' })).not.toBeInTheDocument(); expect(requests(fetcher, '/api/v1/today')).toHaveLength(1);
    if (status === 404) { fireEvent.click(screen.getByRole('button', { name: 'До початку' })); expect(start).toHaveBeenCalledOnce(); }
  });
});

describe('Часовий пояс лише за підтвердженням', () => {
  it.each([undefined, 'Invalid/Zone', 'Europe/Warsaw'])('не пропонує невідомий або поточний пояс %s', async zone => {
    const fetcher = network(); mount({ getDeviceTimeZone: () => zone }); await screen.findByLabelText('Ваш навчальний прогрес');
    expect(screen.queryByRole('button', { name: 'Підтвердити пояс пристрою' })).not.toBeInTheDocument(); expect(requests(fetcher, '/api/v1/learners/me/settings')).toHaveLength(0);
  });
  it('відмова та зміна теми нічого не зберігають', async () => {
    const fetcher = network(); mount({ getDeviceTimeZone: () => 'Asia/Tokyo' }); await screen.findByRole('button', { name: 'Підтвердити пояс пристрою' });
    act(() => themeStore.setTheme('light')); expect(requests(fetcher, '/api/v1/learners/me/settings')).toHaveLength(0);
    fireEvent.click(screen.getByRole('button', { name: 'Залишити збережений пояс' })); expect(screen.queryByRole('heading', { name: 'У вас інший місцевий час?' })).not.toBeInTheDocument(); expect(requests(fetcher, '/api/v1/learners/me/settings')).toHaveLength(0);
  });
  it('підтвердження надсилає лише time_zone один раз, оновлює профіль і перечитує день', async () => {
    const updated = { ...learner, time_zone: 'Asia/Tokyo' }; let release!: (value: Response) => void;
    const fetcher = network(request => request.method === 'PATCH' ? new Promise<Response>(resolve => { release = resolve; }) : json(todaySummary));
    const { client } = mount({ getDeviceTimeZone: () => 'Asia/Tokyo' });
    const button = await screen.findByRole('button', { name: 'Підтвердити пояс пристрою' });
    act(() => { fireEvent.click(button); fireEvent.click(button); });
    await waitFor(() => expect(requests(fetcher, '/api/v1/learners/me/settings')).toHaveLength(1));
    expect(await requests(fetcher, '/api/v1/learners/me/settings')[0].clone().json()).toEqual({ time_zone: 'Asia/Tokyo' });
    expect(screen.getByRole('button', { name: 'Зберігаємо часовий пояс…' })).toBeDisabled();
    await act(async () => { release(json(updated)); }); await screen.findByText('Часовий пояс збережено.');
    expect(client.getQueryData(learnerQueryKey)).toEqual(updated);
    await waitFor(() => expect(requests(fetcher, '/api/v1/today')).toHaveLength(2));
  });
  it.each([400, 401, 503])('помилка %i не змінює профіль і не повторює PATCH автоматично', async status => {
    const fetcher = network(request => request.method === 'PATCH' ? json({ code: 'settings.invalid_time_zone' }, status) : json(todaySummary)); const { client } = mount({ getDeviceTimeZone: () => 'Asia/Tokyo' });
    client.setQueryData(learnerQueryKey, learner); fireEvent.click(await screen.findByRole('button', { name: 'Підтвердити пояс пристрою' }));
    await screen.findByText(status === 400 ? /Виберіть його вручну/ : status === 401 ? 'Відкрийте тренер знову в Telegram, щоб продовжити.' : /Збережений пояс не змінився/);
    expect(client.getQueryData(learnerQueryKey)).toEqual(learner); expect(requests(fetcher, '/api/v1/learners/me/settings')).toHaveLength(1);
    if (status === 401) expect(screen.getByRole('button', { name: 'Почати заняття' })).toBeDisabled();
  });
});

describe('Початок і продовження заняття', () => {
  it.each([false, true])('has_active_session=%s: правильний метод, один запит і передача справжньої відповіді', async resume => {
    let release!: (value: Response) => void;
    const fetcher = network(request => new URL(request.url).pathname === '/api/v1/today' ? json({ ...todaySummary, has_active_session: resume }) : new Promise<Response>(resolve => { release = resolve; })); const open = vi.fn(); mount({ onOpenSession: open });
    const button = await screen.findByRole('button', { name: resume ? 'Продовжити заняття' : 'Почати заняття' });
    act(() => { fireEvent.click(button); fireEvent.click(button); });
    const path = resume ? '/api/v1/sessions/current' : '/api/v1/sessions';
    await waitFor(() => expect(requests(fetcher, path)).toHaveLength(1)); expect(requests(fetcher, path)[0].method).toBe(resume ? 'GET' : 'POST');
    expect(screen.getByRole('button', { name: 'Відкриваємо заняття…' })).toBeDisabled();
    await act(async () => { release(json({ ...active, notices: ['new_words_limit_reached'] }, resume ? 200 : 201)); });
    await waitFor(() => expect(open).toHaveBeenCalledExactlyOnceWith({ ...active, notices: ['new_words_limit_reached'] })); await screen.findByText(/Денний ліміт нових слів вичерпано/);
  });
  it('без обробника не створює заняття й показує завершений стан недоступності', async () => {
    const fetcher = network(); mount(); fireEvent.click(await screen.findByRole('button', { name: 'Почати заняття' }));
    await screen.findByText('Заняття зараз недоступне. Спробуйте відкрити тренер пізніше.'); expect(requests(fetcher, '/api/v1/sessions')).toHaveLength(0); expect(screen.getByRole('button', { name: 'Почати заняття' })).toBeEnabled();
  });
  it('закрите заняття перечитує день і не створює нове автоматично', async () => {
    let reads = 0; const fetcher = network(request => new URL(request.url).pathname === '/api/v1/today' ? json({ ...todaySummary, has_active_session: ++reads === 1 }) : json({ code: 'session.not_found' }, 404)); const open = vi.fn(); mount({ onOpenSession: open });
    fireEvent.click(await screen.findByRole('button', { name: 'Продовжити заняття' })); await screen.findByText(/Попереднє заняття вже закрито/); await screen.findByRole('button', { name: 'Почати заняття' });
    expect(open).not.toHaveBeenCalled(); expect(requests(fetcher, '/api/v1/sessions')).toHaveLength(0);
  });
  it('помилка мережі зберігає прогрес і дозволяє явний повтор', async () => {
    const fetcher = network(request => new URL(request.url).pathname === '/api/v1/today' ? json(todaySummary) : Promise.reject(new TypeError('Мережа недоступна'))); mount({ onOpenSession: vi.fn() });
    fireEvent.click(await screen.findByRole('button', { name: 'Почати заняття' })); await screen.findByText('Не вдалося відкрити заняття. Спробуйте ще раз.'); expect(screen.getByLabelText('Ваш навчальний прогрес')).toBeVisible(); expect(requests(fetcher, '/api/v1/sessions')).toHaveLength(1);
    fireEvent.click(screen.getByRole('button', { name: 'Почати заняття' })); await waitFor(() => expect(requests(fetcher, '/api/v1/sessions')).toHaveLength(2));
  });
});

describe('Метрика відкриття', () => {
  it('StrictMode, refetch, тема й повторний монтаж дають одну подію після доступної кнопки без приватного payload', async () => {
    window.Telegram = { WebApp: { initData: 'signed=synthetic' } };
    const fetcher = network(); const first = mount({}, createQueryClient(), true); await screen.findByRole('button', { name: 'Почати заняття' });
    await waitFor(() => expect(requests(fetcher, '/api/v1/telemetry/client-timings')).toHaveLength(1));
    const event = requests(fetcher, '/api/v1/telemetry/client-timings')[0];
    expect(event.headers.get('Authorization')).toBe('tma signed=synthetic');
    expect(await event.clone().json()).toEqual({ metric: 'today_ready_ms', value_ms: expect.any(Number) }); expect(new URL(event.url).search).toBe('');
    act(() => themeStore.setTheme('dark')); await act(async () => { await first.client.invalidateQueries({ queryKey: todayQueryKey }); }); first.unmount(); mount({}, first.client); await screen.findByRole('button', { name: 'Почати заняття' });
    expect(requests(fetcher, '/api/v1/telemetry/client-timings')).toHaveLength(1);
  });
  it.each([0, 120.6, 60_000])('Performance API %s мс округлюється й надсилається лише раз навіть після помилки', async value => {
    const send = vi.fn().mockRejectedValue(new TypeError('Недоступно')); const timing = createTodayTiming(() => value); timing.ready(send); timing.ready(send); await Promise.resolve();
    expect(send).toHaveBeenCalledExactlyOnceWith(Math.round(value));
  });
  it.each([-1, NaN, Infinity, 60_001])('не надсилає недопустиме чи обрізане значення %s', value => {
    const send = vi.fn(); createTodayTiming(() => value).ready(send); expect(send).not.toHaveBeenCalled();
  });
});
