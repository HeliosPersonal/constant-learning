import { useRef, useState, type ReactNode } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Result, Spinner } from '../../../components/ui';
import { openSession, todayQueryKey, TodayApiError, type SessionResponse } from '../../today/api';
import { NewWordCard } from '../new-word/NewWordCard';
import { actOnNewWord, currentSession, SessionApiError } from './api';
import { Recall } from '../recall/Recall';
import './session.css';

type Props = { initialSession?: SessionResponse; onToday: () => void; onBusy?: (value: boolean) => void; renderRecall?: (session: SessionResponse, refresh: () => Promise<SessionResponse>) => ReactNode; renderResult?: (session: SessionResponse, refresh: () => Promise<SessionResponse>) => ReactNode };
export default function SessionRoute({ initialSession, onToday, onBusy, renderRecall, renderResult }: Props) {
  const client = useQueryClient();
  const session = useQuery({ queryKey: ['session', 'current'], queryFn: ({ signal }) => currentSession(signal), initialData: initialSession, retry: false, refetchOnMount: 'always', refetchOnReconnect: false });
  const [message, setMessage] = useState<string>();
  const [terminal, setTerminal] = useState<'expired' | 'missing' | 'auth' | 'complete'>();
  const [pending, setPending] = useState(false);
  const [recallBusy, setRecallBusy] = useState(false);
  const [starting, setStarting] = useState(false);
  const [startError, setStartError] = useState<string>();
  const updateBusy = (value: boolean) => { setRecallBusy(value); onBusy?.(value); };
  const locked = useRef(false);
  const refresh = async () => { const value = await currentSession(); client.setQueryData(['session', 'current'], value); return value; };
  const replace = (value: SessionResponse) => client.setQueryData(['session', 'current'], value);
  const leave = () => { client.removeQueries({ queryKey: ['session', 'current'] }); void client.invalidateQueries({ queryKey: todayQueryKey }); onToday(); };
  async function startNew() {
    setStarting(true); setStartError(undefined);
    try {
      const value = await openSession(false);
      setTerminal(undefined); client.setQueryData(['session', 'current'], value); void client.invalidateQueries({ queryKey: todayQueryKey });
    } catch (error) {
      setStartError(error instanceof TodayApiError && error.status === 401 ? 'Відкрийте тренер знову в Telegram, щоб продовжити.' : 'Не вдалося відкрити заняття. Спробуйте ще раз.');
    } finally { setStarting(false); }
  }
  async function action(kind: 'acknowledge' | 'already-known') {
    if (locked.current || !session.data?.current_item) return;
    locked.current = true; setPending(true); onBusy?.(true); setMessage(undefined);
    try {
      await client.cancelQueries({ queryKey: ['session', 'current'] });
      const result = await actOnNewWord(session.data.id, session.data.current_item.id, kind);
      if (result.session_completed) {
        try { await refresh(); } catch (error) { if (!(error instanceof SessionApiError && error.status === 404)) throw error; setTerminal('complete'); }
        void client.invalidateQueries({ queryKey: todayQueryKey });
      } else await refresh();
    } catch (error) {
      if (error instanceof SessionApiError && error.code === 'session.expired') setTerminal('expired');
      else if (error instanceof SessionApiError && error.status === 401) setTerminal('auth');
      else if (error instanceof SessionApiError && error.status === 404) setTerminal('missing');
      else if (error instanceof SessionApiError && error.status === 409) {
        try { await refresh(); setMessage('Стан заняття оновлено.'); }
        catch { setMessage('Не вдалося оновити заняття. Спробуйте ще раз.'); }
      } else setMessage('Не вдалося зберегти дію або оновити заняття. Оновіть стан перед повтором.');
    } finally { locked.current = false; setPending(false); onBusy?.(false); }
  }
  if (terminal === 'complete') return <Card><p className="eyebrow">ГОТОВО</p><h1>Заняття завершено</h1><p>Ви пройшли всі картки цього заняття.</p><Button size="l" stretched onClick={leave}>← На головну</Button></Card>;
  if (terminal || session.isError) {
    const error = session.error;
    if (error instanceof SessionApiError && error.code === 'session.not_found') return <Card><h1>Активного заняття немає</h1><p>Можна почати нове заняття.</p>{startError && <Result tone="error">{startError}</Result>}<Button size="l" stretched disabled={starting} onClick={() => { void startNew(); }}>{starting ? 'Відкриваємо заняття…' : 'Почати заняття'}</Button><Button size="l" mode="outline" stretched onClick={leave}>← На головну</Button></Card>;
    const text = terminal === 'expired' ? 'Час цього заняття минув. Поверніться на «Сьогодні», щоб продовжити навчання.' : terminal === 'auth' || (error instanceof SessionApiError && error.status === 401) ? 'Відкрийте тренер знову в Telegram, щоб продовжити.' : terminal === 'missing' || (error instanceof SessionApiError && error.status === 404) ? 'Заняття або картку не знайдено. Можливо, заняття вже закрито або належить іншому користувачу.' : 'Не вдалося завантажити заняття. Спробуйте ще раз.';
    return <Card><h1>Заняття недоступне</h1><Result tone="error">{text}</Result>{!terminal && !(error instanceof SessionApiError && [401, 404].includes(error.status)) && <Button size="l" stretched onClick={() => { void session.refetch(); }}>Спробувати ще раз</Button>}<Button size="l" mode="outline" stretched onClick={leave}>← На головну</Button></Card>;
  }
  if (!session.data || (session.isFetching && !pending)) return <Card><div className="loading-state" role="status"><Spinner size="s" /> Завантажуємо заняття…</div></Card>;
  const data = session.data;
  return <Card aria-label="Заняття"><div className="session-progress"><span>Опрацьовано {data.completed_items} із {data.total_items}</span><progress aria-label="Прогрес заняття" value={data.completed_items} max={Math.max(data.total_items, 1)} /></div>
    {data.notices.map(notice => <p className="caption" role="status" key={notice}>{{ new_words_limit_reached: 'Денний ліміт нових слів вичерпано. Продовжуємо повторення за розкладом.', course_completed: 'Базовий курс завершено. Продовжуємо повторення за розкладом.', course_replenished: 'Курс поповнено новими словами. Вони з’являтимуться у вашому щоденному темпі.' }[notice]}</p>)}
    {data.last_result ? (renderResult ? renderResult(data, refresh) : <Recall key={data.last_result.item_id} session={data} refresh={refresh} replace={replace} onBusy={updateBusy} onTerminal={setTerminal} />)
      : data.current_item?.kind === 'new_word_card' ? <NewWordCard item={data.current_item} pending={pending || !!message} onAction={action} />
      : data.current_item ? (renderRecall ? renderRecall(data, refresh) : <Recall key={data.current_item.id} session={data} refresh={refresh} replace={replace} onBusy={updateBusy} onTerminal={setTerminal} />)
      : data.status === 'completed' ? <><h1>Заняття завершено</h1><p>Ви пройшли всі картки цього заняття.</p></> : <><h1>Картка недоступна</h1><p>Не вдалося отримати поточну картку заняття. Поверніться на «Сьогодні».</p></>}
    {pending && <p role="status">Зберігаємо та оновлюємо заняття…</p>}
    {message && <><Result tone="error">{message}</Result><Button size="l" stretched disabled={pending} onClick={() => { setPending(true); void refresh().then(() => setMessage(undefined)).catch(() => setMessage('Не вдалося оновити заняття. Спробуйте ще раз.')).finally(() => setPending(false)); }}>Оновити стан</Button></>}
    <Button className="session-leave" size="l" mode="plain" stretched disabled={pending || recallBusy} onClick={leave}>← На головну</Button>
  </Card>;
}
