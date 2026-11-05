import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { QueryClientProvider } from '@tanstack/react-query';
import { createQueryClient } from '../src/api/query';
import { ThemeProvider } from '../src/theme/ThemeProvider';
import { THEME_KEY } from '../src/theme/store';
import { App } from '../src/App';
import { todaySummary } from './fixtures/today';
import Settings from '../src/features/settings/Settings';
import { draftFromLearner, settingsChanges, validateSettings, type Learner, type SettingsUpdate } from '../src/features/settings/model';
import type { ApiErrorBody } from '../src/features/onboarding/api';
import { parsePrivacyContact } from '../src/features/onboarding/privacy';

const learner: Learner = { id: '0192f0a0-0000-7000-8000-000000000001', first_name: 'Тест', time_zone: 'Europe/Warsaw', time_zone_confirmed: true, daily_new_words_limit: 5, pending_daily_new_words_limit: null, reminder: { local_time: '19:00', days_of_week: ['mon', 'fri'] }, onboarded_at: '2026-11-01T09:00:00Z' };
const notOnboarded: ApiErrorBody = { code: 'learner.not_onboarded', message: 'Спершу налаштуйте тренер.' };
const SAVED = 'Збережено ✓';
const clients: ReturnType<typeof createQueryClient>[] = [];
function json(body: Learner | ApiErrorBody | typeof todaySummary | { content_editor: boolean }, status = 200) { return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }); }
// Settings тепер містить <LanguageSection>, що на монтуванні вантажить каталог мов і мови learner-а.
// Ці запити маршрутизуємо тут на порожні відповіді, щоб тести темпу/нагадувань бачили лише свій PATCH.
function mockFetch(handler: (request: Request) => Response | Promise<Response>) {
  const fetcher = vi.fn<typeof fetch>().mockImplementation(request => {
    const path = new URL((request as Request).url).pathname;
    if (path === '/api/v1/languages') return Promise.resolve(json({ items: [] } as never));
    if (path === '/api/v1/learners/me/languages') return Promise.resolve(json({ active_target_language: null, items: [] } as never));
    if (path === '/api/v1/telemetry/client-timings') return Promise.resolve(new Response(null, { status: 204 }));
    return Promise.resolve(handler(request as Request));
  });
  vi.stubGlobal('fetch', fetcher); return fetcher;
}
// Запити збереження налаштувань — лише PATCH; мовні GET-и не рахуються.
const patches = (fetcher: ReturnType<typeof mockFetch>) => fetcher.mock.calls.filter(call => (call[0] as Request).method === 'PATCH');
function mount(element: React.ReactNode) {
  const client = createQueryClient(); clients.push(client);
  render(<QueryClientProvider client={client}><ThemeProvider>{element}</ThemeProvider></QueryClientProvider>);
  return client;
}
function form(profile = learner) {
  const back = vi.fn(); const start = vi.fn();
  const client = mount(<Settings learner={profile} onBack={back} onStart={start} />);
  return { back, start, client };
}
const limitGroup = () => screen.getByRole('group', { name: 'Нових слів на день' });
const limitValue = () => Number.parseInt(within(limitGroup()).getByRole('status').textContent ?? '', 10);
function chooseLimit(value: number) {
  while (limitValue() !== value) fireEvent.click(screen.getByRole('button', { name: limitValue() < value ? 'Більше' : 'Менше' }));
}
const saved = () => screen.findByText(SAVED, {}, { timeout: 3000 });
// Автозбереження чекає ~600 мс після останньої зміни: цей час потрібен, щоб переконатися, що запиту не буде.
const settle = () => act(async () => { await new Promise(resolve => setTimeout(resolve, 900)); });
afterEach(() => { clients.forEach(client => client.clear()); clients.length = 0; vi.unstubAllGlobals(); vi.restoreAllMocks(); delete window.Telegram; window.history.replaceState(null, '', '/'); });

describe('AC-44: чинний і відкладений темп', () => {
  it('показує чинний ліміт окремо й обирає вже відкладений ліміт', () => {
    const fetcher = mockFetch(() => json(learner)); form({ ...learner, pending_daily_new_words_limit: 10 });
    expect(screen.getByText('Сьогодні:', { exact: false })).toHaveTextContent('5 нових слів');
    expect(screen.getByText('З наступної доби:', { exact: false })).toHaveTextContent('10 нових слів');
    expect(limitValue()).toBe(10); expect(fetcher).not.toHaveBeenCalled();
  });
  it('автоматично зберігає новий ліміт і пояс, залишаючи чинний ліміт у відповіді сервера', async () => {
    let body: SettingsUpdate | undefined;
    const updated: Learner = { ...learner, time_zone: 'Europe/Kyiv', pending_daily_new_words_limit: 10 };
    const fetcher = mockFetch(async request => { body = await request.json() as SettingsUpdate; return json(updated); });
    const { client } = form(); chooseLimit(10); fireEvent.change(screen.getByLabelText('Часовий пояс'), { target: { value: 'Europe/Kyiv' } });
    await saved();
    expect(body).toEqual({ daily_new_words_limit: 10, time_zone: 'Europe/Kyiv' }); expect(patches(fetcher)).toHaveLength(1);
    expect((patches(fetcher)[0][0] as Request).method).toBe('PATCH');
    expect((patches(fetcher)[0][0] as Request).url).toBe(`${window.location.origin}/api/v1/learners/me/settings`);
    expect(screen.getByText('Сьогодні:', { exact: false })).toHaveTextContent('5 нових слів');
    expect(screen.getByText('З наступної доби:', { exact: false })).toHaveTextContent('10 нових слів');
    expect(client.getQueryData(['current-learner'])).toEqual(updated);
  });
  it('нагадування вимикається через null без скасування відкладеного ліміту', async () => {
    let body: SettingsUpdate | undefined;
    const profile = { ...learner, pending_daily_new_words_limit: 10 as const };
    mockFetch(async request => { body = await request.json() as SettingsUpdate; return json({ ...profile, reminder: null }); }); form(profile);
    fireEvent.click(screen.getByLabelText('Нагадувати про заняття'));
    await saved(); expect(body).toEqual({ reminder: null }); expect(limitValue()).toBe(10);
  });
  it('вибір чинного ліміту скасовує відкладену зміну', async () => {
    let body: SettingsUpdate | undefined;
    mockFetch(async request => { body = await request.json() as SettingsUpdate; return json(learner); }); form({ ...learner, pending_daily_new_words_limit: 10 }); chooseLimit(5);
    await saved(); expect(body).toEqual({ daily_new_words_limit: 5 }); expect(screen.queryByText('З наступної доби:', { exact: false })).not.toBeInTheDocument();
  });
  it('кілька швидких натискань дають один запит', async () => {
    const fetcher = mockFetch(() => json({ ...learner, pending_daily_new_words_limit: 8 })); form();
    for (let i = 0; i < 3; i++) fireEvent.click(screen.getByRole('button', { name: 'Більше' }));
    await saved(); expect(patches(fetcher)).toHaveLength(1);
  });
});
describe('AC-27 і AC-45: валідація', () => {
  it.each([0, 1.5, 11])('не приймає денний ліміт %i', limit => {
    const draft = { ...draftFromLearner(learner), limit };
    expect(validateSettings(draft, learner).limit).toBe('Доступно від 1 до 10 нових слів на день.'); expect(settingsChanges(draft, learner).daily_new_words_limit).toBeUndefined();
  });
  it('степер не виходить за межі від 1 до 10', () => {
    form({ ...learner, daily_new_words_limit: 10 }); expect(screen.getByRole('button', { name: 'Більше' })).toBeDisabled(); expect(screen.getByRole('button', { name: 'Менше' })).toBeEnabled();
  });
  it('мінімальний ліміт блокує «Менше»', () => {
    form({ ...learner, daily_new_words_limit: 1 }); expect(screen.getByRole('button', { name: 'Менше' })).toBeDisabled();
  });
  it('нагадування без днів не надсилає PATCH', async () => {
    const fetcher = mockFetch(() => json(learner)); form(); fireEvent.click(screen.getByLabelText('Понеділок')); fireEvent.click(screen.getByLabelText('П’ятниця'));
    expect(screen.getByText('Виберіть хоча б один день або вимкніть нагадування.')).toBeVisible(); await settle(); expect(patches(fetcher)).toHaveLength(0);
  });
  it('непідтверджений пояс потребує ручного підтвердження перед нагадуванням', async () => {
    let body: SettingsUpdate | undefined;
    const profile: Learner = { ...learner, time_zone_confirmed: false, reminder: null };
    const fetcher = mockFetch(async request => { body = await request.json() as SettingsUpdate; return json(learner); }); form(profile);
    fireEvent.click(screen.getByLabelText('Нагадувати про заняття')); fireEvent.click(screen.getByLabelText('Понеділок'));
    expect(screen.getByText('Виберіть і підтвердьте часовий пояс.')).toBeVisible(); await settle(); expect(patches(fetcher)).toHaveLength(0);
    fireEvent.click(screen.getByRole('button', { name: 'Підтвердити часовий пояс' }));
    await saved(); expect(body).toEqual({ time_zone: 'Europe/Warsaw', reminder: { local_time: '19:00', days_of_week: ['mon'] } });
  });
  it('зміна ліміту без нагадування не підтверджує fallback-пояс автоматично', () => {
    const profile: Learner = { ...learner, reminder: null, time_zone_confirmed: false };
    const draft = { ...draftFromLearner(profile), limit: 3 };
    expect(validateSettings(draft, profile)).toEqual({}); expect(settingsChanges(draft, profile)).toEqual({ daily_new_words_limit: 3 });
  });
  it('невірний час не надсилається на сервер', async () => {
    const fetcher = mockFetch(() => json(learner)); form(); fireEvent.change(screen.getByLabelText('Час нагадування'), { target: { value: '' } });
    expect(screen.getByText('Виберіть коректний час нагадування.')).toBeVisible(); await settle(); expect(patches(fetcher)).toHaveLength(0);
  });
  it.each([
    ['settings.invalid_limit', 'Доступно від 1 до 10 нових слів на день.'],
    ['settings.time_zone_required', 'Виберіть і підтвердьте часовий пояс.'],
    ['settings.invalid_time_zone', 'Невідомий часовий пояс. Виберіть інший зі списку.'],
    ['settings.reminder_days_required', 'Виберіть хоча б один день або вимкніть нагадування.'],
    ['settings.invalid_reminder', 'Перевірте час і дні нагадування.'],
  ])('серверний 400 %s зберігає ввід і показує пояснення', async (code, message) => {
    mockFetch(() => json({ code, message: 'Серверне пояснення' }, 400)); form(); chooseLimit(3);
    await screen.findByText(message, {}, { timeout: 3000 }); expect(limitValue()).toBe(3);
  });
});

describe('AC-58: локальна тема, форма та кеш заняття', () => {
  it('зміна теми негайна, зберігається локально й не створює PATCH', async () => {
    const fetcher = mockFetch(() => json(learner)); const { client } = form();
    const session = { currentItemId: 'card-1', draftAnswer: 'spokój' }; client.setQueryData(['session', 'active'], session);
    fireEvent.click(screen.getByRole('radio', { name: /Темна/ }));
    expect(document.documentElement.dataset.theme).toBe('dark'); expect(localStorage.getItem(THEME_KEY)).toBe('dark'); expect(client.getQueryData(['session', 'active'])).toBe(session);
    await settle(); expect(patches(fetcher)).toHaveLength(0);
  });
  it('тема не скидає незбережене введення', () => {
    mockFetch(() => json(learner)); form(); fireEvent.change(screen.getByLabelText('Час нагадування'), { target: { value: '21:15' } }); chooseLimit(3);
    fireEvent.click(screen.getByRole('radio', { name: /Темна/ }));
    expect(screen.getByLabelText('Час нагадування')).toHaveValue('21:15'); expect(limitValue()).toBe(3);
  });
  it('ручна тема переважає themeChanged і зберігає змінену форму', () => {
    mockFetch(() => json(learner));
    let listener = () => undefined as void;
    const app = { colorScheme: 'light', onEvent: (_event: string, callback: () => void) => { listener = callback; }, offEvent: vi.fn() };
    window.Telegram = { WebApp: app }; form(); chooseLimit(3); fireEvent.click(screen.getByRole('radio', { name: /Темна/ }));
    act(() => { app.colorScheme = 'light'; listener(); });
    expect(document.documentElement.dataset.theme).toBe('dark'); expect(limitValue()).toBe(3);
  });
  it('недоступне сховище не блокує локальне перемикання', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new DOMException('Недоступно'); }); form();
    fireEvent.click(screen.getByRole('radio', { name: /Темна/ })); expect(document.documentElement.dataset.theme).toBe('dark');
  });
});

describe('Помилки й повторні дії', () => {
  it('поки запит виконується, дублікатів немає, а поля й тема лишаються доступними', async () => {
    let resolve!: (response: Response) => void;
    const fetcher = mockFetch(() => new Promise<Response>(complete => { resolve = complete; })); form(); chooseLimit(10);
    await waitFor(() => expect(patches(fetcher)).toHaveLength(1), { timeout: 3000 });
    expect(screen.getByRole('form', { name: 'Налаштування профілю' })).toHaveAttribute('aria-busy', 'true');
    expect(screen.getByRole('radio', { name: /Темна/ })).toBeEnabled(); expect(screen.getByRole('button', { name: 'Менше' })).toBeEnabled();
    await act(async () => { resolve(json({ ...learner, pending_daily_new_words_limit: 10 })); }); await saved(); expect(patches(fetcher)).toHaveLength(1);
  });
  it('мережева помилка зберігає змінені значення та дозволяє лише свідомий повтор', async () => {
    let attempts = 0;
    const fetcher = mockFetch(() => { if (++attempts === 1) throw new TypeError('Недоступна мережа'); return json({ ...learner, pending_daily_new_words_limit: 3 }); }); form(); chooseLimit(3); fireEvent.change(screen.getByLabelText('Час нагадування'), { target: { value: '20:00' } });
    await screen.findByText(/Ваші зміни залишилися на екрані/, {}, { timeout: 3000 }); expect(screen.getByLabelText('Час нагадування')).toHaveValue('20:00'); expect(limitValue()).toBe(3); expect(patches(fetcher)).toHaveLength(1);
    await settle(); expect(patches(fetcher)).toHaveLength(1);
    fireEvent.click(screen.getByRole('button', { name: 'Повторити' })); await saved(); expect(patches(fetcher)).toHaveLength(2);
  });
  it('PATCH 401 просить повернутися до Telegram без автоматичного повтору', async () => {
    const fetcher = mockFetch(() => json({ code: 'auth.invalid_init_data', message: 'Доступ заборонено.' }, 401)); form(); chooseLimit(3);
    await screen.findByText('Відкрийте тренер знову в Telegram, щоб продовжити.', {}, { timeout: 3000 }); expect(screen.getByRole('button', { name: 'Більше' })).toBeDisabled();
    await settle(); expect(patches(fetcher)).toHaveLength(1);
  });
  it('PATCH 404 дає явний перехід до початку без циклу й автоматичного онбордингу', async () => {
    const fetcher = mockFetch(() => json(notOnboarded, 404)); const { start } = form(); chooseLimit(3);
    await screen.findByText('Спершу налаштуйте тренер.', {}, { timeout: 3000 }); expect(start).not.toHaveBeenCalled(); expect(screen.getByRole('button', { name: 'Більше' })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: 'До початку' })); expect(start).toHaveBeenCalledOnce(); expect(patches(fetcher)).toHaveLength(1);
  });
});

describe('Маршрут і завантаження профілю', () => {
  it('прямий /settings не перенаправляє готовий профіль на /today', async () => {
    window.history.replaceState(null, '', '/settings'); mockFetch(() => json(learner)); mount(<App />);
    await screen.findByRole('heading', { name: 'Налаштування' }); expect(window.location.pathname).toBe('/settings');
  });
  it('навігація «Сьогодні» ↔ налаштування використовує той самий профіль', async () => {
    window.history.replaceState(null, '', '/today'); const fetcher = mockFetch(request => request.url.endsWith('/today') ? json(todaySummary) : request.url.endsWith('/client-timings') ? new Response(null, { status: 204 }) : json(learner)); mount(<App />);
    await screen.findByRole('heading', { name: 'Сьогодні' }); fireEvent.click(screen.getByRole('button', { name: 'Налаштування' }));
    await screen.findByRole('heading', { name: 'Налаштування' }); fireEvent.click(screen.getByRole('button', { name: '← До «Сьогодні»' }));
    await screen.findByRole('heading', { name: 'Сьогодні' }); expect(fetcher.mock.calls.map(call => call[0] as Request).filter(request => request.url.endsWith('/learners/me'))).toHaveLength(1);
  });
  it('GET 401 не відкриває форму налаштувань або онбордингу', async () => {
    window.history.replaceState(null, '', '/settings'); const fetcher = mockFetch(() => json({ code: 'auth.invalid_init_data', message: 'Недоступно' }, 401)); mount(<App />);
    await screen.findByText('Відкрийте тренер знову в Telegram, щоб продовжити.'); expect(screen.queryByRole('form')).not.toBeInTheDocument(); expect(fetcher).toHaveBeenCalledOnce();
  });
  it('GET 404 на /settings потребує явного переходу до початку', async () => {
    window.history.replaceState(null, '', '/settings'); const fetcher = mockFetch(() => json(notOnboarded, 404)); mount(<App privacyContact={parsePrivacyContact('mailto:privacy@example.test')} />);
    await screen.findByText('Спершу налаштуйте тренер.'); expect(screen.queryByRole('form')).not.toBeInTheDocument(); expect(window.location.pathname).toBe('/settings');
    fireEvent.click(screen.getByRole('button', { name: 'До початку' })); await screen.findByRole('form', { name: 'Налаштування навчання' }); expect(fetcher).toHaveBeenCalledOnce();
  });
});

describe('Вхід до редактора словника', () => {
  const settingsApp = (access: () => Response) => {
    window.history.replaceState(null, '', '/settings');
    const fetcher = mockFetch(request => request.url.endsWith('/learners/me/access') ? access() : json(learner)); mount(<App />); return fetcher;
  };
  it('редактору показує рядок «Редактор словника», тап відкриває /editor', async () => {
    settingsApp(() => json({ content_editor: true }));
    fireEvent.click(await screen.findByRole('button', { name: 'Редактор словника' }));
    await waitFor(() => expect(window.location.pathname).toBe('/editor'));
  });
  it.each([
    ['не редактору', () => json({ content_editor: false })],
    ['при помилці перевірки ролі', () => json({ code: 'server.unavailable', message: 'Недоступно' }, 503)],
  ])('%s рядок прихований', async (_name, access) => {
    const fetcher = settingsApp(access);
    await screen.findByRole('heading', { name: 'Налаштування' });
    await waitFor(() => expect(fetcher.mock.calls.some(call => (call[0] as Request).url.endsWith('/learners/me/access'))).toBe(true));
    await settle(); expect(screen.queryByRole('button', { name: 'Редактор словника' })).not.toBeInTheDocument();
  });
});
