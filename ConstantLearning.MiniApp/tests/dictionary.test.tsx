import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { QueryClientProvider } from '@tanstack/react-query';
import { App } from '../src/App';
import { createQueryClient } from '../src/api/query';
import { ThemeProvider } from '../src/theme/ThemeProvider';
import { themeStore } from '../src/theme/store';
import Dictionary from '../src/features/dictionary/search/Dictionary';
import type { EntryCard, SearchPage } from '../src/features/dictionary/search/api';
import type { Learner } from '../src/features/onboarding/api';

const firstId = '0192f0a0-0000-7000-8000-0000000000e1';
const secondId = '0192f0a0-0000-7000-8000-0000000000e2';
const item: SearchPage['items'][number] = { entry_id: firstId, lemma_pl: 'urząd', part_of_speech: 'noun', short_meaning_uk: 'державна установа', matched_form: 'urzedzie', word_status: 'review' };
const page: SearchPage = { items: [item], has_next: false, has_prev: false, next_cursor: null };
const card: EntryCard = { entry_id: firstId, lemma_pl: 'urząd', part_of_speech: 'noun', gender: 'm', translations_uk: [{ kind: 'canonical', text_uk: 'державна установа' }, { kind: 'variant', text_uk: 'офіс' }, { kind: 'local', text_uk: 'ужонд' }], pronunciation_uk: 'ужонд', example_pl: 'Idę do urzędu.', example_uk: 'Я йду до державної установи.', word_status: 'review' };
const learner: Learner = { id: '0192f0a0-0000-7000-8000-000000000001', first_name: 'Тест', time_zone: 'Europe/Warsaw', time_zone_confirmed: true, daily_new_words_limit: 5, pending_daily_new_words_limit: null, reminder: null, onboarded_at: '2026-11-01T09:00:00Z' };
const clients: ReturnType<typeof createQueryClient>[] = [];
function json(body: unknown, status = 200) { return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }); }
function mockFetch(handler: (request: Request) => Response | Promise<Response>) {
  const fetcher = vi.fn<typeof fetch>().mockImplementation(request => Promise.resolve(handler(request as Request)));
  vi.stubGlobal('fetch', fetcher); return fetcher;
}
function mount(element: React.ReactNode = <Dictionary onBack={vi.fn()} />) {
  const client = createQueryClient(); clients.push(client);
  return render(<QueryClientProvider client={client}><ThemeProvider>{element}</ThemeProvider></QueryClientProvider>);
}
function query(value: string) { fireEvent.change(screen.getByRole('searchbox', { name: 'Слово для пошуку' }), { target: { value } }); }
function deferred() { let resolve!: (response: Response) => void; const promise = new Promise<Response>(complete => { resolve = complete; }); return { promise, resolve }; }

afterEach(() => { clients.forEach(client => client.clear()); clients.length = 0; vi.unstubAllGlobals(); vi.restoreAllMocks(); delete window.Telegram; window.history.replaceState(null, '', '/'); });

describe('AC-30: пошук і картка', () => {
  it('порожнє поле й пробіли не запитують API; очистка повертає початковий стан', async () => {
    const fetcher = mockFetch(() => json(page)); mount();
    expect(screen.getByRole('heading', { name: 'З чого почнемо?' })).toBeVisible(); query('   '); expect(fetcher).not.toHaveBeenCalled();
    query('u'); await screen.findByRole('button', { name: /urząd/ }); fireEvent.click(screen.getByRole('button', { name: 'Очистити' }));
    expect(screen.getByRole('searchbox')).toHaveValue(''); expect(screen.getByRole('heading', { name: 'З чого почнемо?' })).toBeVisible(); expect(screen.queryByRole('button', { name: /urząd/ })).not.toBeInTheDocument();
  });
  it('кожен символ одразу надсилає окремий GET без debounce з tma і вихідним q', async () => {
    window.Telegram = { WebApp: { initData: 'fixture=dictionary', colorScheme: 'light' } };
    const requests: Request[] = [];
    mockFetch(request => { requests.push(request); return json(page); }); mount();
    for (const value of ['u', 'ur', 'urz', 'urze', 'urzęd', 'urzedzie', 'установа']) { query(value); await waitFor(() => expect(requests).toHaveLength(['u', 'ur', 'urz', 'urze', 'urzęd', 'urzedzie', 'установа'].indexOf(value) + 1)); }
    expect(requests.map(request => new URL(request.url).searchParams.get('q'))).toEqual(['u', 'ur', 'urz', 'urze', 'urzęd', 'urzedzie', 'установа']);
    expect(requests.every(request => request.method === 'GET' && request.headers.get('Authorization') === 'tma fixture=dictionary')).toBe(true);
  });
  it('скасовує попередній запит; його пізня відповідь не перезаписує актуальну', async () => {
    const old = deferred(); const latest = deferred(); const requests: Request[] = [];
    mockFetch(request => { requests.push(request); return new URL(request.url).searchParams.get('q') === 'u' ? old.promise : latest.promise; }); mount();
    query('u'); await waitFor(() => expect(requests).toHaveLength(1)); expect(screen.getByRole('status')).toHaveTextContent('Шукаємо слово…');
    query('ur'); await waitFor(() => expect(requests).toHaveLength(2)); expect(requests[0].signal.aborted).toBe(true);
    await act(async () => latest.resolve(json(page))); await screen.findByRole('button', { name: /urząd/ });
    await act(async () => old.resolve(json({ ...page, items: [{ ...item, lemma_pl: 'застаріле слово' }] })));
    expect(screen.getByRole('button', { name: /urząd/ })).toBeVisible(); expect(screen.queryByText('застаріле слово')).not.toBeInTheDocument();
  });
  it('скасовує пошук при очищенні та виході й не показує помилку відміни', async () => {
    const pending = deferred(); const requests: Request[] = [];
    mockFetch(request => { requests.push(request); return pending.promise; }); const view = mount(); query('u'); await waitFor(() => expect(requests).toHaveLength(1)); query(''); expect(requests[0].signal.aborted).toBe(true);
    await act(async () => pending.resolve(json(page))); expect(screen.queryByRole('alert')).not.toBeInTheDocument(); expect(screen.queryByText('urząd')).not.toBeInTheDocument();
    query('ur'); await waitFor(() => expect(requests).toHaveLength(2)); view.unmount(); expect(requests[1].signal.aborted).toBe(true);
  });
  it('пізня помилка скасованого запиту не приховує актуальні результати', async () => {
    const old = deferred(); mockFetch(request => new URL(request.url).searchParams.get('q') === 'u' ? old.promise : json(page)); mount(); query('u');
    await screen.findByText('Шукаємо слово…'); query('ur'); await screen.findByRole('button', { name: /urząd/ });
    await act(async () => old.resolve(json({ code: 'server.unavailable' }, 503))); expect(screen.queryByRole('alert')).not.toBeInTheDocument(); expect(screen.getByRole('button', { name: /urząd/ })).toBeVisible();
  });
  it('повторний текст після швидкого редагування чекає свіжої відповіді замість старої картки', async () => {
    let calls = 0; const old = deferred(); const pending = deferred(); mockFetch(request => new URL(request.url).searchParams.get('q') === 'urz' ? old.promise : ++calls === 1 ? json(page) : pending.promise); mount(); query('ur'); await screen.findByRole('button', { name: /urząd/ });
    query('urz'); query('ur'); expect(screen.queryByRole('button', { name: /urząd/ })).not.toBeInTheDocument(); expect(screen.getByText('Шукаємо слово…')).toBeVisible();
    await act(async () => pending.resolve(json({ ...page, items: [] }))); await screen.findByRole('heading', { name: 'Не знайшли слово' });
  });
  it('залишає серверний порядок та всі леми неоднозначної форми з частиною мови, значенням, збігом і статусом', async () => {
    mockFetch(() => json({ ...page, items: [{ ...item, word_status: 'not_started' }, { ...item, entry_id: secondId, lemma_pl: 'urządzać', part_of_speech: 'verb', short_meaning_uk: 'влаштовувати', word_status: 'recalled' }] })); mount(); query('ur');
    const results = await screen.findByRole('list'); const buttons = within(results).getAllByRole('button');
    expect(buttons[0]).toHaveTextContent('urząd'); expect(buttons[0]).toHaveTextContent('іменник · державна установа'); expect(buttons[0]).toHaveTextContent('Збіг: urzedzie'); expect(buttons[0]).toHaveTextContent('Ще не вчив');
    expect(buttons[1]).toHaveTextContent('urządzać'); expect(buttons[1]).toHaveTextContent('дієслово · влаштовувати'); expect(buttons[1]).toHaveTextContent('Вже пригадую');
  });
  it('відображає фактичні скорочені коди частин мови курсу українською в результатах і картці', async () => {
    const parts = [['adj', 'прикметник'], ['adv', 'прислівник'], ['pron', 'займенник'], ['det', 'означник'], ['prep', 'прийменник'], ['conj', 'сполучник'], ['num', 'числівник'], ['intj', 'вигук']];
    mockFetch(request => json(request.url.includes('/entries/') ? { ...card, lemma_pl: 'dobry', part_of_speech: 'adj', gender: null } : { ...page, items: parts.map(([code], index) => ({ ...item, entry_id: `${firstId}-${index}`, lemma_pl: `dobry ${index}`, part_of_speech: code })) }));
    mount(); query('do'); const list = await screen.findByRole('list'); const buttons = within(list).getAllByRole('button');
    parts.forEach(([, label], index) => expect(buttons[index]).toHaveTextContent(`${label} · державна установа`));
    fireEvent.click(buttons[0]); await screen.findByRole('heading', { name: 'dobry' }); expect(screen.getByText('прикметник', { exact: true })).toBeVisible();
  });
  it('GET картки показує переклади, рід, вимову, приклад і особистий статус без мутацій', async () => {
    const fetcher = mockFetch(request => json(request.url.includes('/entries/') ? card : page)); mount(); query('urzedzie');
    fireEvent.click(await screen.findByRole('button', { name: /urząd/ }));
    await screen.findByRole('heading', { name: 'urząd' }); expect(screen.getByText('іменник · чоловічий рід')).toBeVisible();
    expect(screen.getByText('На повторення')).toBeVisible(); expect(screen.getByText('Основний переклад')).toBeVisible(); expect(screen.getByText('офіс')).toBeVisible(); expect(screen.getByText('Місцевий варіант')).toBeVisible(); expect(screen.getByText('Idę do urzędu.')).toHaveAttribute('lang', 'pl'); expect(screen.getByText('Я йду до державної установи.')).toBeVisible();
    expect(fetcher.mock.calls.map(call => (call[0] as Request).method)).toEqual(['GET', 'GET']); expect((fetcher.mock.calls[1][0] as Request).url).toContain(`/entries/${firstId}`);
    fireEvent.click(screen.getByRole('button', { name: '← До результатів' })); expect(screen.getByRole('searchbox')).toHaveValue('urzedzie'); expect(screen.getByRole('button', { name: /urząd/ })).toBeVisible(); expect(fetcher).toHaveBeenCalledTimes(2);
  });
  it('закрита картка скасовується; її пізній результат не змінює іншу картку', async () => {
    const old = deferred(); const requests: Request[] = [];
    mockFetch(request => { requests.push(request); if (request.url.includes(firstId)) return old.promise; if (request.url.includes(secondId)) return json({ ...card, entry_id: secondId, lemma_pl: 'urządzać' }); return json({ ...page, items: [item, { ...item, entry_id: secondId, lemma_pl: 'urządzać' }] }); });
    mount(); query('ur'); fireEvent.click(await screen.findByRole('button', { name: /^urząd(?!zać)/ })); await waitFor(() => expect(requests).toHaveLength(2)); fireEvent.click(screen.getByRole('button', { name: '← До результатів' })); expect(requests[1].signal.aborted).toBe(true);
    fireEvent.click(screen.getByRole('button', { name: /urządzać/ })); await screen.findByRole('heading', { name: 'urządzać' }); await act(async () => old.resolve(json(card))); expect(screen.getByRole('heading', { name: 'urządzać' })).toBeVisible();
  });
  it('наступна й попередня сторінки передають серверні курсори, новий запит скидає їх', async () => {
    const requests: Request[] = []; mockFetch(request => { requests.push(request); const params = new URL(request.url).searchParams; return json(params.has('after') ? { ...page, items: [{ ...item, entry_id: secondId, lemma_pl: 'urządzać' }], has_prev: true } : { ...page, has_next: true, next_cursor: firstId }); }); mount(); query('ur');
    fireEvent.click(await screen.findByRole('button', { name: 'Наступні ›' })); await screen.findByRole('button', { name: /urządzać/ }); expect(new URL(requests[1].url).searchParams.get('after')).toBe(firstId); expect(screen.getByRole('button', { name: 'Наступні ›' })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: '‹ Попередні' })); await screen.findByRole('button', { name: /^urząd(?!zać)/ }); expect(new URL(requests[2].url).searchParams.get('before')).toBe(secondId);
    query('u'); await waitFor(() => expect(requests).toHaveLength(4)); expect(new URL(requests[3].url).searchParams.has('before')).toBe(false); expect(new URL(requests[3].url).searchParams.has('after')).toBe(false);
  });
});

describe('AC-31 та стани помилок', () => {
  it('порожній результат передає точний вихідний запит у callback майбутньої форми', async () => {
    const report = vi.fn(); const fetcher = mockFetch(() => json({ ...page, items: [] })); mount(<Dictionary onBack={vi.fn()} onReportMissing={report} />); query('  Немає słowa  ');
    await screen.findByRole('heading', { name: 'Не знайшли слово' }); fireEvent.click(screen.getByRole('button', { name: 'Повідомити про відсутній запис' }));
    expect(report).toHaveBeenCalledExactlyOnceWith({ origin: 'missing_word', search_query: '  Немає słowa  ' }); expect(fetcher).toHaveBeenCalledOnce();
  });
  it('порожній результат відкриває форму без надсилання та зберігає запит після повернення', async () => {
    const fetcher = mockFetch(() => json({ ...page, items: [] })); mount(); query('немає'); await screen.findByRole('heading', { name: 'Не знайшли слово' }); fireEvent.click(screen.getByRole('button', { name: 'Повідомити про відсутній запис' }));
    await screen.findByRole('dialog', { name: 'Повідомити про відсутній запис' }); expect(fetcher).toHaveBeenCalledOnce();
    fireEvent.click(screen.getByRole('button', { name: 'Назад до словника' })); expect(screen.getByRole('searchbox')).toHaveValue('немає'); expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
  it.each(['network', '503'])('помилка %s зберігає запит і повторюється лише за дією', async mode => {
    let attempts = 0; const fetcher = mockFetch(() => { if (++attempts > 1) return json(page); if (mode === 'network') throw new TypeError('Немає мережі'); return json({ code: 'server.unavailable' }, 503); }); mount(); query('ur');
    await screen.findByRole('alert'); expect(screen.getByRole('searchbox')).toHaveValue('ur'); expect(screen.queryByRole('heading', { name: 'Не знайшли слово' })).not.toBeInTheDocument(); expect(fetcher).toHaveBeenCalledOnce();
    fireEvent.click(screen.getByRole('button', { name: 'Спробувати ще раз' })); await screen.findByRole('button', { name: /urząd/ }); expect(fetcher).toHaveBeenCalledTimes(2);
  });
  it('401 не має автоматичного повтору й просить відкрити Telegram', async () => {
    const fetcher = mockFetch(() => json({ code: 'auth.invalid_init_data' }, 401)); mount(); query('ur'); await screen.findByText('Відкрийте тренер знову в Telegram, щоб продовжити.'); expect(screen.queryByRole('button', { name: 'Спробувати ще раз' })).not.toBeInTheDocument(); expect(fetcher).toHaveBeenCalledOnce();
  });
  it('404 картки приховує старий запис і дозволяє оновити пошук після зняття публікації', async () => {
    let searches = 0; mockFetch(request => request.url.includes('/entries/') ? json({ code: 'dictionary.entry_not_found' }, 404) : json(++searches > 1 ? { ...page, items: [] } : page)); mount(); query('ur'); fireEvent.click(await screen.findByRole('button', { name: /urząd/ }));
    await screen.findByText('Цей запис більше недоступний у словнику. Поверніться до результатів і оновіть пошук.'); expect(screen.queryByText('Idę do urzędu.')).not.toBeInTheDocument(); fireEvent.click(screen.getByRole('button', { name: 'Оновити пошук' })); await screen.findByRole('heading', { name: 'Не знайшли слово' });
  });
  it('ручна тема зберігає запит і картку без нових API-запитів', async () => {
    const fetcher = mockFetch(request => json(request.url.includes('/entries/') ? card : page)); mount(); query('ur'); fireEvent.click(await screen.findByRole('button', { name: /urząd/ })); await screen.findByRole('heading', { name: 'urząd' });
    act(() => themeStore.setTheme('dark')); expect(screen.getByRole('heading', { name: 'urząd' })).toBeVisible(); expect(document.documentElement).toHaveAttribute('data-theme', 'dark'); expect(fetcher).toHaveBeenCalledTimes(2); fireEvent.click(screen.getByRole('button', { name: '← До результатів' })); expect(screen.getByRole('searchbox')).toHaveValue('ur');
  });
});

describe('Маршрут словника', () => {
  it('прямий /dictionary зберігається після перевірки профілю, вихід веде до today', async () => {
    window.history.replaceState(null, '', '/dictionary'); const fetcher = mockFetch(() => json(learner)); mount(<App />);
    await screen.findByRole('heading', { name: 'Словник' }); expect(window.location.pathname).toBe('/dictionary'); expect(fetcher).toHaveBeenCalledOnce();
    fireEvent.click(screen.getByRole('button', { name: '← До «Сьогодні»' })); await screen.findByRole('heading', { name: 'Сьогодні' }); expect(window.location.pathname).toBe('/today');
    fireEvent.click(screen.getByRole('button', { name: 'Словник' })); await screen.findByRole('heading', { name: 'Словник' }); expect(window.location.pathname).toBe('/dictionary');
  });
  it.each([401, 404])('профіль %i не відкриває захищений словник', async status => {
    window.history.replaceState(null, '', '/dictionary'); const fetcher = mockFetch(() => json({ code: status === 401 ? 'auth.invalid_init_data' : 'learner.not_onboarded' }, status)); mount(<App />);
    await screen.findByText(status === 401 ? 'Відкрийте тренер знову в Telegram, щоб продовжити.' : 'Спершу налаштуйте тренер.'); expect(screen.queryByRole('searchbox')).not.toBeInTheDocument(); expect(fetcher).toHaveBeenCalledOnce(); expect(window.location.pathname).toBe('/dictionary');
  });
});
