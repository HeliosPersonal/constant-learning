import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { QueryClientProvider } from '@tanstack/react-query';
import type { components } from '../src/api/schema';
import { createQueryClient } from '../src/api/query';
import { ThemeProvider } from '../src/theme/ThemeProvider';
import { themeStore } from '../src/theme/store';
import Dictionary from '../src/features/dictionary/search/Dictionary';
import ContentReportDialog from '../src/features/dictionary/report/ContentReportDialog';
import { reportBody, textLength, validateReport, type ReportContext, type ReportDraft } from '../src/features/dictionary/report/model';

const entry: components['schemas']['DictionaryEntryCard'] = { entry_id: '0192f0a0-0000-7000-8000-0000000000e1', lemma_pl: 'urząd', part_of_speech: 'noun', gender: 'm', translations_uk: [{ kind: 'canonical', text_uk: 'державна установа' }], pronunciation_uk: 'ужонд', example_pl: 'Idę do urzędu.', example_uk: 'Я йду до державної установи.', word_status: 'review' };
const context: ReportContext = { origin: 'card', entry };
const accepted: components['schemas']['ContentReportAccepted'] = { id: '0192f0a0-0000-7000-8000-0000000000d2', status: 'pending_verification', message: 'Дякуємо, передали на перевірку' };
const draft: ReportDraft = { field: 'translation', proposed: '', comment: '' };
const page: components['schemas']['DictionarySearchPage'] = { items: [{ entry_id: entry.entry_id, lemma_pl: entry.lemma_pl, part_of_speech: 'noun', short_meaning_uk: 'державна установа', matched_form: 'urzedzie', word_status: 'review' }], has_next: false, has_prev: false, next_cursor: null };
const clients: ReturnType<typeof createQueryClient>[] = [];
function json(body: unknown, status = 200) { return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }); }
function mockFetch(handler: (request: Request) => Response | Promise<Response>) {
  const fetcher = vi.fn<typeof fetch>().mockImplementation(request => Promise.resolve(handler(request as Request)));
  vi.stubGlobal('fetch', fetcher); return fetcher;
}
function mount(element: React.ReactNode) {
  const client = createQueryClient(); clients.push(client);
  return render(<QueryClientProvider client={client}><ThemeProvider>{element}</ThemeProvider></QueryClientProvider>);
}
function form(reportContext = context) { const close = vi.fn(); mount(<ContentReportDialog open context={reportContext} onClose={close} />); return close; }
function submit() { fireEvent.submit(screen.getByRole('form', { name: 'Повідомлення про словник' })); }
function selectField(value = 'translation') { fireEvent.change(screen.getByLabelText('Що потрібно перевірити?'), { target: { value } }); }
function proposed(value: string) { fireEvent.change(screen.getByLabelText(/Запропоноване значення/), { target: { value } }); }
function comment(value: string) { fireEvent.change(screen.getByLabelText(/Коментар \(необов’язково\)/), { target: { value } }); }

afterEach(() => { clients.forEach(client => client.clear()); clients.length = 0; vi.unstubAllGlobals(); vi.restoreAllMocks(); delete window.Telegram; });

describe('AC-34 і AC-36: справжнє надсилання', () => {
  it.each(['lemma', 'translation', 'pronunciation', 'example'])('поле %s надсилає типізований POST, успіх з’являється лише після 202', async field => {
    let body: components['schemas']['ContentReportCreate'] | undefined;
    let resolve!: (response: Response) => void;
    window.Telegram = { WebApp: { initData: 'fixture=report', colorScheme: 'light' } };
    const fetcher = mockFetch(async request => { body = await request.json() as components['schemas']['ContentReportCreate']; return new Promise<Response>(complete => { resolve = complete; }); }); form(); selectField(field); proposed('  державна установа  '); comment('Перевірте, будь ласка.'); submit();
    await waitFor(() => expect(body).toBeDefined()); expect(screen.queryByText('Дякуємо, передали на перевірку')).not.toBeInTheDocument(); expect(screen.getByRole('button', { name: 'Надсилаємо повідомлення…' })).toBeDisabled();
    expect(body).toEqual({ origin: 'card', entry_id: entry.entry_id, field, proposed_value: '  державна установа  ', comment: 'Перевірте, будь ласка.', search_query: null });
    const request = fetcher.mock.calls[0][0] as Request; expect(request.method).toBe('POST'); expect(request.url).toContain('/api/v1/content-reports'); expect(request.headers.get('Authorization')).toBe('tma fixture=report'); expect(request.headers.has('Idempotency-Key')).toBe(false);
    await act(async () => resolve(json({ ...accepted, status: field === 'lemma' || field === 'example' ? 'in_editor_queue' : 'pending_verification' }, 202)));
    expect(screen.getByRole('status')).toHaveTextContent('Дякуємо, передали на перевірку'); expect(screen.queryByRole('form')).not.toBeInTheDocument();
  });
  it('поле обов’язкове для картки, запропоноване значення й коментар необов’язкові', async () => {
    let body: unknown; const fetcher = mockFetch(async request => { body = await request.json(); return json({ ...accepted, status: 'in_editor_queue' }, 202); }); form(); submit();
    expect(screen.getByText('Виберіть поле, якого стосується повідомлення.', { selector: '.result' })).toBeVisible(); expect(fetcher).not.toHaveBeenCalled();
    selectField(); submit(); await screen.findByText('Дякуємо, передали на перевірку'); expect(body).toEqual({ origin: 'card', entry_id: entry.entry_id, field: 'translation', proposed_value: null, comment: null, search_query: null });
  });
  it('відсутнє слово передає точний вихідний запит без entry_id і поля', async () => {
    let body: unknown; mockFetch(async request => { body = await request.json(); return json({ ...accepted, status: 'in_editor_queue' }, 202); }); form({ origin: 'missing_word', search_query: '  НОВЕ słowo  ' });
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument(); comment('Додайте, будь ласка.'); submit(); await screen.findByText('Дякуємо, передали на перевірку');
    expect(body).toEqual({ origin: 'missing_word', entry_id: null, field: null, proposed_value: null, comment: 'Додайте, будь ласка.', search_query: '  НОВЕ słowo  ' });
  });
  it('коментар із HTML відображається й надсилається лише як звичайний текст', async () => {
    const text = '<img src=x onerror="window.reportXss=1"> **слово**'; let body: unknown;
    mockFetch(async request => { body = await request.json(); return json(accepted, 202); }); form(); selectField(); comment(text);
    expect(screen.getByRole('textbox', { name: /Коментар/ })).toHaveValue(text); expect(document.querySelector('img')).toBeNull(); submit(); await screen.findByText('Дякуємо, передали на перевірку'); expect(body).toMatchObject({ comment: text });
  });
  it.each([[200, accepted], [202, { status: 'pending_verification' }], [202, { ...accepted, status: 'approved' }]])('відповідь %i з неочікуваним контрактом не імітує успішне прийняття', async (status, body) => {
    mockFetch(() => json(body, status as number)); form(); selectField(); submit(); await screen.findByRole('alert'); expect(screen.queryByText('Дякуємо, передали на перевірку')).not.toBeInTheDocument();
  });
});

describe('AC-46: помилки та збереження вводу', () => {
  it('429 пояснює 20 повідомлень і наступну північ, зберігаючи всі поля без повтору', async () => {
    const fetcher = mockFetch(() => json({ code: 'report.daily_limit_reached' }, 429)); form(); selectField('pronunciation'); proposed('ужонд'); comment('Збережіть цей текст.'); submit();
    await screen.findByText(/Сьогоднішній ліміт 20 повідомлень вичерпано/); expect(screen.getByRole('alert')).toHaveTextContent('після 00:00 за вашим часом'); expect(screen.getByRole('combobox')).toHaveValue('pronunciation'); expect(screen.getByRole('textbox', { name: /Запропоноване/ })).toHaveValue('ужонд'); expect(screen.getByRole('textbox', { name: /Коментар/ })).toHaveValue('Збережіть цей текст.'); expect(fetcher).toHaveBeenCalledOnce();
  });
  it.each(['network', '503', '400'])('помилка %s не скидає ввід; свідомий повтор чекає серверного 202', async mode => {
    let attempts = 0; const fetcher = mockFetch(() => { if (++attempts > 1) return json(accepted, 202); if (mode === 'network') throw new TypeError('Мережа недоступна'); return json({ code: 'report.invalid_request' }, Number(mode)); }); form(); selectField('example'); proposed('To jest urząd.'); comment('Перевірити приклад'); submit();
    await screen.findByRole('alert'); expect(screen.getByRole('textbox', { name: /Запропоноване/ })).toHaveValue('To jest urząd.'); expect(screen.getByRole('textbox', { name: /Коментар/ })).toHaveValue('Перевірити приклад'); expect(screen.getByRole('combobox')).toHaveValue('example'); expect(fetcher).toHaveBeenCalledOnce(); submit(); await screen.findByText('Дякуємо, передали на перевірку'); expect(fetcher).toHaveBeenCalledTimes(2);
  });
  it.each([401, 404])('%i зберігає поля й блокує подальші POST до повернення', async status => {
    const fetcher = mockFetch(() => json({ code: status === 401 ? 'auth.invalid_init_data' : 'dictionary.entry_not_found' }, status)); form(); selectField(); proposed('переклад'); submit(); await screen.findByRole('alert');
    expect(screen.getByRole('button', { name: 'Надіслати на перевірку' })).toBeDisabled(); expect(screen.getByRole('textbox', { name: /Запропоноване/ })).toHaveValue('переклад'); submit(); expect(fetcher).toHaveBeenCalledOnce(); expect(screen.getByRole('button', { name: 'Назад до словника' })).toBeEnabled();
  });
});

describe('AC-51: одноразове надсилання й повторне прийняття', () => {
  it('подвійний submit надсилає лише один POST, pending блокує поля, Назад та Escape', async () => {
    let resolve!: (response: Response) => void; const fetcher = mockFetch(() => new Promise<Response>(complete => { resolve = complete; })); const close = form(); selectField(); act(() => { submit(); submit(); }); await waitFor(() => expect(fetcher).toHaveBeenCalledOnce());
    expect(screen.getByRole('combobox')).toBeDisabled(); expect(screen.getByRole('textbox', { name: /Коментар/ })).toBeDisabled(); expect(screen.getByRole('button', { name: 'Назад до словника' })).toBeDisabled(); fireEvent(screen.getByRole('dialog'), new Event('cancel', { cancelable: true, bubbles: true })); expect(close).not.toHaveBeenCalled();
    await act(async () => resolve(json(accepted, 202))); fireEvent.click(screen.getByRole('button', { name: 'Повернутися до словника' })); expect(close).toHaveBeenCalledOnce(); expect(fetcher).toHaveBeenCalledOnce();
  });
  it('після помилки мережі повтор із тим самим тілом приймає серверний наявний report без локального ID', async () => {
    const bodies: unknown[] = []; let attempts = 0;
    mockFetch(async request => { bodies.push(await request.json()); if (++attempts === 1) throw new TypeError('Відповідь втрачено'); return json(accepted, 202); }); form(); selectField(); proposed('переклад'); comment('коментар'); submit(); await screen.findByRole('alert'); submit(); await screen.findByText('Дякуємо, передали на перевірку'); expect(bodies).toHaveLength(2); expect(bodies[1]).toEqual(bodies[0]); expect(bodies[1]).not.toHaveProperty('id');
  });
});

describe('Межі Unicode та звичайний текст', () => {
  it('рахує Unicode code points, приймаючи 500/1000/200 emoji та відхиляючи наступний', () => {
    expect(textLength('😀')).toBe(1);
    expect(validateReport({ origin: 'missing_word', search_query: '😀'.repeat(200) }, { ...draft, proposed: '😀'.repeat(500), comment: '😀'.repeat(1000) })).toEqual({});
    const errors = validateReport({ origin: 'missing_word', search_query: '😀'.repeat(201) }, { ...draft, proposed: '😀'.repeat(501), comment: '😀'.repeat(1001) }); expect(Object.keys(errors)).toEqual(['proposed', 'comment', 'query']);
  });
  it('форма блокує завеликі поля без UTF-16 обрізання та дозволяє виправити їх', async () => {
    const fetcher = mockFetch(() => json(accepted, 202)); form(); selectField(); proposed('😀'.repeat(501)); comment('😀'.repeat(1001)); submit(); expect(fetcher).not.toHaveBeenCalled(); expect(screen.getByRole('textbox', { name: /Запропоноване/ })).toHaveAttribute('aria-invalid', 'true'); expect(screen.getByText(/501 \/ 500 символів/)).toBeVisible();
    proposed('😀'.repeat(500)); comment('😀'.repeat(1000)); submit(); await screen.findByText('Дякуємо, передали на перевірку'); expect(fetcher).toHaveBeenCalledOnce();
  });
  it('відхиляє нульові символи та порожні запити за правилами сервера', () => {
    expect(validateReport({ origin: 'missing_word', search_query: '\u0085\t ' }, draft)).toHaveProperty('query'); expect(validateReport({ origin: 'missing_word', search_query: 'слово\0' }, draft)).toHaveProperty('query'); expect(validateReport(context, { ...draft, proposed: '\0', comment: '\0' })).toHaveProperty('proposed'); expect(validateReport(context, { ...draft, comment: '\0' })).toHaveProperty('comment');
    expect(reportBody(context, { ...draft, proposed: '\u0085\t ' }).proposed_value).toBeNull(); expect(reportBody(context, { ...draft, proposed: '\uFEFF' }).proposed_value).toBe('\uFEFF');
  });
});

describe('Інтеграція з карткою та темою', () => {
  it('повернення з форми зберігає запит, картку й чернетку; надсилання не мутує картку чи кеш заняття', async () => {
    const posts: unknown[] = []; mockFetch(async request => { if (request.method === 'POST') { posts.push(await request.json()); return json(accepted, 202); } return json(request.url.includes('/entries/') ? entry : page); });
    mount(<Dictionary onBack={vi.fn()} />); clients[0].setQueryData(['session', 'active'], { answer: 'відповідь', item: 'картка' }); fireEvent.change(screen.getByRole('searchbox'), { target: { value: 'urzedzie' } }); fireEvent.click(await screen.findByRole('button', { name: /urząd/ })); await screen.findByRole('heading', { name: 'urząd' }); fireEvent.click(screen.getByRole('button', { name: 'Повідомити про неточність' })); const dialog = await screen.findByRole('dialog');
    selectField(); proposed('варіант'); comment('чернетка'); fireEvent.click(within(dialog).getByRole('button', { name: 'Назад до словника' })); expect(screen.getByRole('searchbox')).toHaveValue('urzedzie'); expect(screen.getByRole('heading', { name: 'urząd' })).toBeVisible(); expect(screen.getByText('державна установа')).toBeVisible();
    fireEvent.click(screen.getByRole('button', { name: 'Повідомити про неточність' })); expect(within(screen.getByRole('dialog')).getByRole('textbox', { name: /Коментар/ })).toHaveValue('чернетка'); act(() => themeStore.setTheme('dark')); expect(screen.getByRole('textbox', { name: /Запропоноване/ })).toHaveValue('варіант'); expect(posts).toEqual([]);
    submit(); await screen.findByText('Дякуємо, передали на перевірку'); expect(posts).toHaveLength(1); expect(clients[0].getQueryData(['session', 'active'])).toEqual({ answer: 'відповідь', item: 'картка' }); fireEvent.click(screen.getByRole('button', { name: 'Повернутися до словника' })); expect(screen.getByText('державна установа')).toBeVisible(); expect(screen.getByRole('searchbox')).toHaveValue('urzedzie');
  });
});
