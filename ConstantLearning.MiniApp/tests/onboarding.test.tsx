import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { QueryClientProvider } from '@tanstack/react-query';
import { ThemeProvider } from '../src/theme/ThemeProvider';
import { createQueryClient } from '../src/api/query';
import { Onboarding } from '../src/features/onboarding/Onboarding';
import { OnboardingGate } from '../src/features/onboarding/OnboardingGate';
import { parsePrivacyContact, resolvePrivacyContact } from '../src/features/onboarding/privacy';
import type { ApiErrorBody, Learner, OnboardingRequest } from '../src/features/onboarding/api';
import { themeStore } from '../src/theme/store';
import { todaySummary } from './fixtures/today';

// Синтетичний контакт використовується лише в локальних тестах.
const contact = parsePrivacyContact('mailto:privacy@example.test')!;
const learner: Learner = { id: '0192f0a0-0000-7000-8000-000000000001', first_name: 'Тест', time_zone: 'Europe/Warsaw', time_zone_confirmed: true, daily_new_words_limit: 5, pending_daily_new_words_limit: null, reminder: null, onboarded_at: '2026-11-01T09:00:00Z' };
const notOnboarded: ApiErrorBody = { code: 'learner.not_onboarded', message: 'Спершу налаштуйте тренер.' };
const clients: ReturnType<typeof createQueryClient>[] = [];

function json(body: Learner | ApiErrorBody | typeof todaySummary, status: number) { return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }); }
function mockFetch(handler: (request: Request) => Promise<Response> | Response) {
  const fetcher = vi.fn<typeof fetch>().mockImplementation(request => Promise.resolve(handler(request as Request)));
  vi.stubGlobal('fetch', fetcher);
  return fetcher;
}
function mount(element: React.ReactNode) {
  const client = createQueryClient(); clients.push(client);
  return render(<QueryClientProvider client={client}><ThemeProvider>{element}</ThemeProvider></QueryClientProvider>);
}
function form(onComplete = vi.fn(), zone: string | null = 'Europe/Warsaw') {
  mount(<Onboarding onComplete={onComplete} privacyContact={contact} getDeviceTimeZone={() => zone ?? undefined} />);
  return onComplete;
}
function submit() { fireEvent.submit(screen.getByRole('form', { name: 'Налаштування навчання' })); }
function enableReminder() { fireEvent.click(screen.getByLabelText('Нагадувати про заняття')); }
const limitValue = () => Number.parseInt(within(screen.getByRole('group', { name: 'Нових слів на день' })).getByRole('status').textContent ?? '', 10);
function chooseLimit(value: number) {
  while (limitValue() !== value) fireEvent.click(screen.getByRole('button', { name: limitValue() < value ? 'Більше' : 'Менше' }));
}

afterEach(() => { clients.forEach(client => client.clear()); clients.length = 0; vi.unstubAllGlobals(); vi.restoreAllMocks(); window.history.replaceState(null, '', '/'); });

describe('Приватність перед початком', () => {
  it('без перевизначення використовує погоджений mailto, а порожнє чи невалідне значення блокує початок', () => {
    expect(resolvePrivacyContact(undefined)?.url).toBe('mailto:melnichenkoviatcheslav@gmail.com');
    expect(resolvePrivacyContact('')).toBeUndefined();
    expect(resolvePrivacyContact('invalid')).toBeUndefined();
    expect(resolvePrivacyContact('mailto:privacy@example.test')?.url).toBe('mailto:privacy@example.test');
  });
  it('показує склад даних, строки видалення та контакт перед кнопкою початку', () => {
    form();
    const privacy = screen.getByRole('heading', { name: 'Ваші дані — під вашим контролем' });
    expect(privacy.compareDocumentPosition(screen.getByRole('button', { name: 'Почати навчання' })) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(screen.getByText(/Telegram-ідентифікатор, налаштування та навчальну історію/)).toBeVisible();
    expect(screen.getByText(/7 календарних днів.*14 днів/)).toBeVisible();
    expect(screen.getByRole('link', { name: /Запит на видалення/ })).toHaveAttribute('href', 'mailto:privacy@example.test');
  });
  it('без справжньої конфігурації не дає завершити онбординг', () => {
    const fetcher = mockFetch(() => json(learner, 201));
    mount(<Onboarding onComplete={vi.fn()} privacyContact={null} />);
    expect(screen.getByText('Почати навчання зараз не вдається. Спробуйте пізніше.')).toBeVisible();
    expect(screen.getByRole('button', { name: 'Почати навчання' })).toBeDisabled();
    expect(screen.queryByRole('link', { name: /Запит на видалення/ })).not.toBeInTheDocument();
    submit(); expect(fetcher).not.toHaveBeenCalled();
  });
  it.each(['', 'javascript:alert(1)', 'http://t.me/example', 'mailto:wrong', 'mailto:privacy@example.test?body=data', 'mailto:privacy%0a@example.test', 'https://user:password@example.test'])('ігнорує невалідний контакт %s', value => expect(parsePrivacyContact(value)).toBeUndefined());
  it.each(['mailto:privacy@example.test', 'https://t.me/synthetic_test_owner', 'https://example.test/contact'])('приймає явно заданий контакт %s', value => expect(parsePrivacyContact(value)?.url).toBe(value));
});

describe('Новий learner і форма', () => {
  it.each([3, 5, 10] as const)('201: зберігає %i слів, пояс пристрою й пропущене нагадування', async limit => {
    let body: OnboardingRequest | undefined;
    mockFetch(async request => { body = await request.json() as OnboardingRequest; return json({ ...learner, daily_new_words_limit: limit }, 201); });
    const complete = form();
    chooseLimit(limit);
    submit();
    await waitFor(() => expect(complete).toHaveBeenCalledOnce());
    expect(body).toEqual({ daily_new_words_limit: limit, time_zone: 'Europe/Warsaw', reminder: null });
  });
  it('AC-02: без днів пояснює помилку й не надсилає запит', () => {
    const fetcher = mockFetch(() => json(learner, 201)); form(); enableReminder(); submit();
    expect(screen.getByText('Виберіть хоча б один день або вимкніть нагадування.')).toBeVisible();
    expect(fetcher).not.toHaveBeenCalled();
  });
  it('AC-27: нагадування без визначеного поясу потребує вибору поясу, вибраний вручну пояс підтверджений', async () => {
    let body: OnboardingRequest | undefined;
    const fetcher = mockFetch(async request => { body = await request.json() as OnboardingRequest; return json(learner, 201); });
    const complete = form(vi.fn(), null); enableReminder(); fireEvent.click(screen.getByLabelText('Понеділок')); submit();
    expect(screen.getByText('Виберіть і підтвердьте часовий пояс для нагадування.')).toBeVisible(); expect(fetcher).not.toHaveBeenCalled();
    fireEvent.change(screen.getByLabelText('Часовий пояс'), { target: { value: 'Europe/Kyiv' } });
    fireEvent.change(screen.getByLabelText('Час нагадування'), { target: { value: '18:30' } });
    fireEvent.click(screen.getByLabelText('П’ятниця')); submit();
    await waitFor(() => expect(complete).toHaveBeenCalledOnce());
    expect(body).toEqual({ daily_new_words_limit: 5, time_zone: 'Europe/Kyiv', reminder: { local_time: '18:30', days_of_week: ['mon', 'fri'] } });
  });
  it('без визначеного поясу й без нагадування надсилає null для fallback Варшави', async () => {
    let body: OnboardingRequest | undefined;
    mockFetch(async request => { body = await request.json() as OnboardingRequest; return json({ ...learner, time_zone_confirmed: false }, 201); });
    const complete = form(vi.fn(), null); submit();
    await waitFor(() => expect(complete).toHaveBeenCalledOnce());
    expect(body?.time_zone).toBeNull();
  });
  it('нагадування можна вимкнути після помилки без днів', async () => {
    mockFetch(() => json(learner, 201)); const complete = form(); enableReminder(); submit();
    enableReminder(); submit();
    await waitFor(() => expect(complete).toHaveBeenCalledOnce());
  });
  it('pending блокує повторне надсилання навіть до наступного рендера', async () => {
    let resolve!: (response: Response) => void;
    const fetcher = mockFetch(() => new Promise<Response>(complete => { resolve = complete; }));
    const complete = form();
    act(() => { submit(); submit(); });
    await waitFor(() => expect(fetcher).toHaveBeenCalledOnce());
    expect(screen.getByRole('button', { name: 'Зберігаємо налаштування…' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Більше' })).toBeDisabled();
    await act(async () => { resolve(json(learner, 201)); });
    await waitFor(() => expect(complete).toHaveBeenCalledOnce());
  });
  it('тимчасова помилка зберігає введення та дозволяє свідомий повтор', async () => {
    let attempts = 0;
    const fetcher = mockFetch(() => ++attempts === 1 ? json({ code: 'server.unavailable', message: 'Недоступно' }, 503) : json(learner, 201));
    const complete = form(); chooseLimit(10); enableReminder(); fireEvent.click(screen.getByLabelText('Середа')); fireEvent.change(screen.getByLabelText('Час нагадування'), { target: { value: '20:15' } }); submit();
    await screen.findByText(/Ваш вибір збережено на екрані/);
    expect(limitValue()).toBe(10); expect(screen.getByLabelText('Середа')).toBeChecked(); expect(screen.getByLabelText('Час нагадування')).toHaveValue('20:15');
    expect(fetcher).toHaveBeenCalledOnce(); submit(); await waitFor(() => expect(complete).toHaveBeenCalledOnce()); expect(fetcher).toHaveBeenCalledTimes(2);
  });
  it('помилка мережі не скидає ліміт і не повторює мутацію автоматично', async () => {
    const fetcher = mockFetch(() => { throw new TypeError('Недоступна мережа'); }); form(); chooseLimit(3); submit();
    await screen.findByText(/Ваш вибір збережено на екрані/); expect(limitValue()).toBe(3); expect(fetcher).toHaveBeenCalledOnce();
  });
  it('зміна теми зберігає форму та не викликає API', () => {
    const fetcher = mockFetch(() => json(learner, 201)); form(); enableReminder(); fireEvent.click(screen.getByLabelText('Понеділок')); fireEvent.change(screen.getByLabelText('Час нагадування'), { target: { value: '18:15' } });
    act(() => themeStore.setTheme('dark'));
    expect(screen.getByLabelText('Понеділок')).toBeChecked(); expect(screen.getByLabelText('Час нагадування')).toHaveValue('18:15'); expect(fetcher).not.toHaveBeenCalled();
  });
  it.each([
    ['settings.reminder_days_required', 'Виберіть хоча б один день або вимкніть нагадування.'],
    ['settings.time_zone_required', 'Виберіть і підтвердьте часовий пояс для нагадування.'],
    ['settings.invalid_reminder', 'Перевірте час і дні нагадування.'],
    ['settings.invalid_limit', 'Виберіть від 1 до 10 нових слів на день.'],
  ])('400: точний код %s має відповідне пояснення', async (code, message) => {
    mockFetch(() => json({ code, message: 'Серверне пояснення' }, 400)); form(); submit(); await screen.findByText(message);
  });
  it('401 після submit просить повернутися до Telegram і не повторює запит', async () => {
    const fetcher = mockFetch(() => json({ code: 'auth.invalid_init_data', message: 'Доступ заборонено.' }, 401)); form(); submit();
    await screen.findByText('Відкрийте тренер знову в Telegram, щоб продовжити.');
    expect(screen.getByRole('button', { name: 'Почати навчання' })).toBeDisabled(); submit(); expect(fetcher).toHaveBeenCalledOnce();
  });
  it('AC-53: 200 повертає наявний профіль замість вибраних нових налаштувань', async () => {
    const existing: Learner = { ...learner, daily_new_words_limit: 3, reminder: { local_time: '18:00', days_of_week: ['wed'] } };
    mockFetch(() => json(existing, 200)); const complete = form(); chooseLimit(10); submit();
    await waitFor(() => expect(complete).toHaveBeenCalledWith(existing));
  });
});

describe('Завантаження профілю та перехід', () => {
  it('точний 404 learner.not_onboarded відкриває форму', async () => {
    mockFetch(() => json(notOnboarded, 404)); mount(<OnboardingGate privacyContact={contact} />);
    await screen.findByRole('form', { name: 'Налаштування навчання' });
  });
  it('інший 404 не запускає новий онбординг', async () => {
    mockFetch(() => json({ code: 'learner.unavailable', message: 'Немає.' }, 404)); mount(<OnboardingGate privacyContact={contact} />);
    await screen.findByText('Не вдалося відкрити тренер. Спробуйте ще раз.'); expect(screen.queryByRole('form')).not.toBeInTheDocument();
  });
  it('401 не створює auth-loop і не показує форму чи автоматичний повтор', async () => {
    const fetcher = mockFetch(() => json({ code: 'auth.invalid_init_data', message: 'Доступ заборонено.' }, 401)); mount(<OnboardingGate privacyContact={contact} />);
    await screen.findByText('Відкрийте тренер знову в Telegram, щоб продовжити.'); expect(screen.queryByRole('button', { name: 'Спробувати ще раз' })).not.toBeInTheDocument(); expect(screen.queryByRole('form')).not.toBeInTheDocument(); expect(fetcher).toHaveBeenCalledOnce();
  });
  it('GET 200 відразу веде на today callback без POST і зміни профілю', async () => {
    const fetcher = mockFetch(request => request.url.endsWith('/today') ? json(todaySummary, 200) : request.url.endsWith('/client-timings') ? new Response(null, { status: 204 }) : json(learner, 200)); const navigate = vi.fn(); mount(<OnboardingGate privacyContact={contact} onNavigateToToday={navigate} />);
    await screen.findByRole('heading', { name: 'Сьогодні' }); await waitFor(() => expect(navigate).toHaveBeenCalledExactlyOnceWith(learner)); expect(window.location.pathname).toBe('/today');
    const profileRequests = fetcher.mock.calls.map(call => call[0] as Request).filter(request => request.url.endsWith('/learners/me'));
    expect(profileRequests).toHaveLength(1); expect(profileRequests[0].method).toBe('GET');
    expect(fetcher.mock.calls.map(call => call[0] as Request).filter(request => request.method === 'POST' && !request.url.endsWith('/client-timings'))).toHaveLength(0);
  });
  it('GET 404, потім POST 201 викликає today callback один раз', async () => {
    mockFetch(request => request.url.endsWith('/today') ? json(todaySummary, 200) : request.url.endsWith('/client-timings') ? new Response(null, { status: 204 }) : request.method === 'GET' ? json(notOnboarded, 404) : json(learner, 201)); const navigate = vi.fn(); mount(<OnboardingGate privacyContact={contact} onNavigateToToday={navigate} />);
    await screen.findByRole('form'); submit(); await screen.findByRole('heading', { name: 'Сьогодні' }); expect(navigate).toHaveBeenCalledExactlyOnceWith(learner);
  });
});
