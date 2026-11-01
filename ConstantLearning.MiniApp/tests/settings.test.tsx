import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { QueryClientProvider } from '@tanstack/react-query';
import { createQueryClient } from '../src/api/query';
import { ThemeProvider } from '../src/theme/ThemeProvider';
import { THEME_KEY } from '../src/theme/store';
import { App } from '../src/App';
import Settings from '../src/features/settings/Settings';
import { draftFromLearner, settingsChanges, validateSettings, type Learner, type SettingsUpdate } from '../src/features/settings/model';
import type { ApiErrorBody } from '../src/features/onboarding/api';
import { parsePrivacyContact } from '../src/features/onboarding/privacy';

const learner: Learner = { id: '0192f0a0-0000-7000-8000-000000000001', first_name: 'Тест', time_zone: 'Europe/Warsaw', time_zone_confirmed: true, daily_new_words_limit: 5, pending_daily_new_words_limit: null, reminder: { local_time: '19:00', days_of_week: ['mon', 'fri'] }, onboarded_at: '2026-11-01T09:00:00Z' };
const notOnboarded: ApiErrorBody = { code: 'learner.not_onboarded', message: 'Спершу налаштуйте тренер.' };
const clients: ReturnType<typeof createQueryClient>[] = [];
function json(body: Learner | ApiErrorBody, status = 200) { return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }); }
function mockFetch(handler: (request: Request) => Response | Promise<Response>) {
  const fetcher = vi.fn<typeof fetch>().mockImplementation(request => Promise.resolve(handler(request as Request)));
  vi.stubGlobal('fetch', fetcher); return fetcher;
}
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
function submit() { fireEvent.submit(screen.getByRole('form', { name: 'Налаштування профілю' })); }
function chooseLimit(value: 3 | 5 | 10) { fireEvent.click(screen.getByRole('radio', { name: `${value} ${value === 3 ? 'нові слова' : 'нових слів'}` })); }
afterEach(() => { clients.forEach(client => client.clear()); clients.length = 0; vi.unstubAllGlobals(); vi.restoreAllMocks(); delete window.Telegram; window.history.replaceState(null, '', '/'); });

describe('AC-44: чинний і відкладений темп', () => {
  it('показує чинний ліміт окремо й обирає вже відкладений ліміт', () => {
    const fetcher = mockFetch(() => json(learner)); form({ ...learner, pending_daily_new_words_limit: 10 });
    expect(screen.getByText('Сьогодні:', { exact: false })).toHaveTextContent('5 нових слів');
    expect(screen.getByText('З наступної доби:', { exact: false })).toHaveTextContent('10 нових слів');
    expect(screen.getByRole('radio', { name: '10 нових слів' })).toBeChecked();
    expect(screen.getByRole('button', { name: 'Зберегти зміни' })).toBeDisabled(); expect(fetcher).not.toHaveBeenCalled();
  });
  it('зберігає новий ліміт і підтверджений пояс, залишаючи чинний ліміт у відповіді сервера', async () => {
    let body: SettingsUpdate | undefined;
    const updated: Learner = { ...learner, time_zone: 'Europe/Kyiv', pending_daily_new_words_limit: 10 };
    const fetcher = mockFetch(async request => { body = await request.json() as SettingsUpdate; return json(updated); });
    const { client } = form(); chooseLimit(10); fireEvent.change(screen.getByLabelText('Часовий пояс'), { target: { value: 'Europe/Kyiv' } });
    submit(); expect(screen.getByText('Виберіть і підтвердьте часовий пояс.')).toBeVisible(); expect(fetcher).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: 'Підтвердити часовий пояс' })); submit();
    await screen.findByText('Налаштування збережено.');
    expect(body).toEqual({ daily_new_words_limit: 10, time_zone: 'Europe/Kyiv' });
    expect((fetcher.mock.calls[0][0] as Request).method).toBe('PATCH');
    expect((fetcher.mock.calls[0][0] as Request).url).toBe(`${window.location.origin}/api/v1/learners/me/settings`);
    expect(screen.getByText('Сьогодні:', { exact: false })).toHaveTextContent('5 нових слів');
    expect(screen.getByText('З наступної доби:', { exact: false })).toHaveTextContent('10 нових слів');
    expect(client.getQueryData(['current-learner'])).toEqual(updated);
  });
  it('нагадування вимикається через null без скасування відкладеного ліміту', async () => {
    let body: SettingsUpdate | undefined;
    const profile = { ...learner, pending_daily_new_words_limit: 10 as const };
    mockFetch(async request => { body = await request.json() as SettingsUpdate; return json({ ...profile, reminder: null }); }); form(profile);
    fireEvent.click(screen.getByLabelText('Нагадувати про заняття')); submit();
    await screen.findByText('Налаштування збережено.'); expect(body).toEqual({ reminder: null });
    expect(screen.getByRole('radio', { name: '10 нових слів' })).toBeChecked();
  });
  it('вибір чинного ліміту скасовує відкладену зміну', async () => {
    let body: SettingsUpdate | undefined;
    mockFetch(async request => { body = await request.json() as SettingsUpdate; return json(learner); }); form({ ...learner, pending_daily_new_words_limit: 10 }); chooseLimit(5); submit();
    await screen.findByText('Налаштування збережено.'); expect(body).toEqual({ daily_new_words_limit: 5 }); expect(screen.queryByText('З наступної доби:', { exact: false })).not.toBeInTheDocument();
  });
});

describe('AC-27 і AC-45: валідація', () => {
  it.each([0, 4, 7, 11])('не приймає денний ліміт %i', limit => {
    const draft = { ...draftFromLearner(learner), limit };
    expect(validateSettings(draft, learner).limit).toBe('Доступні лише 3, 5 або 10 нових слів на день.'); expect(settingsChanges(draft, learner).daily_new_words_limit).toBeUndefined();
  });
  it('на екрані доступні лише три числові варіанти', () => {
    form(); const group = screen.getByRole('group', { name: 'Денний ліміт нових слів' });
    expect(within(group).getAllByRole('radio').map(radio => radio.getAttribute('aria-label'))).toEqual(['3 нові слова', '5 нових слів', '10 нових слів']);
  });
  it('нагадування без днів не надсилає PATCH', () => {
    const fetcher = mockFetch(() => json(learner)); form(); fireEvent.click(screen.getByLabelText('Понеділок')); fireEvent.click(screen.getByLabelText('П’ятниця')); submit();
    expect(screen.getByText('Виберіть хоча б один день або вимкніть нагадування.')).toBeVisible(); expect(fetcher).not.toHaveBeenCalled();
  });
  it('непідтверджений пояс потребує ручного підтвердження перед нагадуванням', async () => {
    let body: SettingsUpdate | undefined;
    const profile: Learner = { ...learner, time_zone_confirmed: false, reminder: null };
    mockFetch(async request => { body = await request.json() as SettingsUpdate; return json(learner); }); form(profile);
    fireEvent.click(screen.getByLabelText('Нагадувати про заняття')); fireEvent.click(screen.getByLabelText('Понеділок')); submit();
    expect(screen.getByText('Виберіть і підтвердьте часовий пояс.')).toBeVisible();
    fireEvent.click(screen.getByRole('button', { name: 'Підтвердити часовий пояс' })); submit();
    await screen.findByText('Налаштування збережено.'); expect(body).toEqual({ time_zone: 'Europe/Warsaw', reminder: { local_time: '19:00', days_of_week: ['mon'] } });
  });
  it('зміна ліміту без нагадування не підтверджує fallback-пояс автоматично', () => {
    const profile: Learner = { ...learner, reminder: null, time_zone_confirmed: false };
    const draft = { ...draftFromLearner(profile), limit: 3 };
    expect(validateSettings(draft, profile)).toEqual({}); expect(settingsChanges(draft, profile)).toEqual({ daily_new_words_limit: 3 });
  });
  it('невірний час не надсилається на сервер', () => {
    const fetcher = mockFetch(() => json(learner)); form(); fireEvent.change(screen.getByLabelText('Час нагадування'), { target: { value: '' } }); submit();
    expect(screen.getByText('Виберіть коректний час нагадування.')).toBeVisible(); expect(fetcher).not.toHaveBeenCalled();
  });
  it.each([
    ['settings.invalid_limit', 'Доступні лише 3, 5 або 10 нових слів на день.'],
    ['settings.time_zone_required', 'Виберіть і підтвердьте часовий пояс.'],
    ['settings.invalid_time_zone', 'Невідомий часовий пояс. Виберіть інший зі списку.'],
    ['settings.reminder_days_required', 'Виберіть хоча б один день або вимкніть нагадування.'],
    ['settings.invalid_reminder', 'Перевірте час і дні нагадування.'],
  ])('серверний 400 %s зберігає ввід і показує пояснення', async (code, message) => {
    mockFetch(() => json({ code, message: 'Серверне пояснення' }, 400)); form(); chooseLimit(3); submit();
    await screen.findByText(message); expect(screen.getByRole('radio', { name: '3 нові слова' })).toBeChecked();
  });
});

describe('AC-58: локальна тема, форма та кеш заняття', () => {
  it('зміна теми негайна, зберігається локально й не створює PATCH', () => {
    const fetcher = mockFetch(() => json(learner)); const { client } = form();
    const session = { currentItemId: 'card-1', draftAnswer: 'spokój' }; client.setQueryData(['session', 'active'], session);
    chooseLimit(3); fireEvent.change(screen.getByLabelText('Час нагадування'), { target: { value: '18:30' } }); fireEvent.click(screen.getByRole('radio', { name: /Темна/ }));
    expect(document.documentElement.dataset.theme).toBe('dark'); expect(localStorage.getItem(THEME_KEY)).toBe('dark');
    expect(screen.getByLabelText('Час нагадування')).toHaveValue('18:30'); expect(screen.getByRole('radio', { name: '3 нові слова' })).toBeChecked(); expect(client.getQueryData(['session', 'active'])).toBe(session); expect(fetcher).not.toHaveBeenCalled();
  });
  it('лише тема не робить серверну форму зміненою', () => {
    const fetcher = mockFetch(() => json(learner)); form(); fireEvent.click(screen.getByRole('radio', { name: /Темна/ })); submit();
    expect(screen.getByRole('button', { name: 'Зберегти зміни' })).toBeDisabled(); expect(fetcher).not.toHaveBeenCalled();
  });
  it('ручна тема переважає themeChanged і зберігає змінену форму', () => {
    let listener = () => undefined as void;
    const app = { colorScheme: 'light', onEvent: (_event: string, callback: () => void) => { listener = callback; }, offEvent: vi.fn() };
    window.Telegram = { WebApp: app }; form(); chooseLimit(3); fireEvent.click(screen.getByRole('radio', { name: /Темна/ }));
    act(() => { app.colorScheme = 'light'; listener(); });
    expect(document.documentElement.dataset.theme).toBe('dark'); expect(screen.getByRole('radio', { name: '3 нові слова' })).toBeChecked();
  });
  it('portal використовує спільну тему, не скидаючи вводу й кешу', () => {
    const fetcher = mockFetch(() => json(learner)); const { client } = form(); const session = { currentItemId: 'card-2', draftAnswer: 'dom' }; client.setQueryData(['session', 'active'], session);
    fireEvent.change(screen.getByLabelText('Час нагадування'), { target: { value: '21:15' } }); fireEvent.click(screen.getByRole('button', { name: 'Про оформлення' }));
    const dialog = screen.getByRole('dialog'); expect(dialog.parentElement).toBe(document.body);
    fireEvent.click(within(dialog).getByRole('radio', { name: /Світла/ })); expect(document.documentElement.dataset.theme).toBe('light');
    expect(screen.getByLabelText('Час нагадування')).toHaveValue('21:15'); expect(client.getQueryData(['session', 'active'])).toBe(session); expect(fetcher).not.toHaveBeenCalled();
  });
  it('недоступне сховище не блокує локальне перемикання', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new DOMException('Недоступно'); }); form();
    fireEvent.click(screen.getByRole('radio', { name: /Темна/ })); expect(document.documentElement.dataset.theme).toBe('dark');
  });
});

describe('Помилки й повторні дії', () => {
  it('pending блокує дублікати й поля, лишаючи тему доступною', async () => {
    let resolve!: (response: Response) => void;
    const fetcher = mockFetch(() => new Promise<Response>(complete => { resolve = complete; })); form(); chooseLimit(10);
    act(() => { submit(); submit(); }); await waitFor(() => expect(fetcher).toHaveBeenCalledOnce());
    expect(screen.getByRole('button', { name: 'Зберігаємо зміни…' })).toBeDisabled(); expect(screen.getByRole('radio', { name: '3 нові слова' })).toBeDisabled(); expect(screen.getByRole('radio', { name: /Темна/ })).toBeEnabled();
    await act(async () => { resolve(json({ ...learner, pending_daily_new_words_limit: 10 })); }); await screen.findByText('Налаштування збережено.');
  });
  it('мережева помилка зберігає змінені значення та дозволяє лише свідомий повтор', async () => {
    let attempts = 0;
    const fetcher = mockFetch(() => { if (++attempts === 1) throw new TypeError('Недоступна мережа'); return json({ ...learner, pending_daily_new_words_limit: 3 }); }); form(); chooseLimit(3); fireEvent.change(screen.getByLabelText('Час нагадування'), { target: { value: '20:00' } }); submit();
    await screen.findByText(/Ваші зміни залишилися на екрані/); expect(screen.getByLabelText('Час нагадування')).toHaveValue('20:00'); expect(screen.getByRole('radio', { name: '3 нові слова' })).toBeChecked(); expect(fetcher).toHaveBeenCalledOnce();
    submit(); await screen.findByText('Налаштування збережено.'); expect(fetcher).toHaveBeenCalledTimes(2);
  });
  it('PATCH 401 просить повернутися до Telegram без автоматичного повтору', async () => {
    const fetcher = mockFetch(() => json({ code: 'auth.invalid_init_data', message: 'Доступ заборонено.' }, 401)); form(); chooseLimit(3); submit();
    await screen.findByText('Відкрийте тренер знову в Telegram, щоб продовжити.'); expect(screen.getByRole('button', { name: 'Зберегти зміни' })).toBeDisabled(); submit(); expect(fetcher).toHaveBeenCalledOnce();
  });
  it('PATCH 404 дає явний перехід до початку без циклу й автоматичного онбордингу', async () => {
    const fetcher = mockFetch(() => json(notOnboarded, 404)); const { start } = form(); chooseLimit(3); submit();
    await screen.findByText('Спершу налаштуйте тренер.'); expect(start).not.toHaveBeenCalled(); expect(screen.getByRole('button', { name: 'Зберегти зміни' })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: 'До початку' })); expect(start).toHaveBeenCalledOnce(); expect(fetcher).toHaveBeenCalledOnce();
  });
});

describe('Маршрут і завантаження профілю', () => {
  it('прямий /settings не перенаправляє готовий профіль на /today', async () => {
    window.history.replaceState(null, '', '/settings'); mockFetch(() => json(learner)); mount(<App />);
    await screen.findByRole('heading', { name: 'Налаштування' }); expect(window.location.pathname).toBe('/settings');
  });
  it('навігація «Сьогодні» ↔ налаштування використовує той самий профіль', async () => {
    window.history.replaceState(null, '', '/today'); const fetcher = mockFetch(() => json(learner)); mount(<App />);
    await screen.findByRole('heading', { name: 'Сьогодні' }); fireEvent.click(screen.getByRole('button', { name: 'Налаштування' }));
    await screen.findByRole('heading', { name: 'Налаштування' }); fireEvent.click(screen.getByRole('button', { name: '← До «Сьогодні»' }));
    await screen.findByRole('heading', { name: 'Сьогодні' }); expect(fetcher).toHaveBeenCalledOnce();
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
