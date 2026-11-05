import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { QueryClientProvider } from '@tanstack/react-query';
import { App } from '../src/App';
import { createQueryClient } from '../src/api/query';
import { ThemeProvider } from '../src/theme/ThemeProvider';
import { themeStore } from '../src/theme/store';
import Editor from '../src/features/editor/Editor';
import { editorQueryKey, type QueueItem } from '../src/features/editor/api';
import { initialDraft, validateDecision } from '../src/features/editor/model';
import { answerChange, appliedPage, change, closedReport, disputeReport, missingReport, queuePage, report, rollbackResult } from './fixtures/editor';
import { todaySummary } from './fixtures/today';

const queuePath = '/api/v1/editor/content-reports';
const appliedPath = '/api/v1/editor/applied-changes';
const decisionPath = `${queuePath}/${report.id}/decision`;
const rollbackPath = `${appliedPath}/${change.id}/rollback`;
const languageCatalog = [
  { code: 'pl', display_name_uk: 'Польська', is_enabled: true, sort_order: 0, available: true },
  { code: 'en', display_name_uk: 'Англійська', is_enabled: true, sort_order: 1, available: true },
];
const clients: ReturnType<typeof createQueryClient>[] = [];
function json(body: unknown, status = 200) { return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }); }
// Черга редактора підвантажує каталог мов для фільтра (AC-11) — відповідаємо на нього окремо,
// щоб тестові обробники й підрахунки стосувалися лише черги/застосованих/рішень.
function network(handler?: (request: Request) => Response | Promise<Response>) {
  const fetcher = vi.fn<typeof fetch>().mockImplementation(async input => {
    const request = input as Request;
    if (new URL(request.url).pathname === '/api/v1/languages') return json({ items: languageCatalog });
    return handler ? handler(request) : json(new URL(request.url).pathname === queuePath ? queuePage : appliedPage);
  }); vi.stubGlobal('fetch', fetcher); return fetcher;
}
function mount(element = <Editor onBack={vi.fn()} />) {
  const client = createQueryClient(); clients.push(client);
  return { ...render(<QueryClientProvider client={client}><ThemeProvider>{element}</ThemeProvider></QueryClientProvider>), client };
}
const requests = (fetcher: ReturnType<typeof network>, path: string) => fetcher.mock.calls.map(call => call[0] as Request).filter(request => new URL(request.url).pathname === path);
const firstRow = async () => within(await screen.findByRole('list')).getAllByRole('button')[0];
async function openReport() { fireEvent.click(await firstRow()); return screen.getByRole('region', { name: /Розглянути/ }); }
function submit() { fireEvent.submit(screen.getByRole('form', { name: 'Рішення щодо повідомлення' })); }
async function openApplied() { await screen.findByRole('tab', { name: 'Застосовані' }); fireEvent.click(screen.getByRole('tab', { name: 'Застосовані' })); await screen.findAllByRole('region', { name: 'Зміна: urząd' }); }
async function openRollback() { await openApplied(); fireEvent.click(screen.getByRole('button', { name: 'Відкотити' })); return screen.getByRole('region', { name: 'Відкотити зміну?' }); }
afterEach(() => { clients.forEach(client => client.clear()); clients.length = 0; vi.unstubAllGlobals(); vi.restoreAllMocks(); window.history.replaceState(null, '', '/'); delete window.Telegram; });

describe('Серверний доступ і читання', () => {
  it('прямий /editor використовує захищений GET без профілю learner й лише псевдонім автора', async () => {
    window.Telegram = { WebApp: { initData: 'signed=editor-fixture' } }; window.history.replaceState(null, '', '/editor');
    const fetcher = network(() => json({ ...queuePage, items: [{ ...report, first_name: 'Не показувати ім’я', telegram_id: 123456 }] })); mount(<App />);
    await screen.findByText('R-7F3A9C'); expect(screen.queryByText('Не показувати ім’я')).not.toBeInTheDocument(); expect(screen.queryByText('123456')).not.toBeInTheDocument();
    expect(requests(fetcher, '/api/v1/learners/me')).toHaveLength(0); expect(requests(fetcher, queuePath)).toHaveLength(1); expect(requests(fetcher, queuePath)[0].headers.get('Authorization')).toBe('tma signed=editor-fixture');
  });
  it('Сьогодні не перевіряє редакторську роль і не завантажує редакторські дані', async () => {
    window.history.replaceState(null, '', '/today'); const fetcher = network(request => request.url.endsWith('/today') ? json(todaySummary) : request.url.endsWith('/client-timings') ? new Response(null, { status: 204 }) : json({ id: report.id, time_zone: 'Europe/Warsaw', time_zone_confirmed: true, daily_new_words_limit: 5, reminder: null, onboarded_at: '2026-11-01T09:00:00Z' })); mount(<App />);
    await screen.findByLabelText('Ваш навчальний прогрес'); expect(requests(fetcher, queuePath)).toHaveLength(0); expect(requests(fetcher, appliedPath)).toHaveLength(0);
  });
  it.each([401, 403])('%i не показує списків, чужих повідомлень, вкладок чи дій і не повторює запити', async status => {
    const fetcher = network(() => json({ code: status === 403 ? 'editor.role_required' : 'auth.invalid_init_data' }, status)); mount();
    await screen.findByText(status === 403 ? 'Цей розділ доступний лише редакторам словника.' : 'Відкрийте тренер знову в Telegram, щоб продовжити.');
    expect(screen.queryByText(report.reporter_alias)).not.toBeInTheDocument(); expect(screen.queryByRole('list')).not.toBeInTheDocument(); expect(screen.queryByRole('tab', { name: 'Застосовані' })).not.toBeInTheDocument(); expect(screen.queryByRole('region', { name: /Розглянути|Відкотити зміну\?/ })).not.toBeInTheDocument(); expect(fetcher).toHaveBeenCalledOnce();
  });
  it('очікування не показує даних, тимчасова помилка має явний повтор', async () => {
    let release!: (value: Response) => void; let reads = 0;
    const fetcher = network(() => ++reads === 1 ? new Promise<Response>(resolve => { release = resolve; }) : json(queuePage)); mount();
    expect(screen.getByText('Завантажуємо чергу…')).toBeVisible(); expect(screen.queryByText(report.reporter_alias)).not.toBeInTheDocument();
    await waitFor(() => expect(fetcher).toHaveBeenCalledOnce());
    await act(async () => { release(json({ code: 'server.unavailable' }, 503)); }); await screen.findByText('Не вдалося завантажити список. Спробуйте ще раз.'); expect(fetcher).toHaveBeenCalledOnce();
    fireEvent.click(screen.getByRole('button', { name: 'Спробувати ще раз' })); await screen.findByText(report.reporter_alias); expect(requests(fetcher, queuePath)).toHaveLength(2);
  });
  it.each(['queue', 'applied'] as const)('%s передає after/before окремо й зберігає серверний порядок', async view => {
    const first = view === 'queue' ? report : change; const second = { ...first, id: '0192f0a0-0000-7000-8000-0000000000f9' };
    const path = view === 'queue' ? queuePath : appliedPath;
    const fetcher = network(request => {
      const url = new URL(request.url);
      if (url.pathname !== path) return json(queuePage);
      const later = url.searchParams.has('after'); return json({ items: [later ? second : first], has_next: !later, has_prev: later, next_cursor: later ? null : first.id });
    }); mount(); if (view === 'applied') await openApplied(); else await screen.findByText(report.reporter_alias);
    fireEvent.click(screen.getByRole('button', { name: 'Наступна ›' })); await waitFor(() => expect(screen.getByRole('button', { name: /Попередня/ })).toBeEnabled());
    const forward = new URL(requests(fetcher, path)[1].url); expect(forward.searchParams.get('after')).toBe(first.id); expect(forward.searchParams.has('before')).toBe(false);
    fireEvent.click(screen.getByRole('button', { name: /Попередня/ })); await waitFor(() => expect(requests(fetcher, path)).toHaveLength(3));
    const backward = new URL(requests(fetcher, path)[2].url); expect(backward.searchParams.get('before')).toBe(second.id); expect(backward.searchParams.has('after')).toBe(false); expect(backward.searchParams.get('limit')).toBe('20');
  });
  it('AC-11: фільтр за мовою перемикає набір черги, передає target_language і скидає курсор', async () => {
    const enReport = { ...report, id: '0192f0a0-0000-7000-8000-0000000000e5', target_language: 'en', lemma: 'office', reporter_alias: 'R-ENONLY' };
    const fetcher = network(request => {
      const url = new URL(request.url);
      if (url.pathname !== queuePath) return json(appliedPage);
      return json(url.searchParams.get('target_language') === 'en' ? { ...queuePage, items: [enReport] } : queuePage);
    });
    mount(); await screen.findByText(report.reporter_alias);
    fireEvent.change(await screen.findByLabelText('Мова черги'), { target: { value: 'en' } });
    await screen.findByText('R-ENONLY'); expect(screen.queryByText(report.reporter_alias)).not.toBeInTheDocument();
    const filtered = new URL(requests(fetcher, queuePath).at(-1)!.url);
    expect(filtered.searchParams.get('target_language')).toBe('en'); expect(filtered.searchParams.has('after')).toBe(false); expect(filtered.searchParams.has('before')).toBe(false);
  });
  it('403 іншого списку прибирає раніше доступні дані й кеш редактора', async () => {
    const fetcher = network(request => json(request.url.includes('/applied-changes') ? { code: 'editor.role_required' } : queuePage, request.url.includes('/applied-changes') ? 403 : 200)); const { client } = mount();
    await screen.findByText(report.reporter_alias); fireEvent.click(screen.getByRole('tab', { name: 'Застосовані' })); await screen.findByText('Цей розділ доступний лише редакторам словника.');
    expect(screen.queryByText(report.reporter_alias)).not.toBeInTheDocument(); expect(screen.queryByRole('tab', { name: 'Черга' })).not.toBeInTheDocument(); await waitFor(() => expect(client.getQueriesData({ queryKey: editorQueryKey }).every(([, data]) => data === undefined)).toBe(true)); expect(requests(fetcher, appliedPath)).toHaveLength(1);
  });
});

describe('Рішення редактора', () => {
  it('коментарі — звичайний текст, а закриття повертає фокус і зберігає чернетку', async () => {
    network(() => json({ ...queuePage, items: [{ ...report, comment: '<img src=x onerror=alert(1)>' }] })); mount();
    const trigger = await firstRow(); trigger.focus(); fireEvent.click(trigger);
    expect(screen.getByText('<img src=x onerror=alert(1)>')).toBeVisible(); expect(document.querySelector('.editor-screen img')).toBeNull();
    fireEvent.change(screen.getByLabelText(/Остаточне значення/), { target: { value: 'державний орган' } });
    fireEvent.click(screen.getByRole('button', { name: '← До черги' })); expect(trigger).toHaveFocus(); fireEvent.click(trigger); expect(screen.getByLabelText(/Остаточне значення/)).toHaveValue('державний орган');
    act(() => themeStore.setTheme('dark')); expect(screen.getByLabelText(/Остаточне значення/)).toHaveValue('державний орган');
  });
  it('AC-38: схвалення без виправлення не надсилає corrected_value й оновлює чергу тільки після відповіді', async () => {
    let accepted = false; const fetcher = network(request => request.method === 'POST' ? (accepted = true, json(closedReport(report.id))) : json({ ...queuePage, items: accepted ? [] : [report] })); mount(); await openReport(); submit();
    await screen.findByText('Зміну застосовано. Вона доступна в списку застосованих змін.'); expect(await requests(fetcher, decisionPath)[0].clone().json()).toEqual({ decision: 'approve' }); await screen.findByText('Усе розглянуто'); expect(screen.queryByRole('region', { name: /Розглянути|Відкотити зміну\?/ })).not.toBeInTheDocument();
  });
  it.each([report, missingReport])('AC-39/40: правильні причини для $origin, обов’язкова причина й точний reject payload', async item => {
    const fetcher = network(request => request.method === 'POST' ? json(closedReport(item.id, 'rejected', item.origin === 'missing_word' ? 'phrase_not_lemma' : 'duplicate')) : json({ ...queuePage, items: [item] })); mount(); await openReport();
    fireEvent.click(screen.getByRole('radio', { name: 'Відхилити' })); submit(); await screen.findByText('Виберіть причину відхилення.'); expect(fetcher.mock.calls.filter(call => (call[0] as Request).method === 'POST')).toHaveLength(0);
    const options = within(screen.getByLabelText('Причина відхилення')).getAllByRole('option').map(option => option.textContent);
    expect(options).toEqual(item.origin === 'missing_word' ? ['Оберіть причину', 'Уже є в словнику', 'Фраза, а не лема', 'Поза межами курсу'] : ['Оберіть причину', 'Джерело не підтверджує', 'Запис уже правильний', 'Поза межами словника', 'Дублікат']);
    const reason = item.origin === 'missing_word' ? 'phrase_not_lemma' : 'duplicate'; fireEvent.change(screen.getByLabelText('Причина відхилення'), { target: { value: reason } }); submit(); await screen.findByText('Повідомлення відхилено з обраною причиною.');
    expect(await requests(fetcher, `${queuePath}/${item.id}/decision`)[0].clone().json()).toEqual({ decision: 'reject', rejection_reason: reason });
  });
  it('AC-40: відсутня лема потребує польського кандидата й не може одразу публікуватися', async () => {
    const fetcher = network(request => request.method === 'POST' ? json(closedReport(missingReport.id, 'added_to_candidates')) : json({ ...queuePage, items: [missingReport] })); mount(); await openReport();
    expect(screen.queryByRole('radio', { name: 'Схвалити' })).not.toBeInTheDocument(); submit(); await screen.findByText('Вкажіть одну польську лему малими літерами, без пробілів.');
    fireEvent.change(screen.getByLabelText(/Польська лема/), { target: { value: 'zaświadczenie' } }); submit(); await screen.findByText(/Лему додано до кандидатів/);
    expect(await requests(fetcher, `${queuePath}/${missingReport.id}/decision`)[0].clone().json()).toEqual({ decision: 'add_to_candidates', corrected_value: 'zaświadczenie' });
  });
  it('answer_dispute з field:null має змістовний підпис і не обіцяє перерахунку історії', async () => {
    network(() => json({ ...queuePage, items: [disputeReport] })); mount(); const dialog = await openReport();
    expect(within(dialog).getByText(/Прийнятий варіант відповіді/)).toBeVisible(); expect(within(dialog).getByText('Окремий варіант ще не додано')).toBeVisible(); expect(within(dialog).getByText(/Попередні відповіді не перераховуються/)).toBeVisible();
  });
  it('pending блокує подвійне надсилання, поля, закриття й Escape без оптимістичного успіху', async () => {
    let release!: (value: Response) => void;
    const fetcher = network(request => request.method === 'POST' ? new Promise<Response>(resolve => { release = resolve; }) : json(queuePage)); mount(); const dialog = await openReport();
    act(() => { submit(); submit(); }); await waitFor(() => expect(requests(fetcher, decisionPath)).toHaveLength(1));
    expect(screen.getByRole('button', { name: 'Зберігаємо рішення…' })).toBeDisabled(); expect(screen.getByRole('radio', { name: 'Схвалити' })).toBeDisabled(); expect(screen.getByRole('button', { name: '← До черги' })).toBeDisabled();
    fireEvent(dialog, new Event('cancel', { cancelable: true, bubbles: false })); expect(dialog).toBeInTheDocument(); expect(screen.queryByText(/Зміну застосовано/)).not.toBeInTheDocument();
    await act(async () => { release(json(closedReport(report.id))); }); await screen.findByText(/Зміну застосовано/);
  });
  it.each([400, 503])('%i зберігає виправлення й причину для явного повтору', async status => {
    let posts = 0; const fetcher = network(request => request.method === 'POST' ? ++posts === 1 ? json({ code: status === 400 ? 'editor.invalid_value' : 'server.unavailable' }, status) : json(closedReport(report.id, 'rejected', 'duplicate')) : json(queuePage)); mount(); await openReport();
    fireEvent.change(screen.getByLabelText(/Остаточне значення/), { target: { value: 'державний орган' } }); fireEvent.click(screen.getByRole('radio', { name: 'Відхилити' })); fireEvent.change(screen.getByLabelText('Причина відхилення'), { target: { value: 'duplicate' } }); submit();
    await screen.findByText(status === 400 ? 'Остаточне значення не відповідає полю словника. Перевірте введення.' : 'Не вдалося зберегти рішення. Ваш вибір залишився на екрані — спробуйте ще раз.');
    expect(screen.getByLabelText('Причина відхилення')).toHaveValue('duplicate'); expect(requests(fetcher, decisionPath)).toHaveLength(1);
    fireEvent.click(screen.getByRole('radio', { name: 'Схвалити' })); expect(screen.getByLabelText(/Остаточне значення/)).toHaveValue('державний орган'); fireEvent.click(screen.getByRole('radio', { name: 'Відхилити' })); submit(); await screen.findByText('Повідомлення відхилено з обраною причиною.'); expect(requests(fetcher, decisionPath)).toHaveLength(2);
  });
  it('409 перечитує чергу, зберігає форму й не показує вигаданого успіху', async () => {
    let posts = 0; const fetcher = network(request => request.method === 'POST' ? (posts++, json({ code: 'report.already_closed' }, 409)) : json({ ...queuePage, items: posts ? [] : [report] })); mount(); await openReport(); fireEvent.change(screen.getByLabelText(/Остаточне значення/), { target: { value: 'орган влади' } }); submit();
    await screen.findByText('Це повідомлення вже розглянуто. Чергу оновлено.'); expect(screen.getByLabelText(/Остаточне значення/)).toHaveValue('орган влади'); expect(screen.getByRole('button', { name: 'Схвалити зміну' })).toBeDisabled(); expect(screen.queryByText(/Зміну застосовано/)).not.toBeInTheDocument(); expect(requests(fetcher, queuePath).length).toBeGreaterThan(1);
  });
  it.each([401, 403])('%i після рішення закриває діалог і прибирає всі захищені дані та дії', async status => {
    const fetcher = network(request => request.method === 'POST' ? json({ code: 'editor.role_required' }, status) : json(queuePage)); const { client } = mount(); await openReport(); submit();
    await screen.findByText(status === 403 ? 'Цей розділ доступний лише редакторам словника.' : 'Відкрийте тренер знову в Telegram, щоб продовжити.'); expect(screen.queryByRole('region', { name: /Розглянути|Відкотити зміну\?/ })).not.toBeInTheDocument(); expect(screen.queryByText(report.reporter_alias)).not.toBeInTheDocument(); expect(screen.queryByRole('tab', { name: 'Застосовані' })).not.toBeInTheDocument(); await waitFor(() => expect(client.getQueriesData({ queryKey: editorQueryKey }).every(([, data]) => data === undefined)).toBe(true)); expect(requests(fetcher, decisionPath)).toHaveLength(1);
  });
});

describe('Застосовані зміни й відкат', () => {
  it('AC-48: показує поле, фактичні значення, підтвердження й дату, accepted_answer має null-пояснення', async () => {
    network(); mount(); await openApplied(); const cards = screen.getAllByRole('region', { name: 'Зміна: urząd' });
    expect(within(cards[0]).getByText('державна установа')).toBeVisible(); expect(within(cards[0]).getByText('установа')).toBeVisible(); expect(within(cards[0]).getByText(/автоматичною звіркою/)).toBeVisible(); expect(within(cards[0]).getByText(/2 лист.*2026.*11:00.*UTC/)).toBeVisible();
    expect(within(cards[1]).getByText('Прийнятий варіант відповіді')).toBeVisible(); expect(within(cards[1]).getByText('Варіанта ще не було')).toBeVisible(); expect(within(cards[1]).getByText(answerChange.new_value)).toBeVisible(); expect(screen.queryByText('accepted_answer')).not.toBeInTheDocument();
    expect(within(cards[1]).getByRole('button', { name: 'Лише поточна' })).toBeDisabled();
  });
  it('без server rollback_allowed запит неможливий; скасування підтвердження також не викликає POST', async () => {
    const fetcher = network(); mount(); await openApplied(); fireEvent.click(screen.getByRole('button', { name: 'Лише поточна' })); expect(screen.queryByRole('region', { name: /Розглянути|Відкотити зміну\?/ })).not.toBeInTheDocument(); fireEvent.click(screen.getByRole('button', { name: 'Відкотити' }));
    expect(screen.getByRole('heading', { name: 'Відкотити зміну?' })).toBeVisible(); expect(requests(fetcher, rollbackPath)).toHaveLength(0); fireEvent.click(screen.getByRole('button', { name: 'Залишити без змін' })); expect(requests(fetcher, rollbackPath)).toHaveLength(0);
  });
  it('відкат чекає підтвердження, блокує подвійний POST й оновлює лише за серверним успіхом', async () => {
    let release!: (value: Response) => void; let accepted = false;
    const fetcher = network(request => request.method === 'POST' ? new Promise<Response>(resolve => { release = resolve; }) : json(request.url.includes('/applied-changes') ? { ...appliedPage, items: [{ ...change, rollback_allowed: !accepted }] } : queuePage)); mount(); const dialog = await openRollback();
    act(() => { fireEvent.click(screen.getByRole('button', { name: 'Підтвердити відкат' })); fireEvent.click(screen.getByRole('button', { name: 'Підтвердити відкат' })); });
    await waitFor(() => expect(requests(fetcher, rollbackPath)).toHaveLength(1)); expect(screen.getByRole('button', { name: 'Відновлюємо версію…' })).toBeDisabled(); fireEvent(dialog, new Event('cancel', { cancelable: true })); expect(dialog).toBeInTheDocument(); expect(screen.queryByText(/Попередню версію відновлено/)).not.toBeInTheDocument();
    accepted = true; await act(async () => { release(json(rollbackResult)); }); await screen.findByText(/Попередню версію відновлено/); await screen.findByRole('button', { name: 'Лише поточна' }); expect(screen.queryByRole('region', { name: /Розглянути|Відкотити зміну\?/ })).not.toBeInTheDocument(); expect(screen.getByText(change.new_value)).toBeVisible();
  });
  it('AC-49: застарілий 409 перечитує список і не підміняє значення чи результат успіхом', async () => {
    let conflict = false; const fetcher = network(request => request.method === 'POST' ? (conflict = true, json({ code: 'editor.rollback_not_current' }, 409)) : json(request.url.includes('/applied-changes') ? { ...appliedPage, items: [{ ...change, rollback_allowed: !conflict }] } : queuePage)); mount(); await openRollback(); fireEvent.click(screen.getByRole('button', { name: 'Підтвердити відкат' }));
    await screen.findByText(/Відкотити можна лише поточну версію запису. Список оновлено/); await screen.findByRole('button', { name: 'Лише поточна' }); expect(screen.queryByRole('region', { name: /Розглянути|Відкотити зміну\?/ })).not.toBeInTheDocument(); expect(screen.queryByText(/Попередню версію відновлено/)).not.toBeInTheDocument(); expect(screen.getByText(change.new_value)).toBeVisible(); expect(requests(fetcher, appliedPath).length).toBeGreaterThan(1);
  });
  it('мережна помилка залишає підтвердження для свідомого повтору', async () => {
    const fetcher = network(request => request.method === 'POST' ? Promise.reject(new TypeError('Недоступно')) : json(request.url.includes('/applied-changes') ? appliedPage : queuePage)); mount(); await openRollback(); fireEvent.click(screen.getByRole('button', { name: 'Підтвердити відкат' }));
    await screen.findByText('Не вдалося відкотити зміну. Нічого не підтверджено — спробуйте ще раз.'); expect(screen.getByRole('button', { name: 'Підтвердити відкат' })).toBeEnabled(); expect(requests(fetcher, rollbackPath)).toHaveLength(1); fireEvent.click(screen.getByRole('button', { name: 'Підтвердити відкат' })); await waitFor(() => expect(requests(fetcher, rollbackPath)).toHaveLength(2));
  });
  it('AC-50: 403 відкату прибирає список і діалог без повторного запиту', async () => {
    const fetcher = network(request => request.method === 'POST' ? json({ code: 'editor.role_required' }, 403) : json(request.url.includes('/applied-changes') ? appliedPage : queuePage)); mount(); await openRollback(); fireEvent.click(screen.getByRole('button', { name: 'Підтвердити відкат' }));
    await screen.findByText('Цей розділ доступний лише редакторам словника.'); expect(screen.queryByRole('region', { name: /Розглянути|Відкотити зміну\?/ })).not.toBeInTheDocument(); expect(screen.queryByText(change.new_value)).not.toBeInTheDocument(); expect(screen.queryByRole('button', { name: 'Відкотити' })).not.toBeInTheDocument(); expect(requests(fetcher, rollbackPath)).toHaveLength(1);
  });
});

describe('Семантичні межі остаточного значення', () => {
  it.each([
    [report, 'у'.repeat(200), true], [report, 'у'.repeat(201), false],
    [{ ...report, field: 'example' }, 'a'.repeat(500), true], [{ ...report, field: 'example' }, 'a'.repeat(501), false],
    [report, 'urząd', false], [report, 'установа; орган', false],
    [{ ...report, field: 'pronunciation' }, 'ужонд', true], [{ ...report, field: 'pronunciation' }, 'urząd', false],
    [{ ...report, field: 'lemma' }, 'urząd', true], [{ ...report, field: 'lemma' }, 'dwa słowa', false],
    [disputeReport, 'w urzędzie', true], [disputeReport, 'уряд', false],
    [missingReport, 'Zaświadczenie', true], [missingReport, 'два слова', false],
    [report, 'установа\n', false], [report, '   ', false],
  ] as [QueueItem, string, boolean][])('$origin/$field, довжина й мова відповідають серверним правилам', (item, corrected, valid) => {
    expect(Object.keys(validateDecision(item, { ...initialDraft(item), corrected })).length === 0).toBe(valid);
  });
});
