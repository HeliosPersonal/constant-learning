import { useEffect, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Result, Spinner } from '../../components/ui';
import { learnerQueryKey, type Learner } from '../onboarding/api';
import { detectTimeZone, isTimeZone } from '../onboarding/timeZone';
import { SettingsApiError, updateSettings } from '../settings/api';
import { getToday, openSession, reportTodayReady, todayQueryKey, TodayApiError, type SessionResponse, type TodaySummary } from './api';
import { todayTiming } from './timing';
import './today.css';

const shownNotices = new WeakSet<TodaySummary>();
const sessionNotices = {
  new_words_limit_reached: 'Денний ліміт нових слів вичерпано. Продовжуємо повторення за розкладом.',
  course_completed: 'Базовий курс завершено. Продовжуємо повторення за розкладом.',
  course_replenished: 'Курс поповнено новими словами. Вони з’являтимуться у вашому щоденному темпі.',
};

export function Today({ learner, onOpenSession, onStart, getDeviceTimeZone = detectTimeZone }: {
  learner: Learner;
  onOpenSession?: (session: SessionResponse) => void;
  onStart?: () => void;
  getDeviceTimeZone?: () => string | undefined;
}) {
  const queryClient = useQueryClient();
  const today = useQuery({ queryKey: todayQueryKey, queryFn: ({ signal }) => getToday(signal), retry: false, refetchOnReconnect: false });
  const [deviceZone] = useState(() => { const zone = getDeviceTimeZone(); return zone && isTimeZone(zone) ? zone : undefined; });
  const [dismissedZone, setDismissedZone] = useState<string>();
  const [replenished, setReplenished] = useState(false);
  const [message, setMessage] = useState<string>();
  const [zoneMessage, setZoneMessage] = useState<string>();
  const [zoneSaved, setZoneSaved] = useState(false);
  const [blocked, setBlocked] = useState<'auth' | 'onboarding' | null>(null);
  const [notices, setNotices] = useState<SessionResponse['notices']>([]);
  const submitting = useRef(false);
  const confirming = useRef(false);
  const buttonRef = useRef<HTMLButtonElement>(null);
  const messageRef = useRef<HTMLDivElement>(null);

  const terminal = (error: unknown) => {
    if (error instanceof TodayApiError || error instanceof SettingsApiError) {
      if (error.status === 401) { setBlocked('auth'); return 'Відкрийте тренер знову в Telegram, щоб продовжити.'; }
      if (error.status === 404 && error.body?.code === 'learner.not_onboarded') { setBlocked('onboarding'); return 'Спершу налаштуйте тренер.'; }
    }
  };

  const session = useMutation({
    mutationFn: openSession, retry: false,
    onSuccess(value) { setNotices(value.notices); setMessage(undefined); onOpenSession?.(value); },
    onError(error) {
      const terminalMessage = terminal(error);
      if (terminalMessage) setMessage(terminalMessage);
      else if (error instanceof TodayApiError && error.status === 404 && error.body?.code === 'session.not_found') {
        setMessage('Попереднє заняття вже закрито. Оновлюємо ваш день — після цього можна почати нове.');
        void today.refetch();
      } else setMessage('Не вдалося відкрити заняття. Спробуйте ще раз.');
      requestAnimationFrame(() => messageRef.current?.focus());
    },
    onSettled() { submitting.current = false; },
  });

  const zone = useMutation({
    mutationFn: updateSettings, retry: false,
    async onSuccess(updated) {
      queryClient.setQueryData(learnerQueryKey, updated);
      setZoneSaved(true); setZoneMessage(undefined);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: learnerQueryKey }),
        queryClient.invalidateQueries({ queryKey: todayQueryKey }),
      ]);
    },
    onError(error) {
      setZoneMessage(terminal(error) ?? (error instanceof SettingsApiError && error.status === 400
        ? 'Не вдалося підтвердити часовий пояс. Виберіть його вручну в налаштуваннях.'
        : 'Не вдалося зберегти часовий пояс. Збережений пояс не змінився — спробуйте ще раз.'));
    },
    onSettled() { confirming.current = false; },
  });

  useEffect(() => {
    if (today.data?.course_replenished_notice && !shownNotices.has(today.data)) {
      shownNotices.add(today.data); setReplenished(true);
    }
  }, [today.data]);
  useEffect(() => {
    if (!today.data || today.isError || blocked || session.isPending || zone.isPending) return;
    const frame = requestAnimationFrame(() => {
      if (buttonRef.current) todayTiming.ready(reportTodayReady);
    });
    return () => cancelAnimationFrame(frame);
  }, [today.data, today.isError, blocked, session.isPending, zone.isPending]);

  const startOver = () => { queryClient.setQueryData(learnerQueryKey, null); queryClient.removeQueries({ queryKey: todayQueryKey }); onStart?.(); };
  const heading = <><p className="eyebrow">ВАШ РИТМ</p><h1 id="today-title">Сьогодні</h1></>;
  if (today.isPending) return <Card aria-labelledby="today-title">{heading}<div className="loading-state" role="status"><Spinner size="s" /> Збираємо ваш прогрес…</div></Card>;
  if (today.isError) {
    const unauthorized = today.error instanceof TodayApiError && today.error.status === 401;
    const notOnboarded = today.error instanceof TodayApiError && today.error.status === 404 && today.error.body?.code === 'learner.not_onboarded';
    return <Card aria-labelledby="today-title">{heading}<Result tone="error">{unauthorized ? 'Відкрийте тренер знову в Telegram, щоб продовжити.' : notOnboarded ? 'Спершу налаштуйте тренер.' : 'Не вдалося завантажити прогрес. Спробуйте ще раз.'}</Result>
      {notOnboarded && <Button size="l" stretched onClick={startOver}>До початку</Button>}
      {!unauthorized && !notOnboarded && <Button size="l" stretched disabled={today.isFetching} onClick={() => { void today.refetch(); }}>Спробувати ще раз</Button>}
    </Card>;
  }
  const summary = today.data;
  const noAvailableLesson = summary.course_state === 'completed' && summary.due_review_count === 0 && !summary.has_active_session;
  const date = new Intl.DateTimeFormat('uk-UA', { day: 'numeric', month: 'long', timeZone: 'UTC' }).format(new Date(`${summary.learner_day}T12:00:00Z`));
  const proposeZone = deviceZone && deviceZone !== learner.time_zone && deviceZone !== dismissedZone;

  function begin() {
    if (submitting.current || session.isPending || zone.isPending || blocked || noAvailableLesson) return;
    if (!onOpenSession) { setMessage('Заняття зараз недоступне. Спробуйте відкрити тренер пізніше.'); return; }
    submitting.current = true; setMessage(undefined); setNotices([]); session.mutate(summary.has_active_session);
  }

  return <>
    <Card aria-labelledby="today-title">{heading}<p className="caption today-date"><time dateTime={summary.learner_day}>{date}</time> · <span className="today-zone-name">{learner.time_zone}</span></p>
      <p className="caption">Крок за кроком до впевненої польської.</p>
      <dl className="today-statistics" aria-label="Ваш навчальний прогрес">
        <div><dt>Слів до повторення</dt><dd>{summary.due_review_count}</dd></div>
        <div><dt>Уже вдається пригадати</dt><dd>{summary.recalled_words_count}</dd></div>
        <div><dt>Завершених занять за 7 днів</dt><dd>{summary.completed_sessions_last_7_days}</dd></div>
      </dl>
      {summary.course_state === 'completed' ? <div className="today-course"><h2>Базовий курс завершено</h2><p className="caption">Ви вже отримали всі слова курсу. Повторення триватимуть за вашим розкладом.</p></div>
        : <p className="caption">Ваш темп — {learner.daily_new_words_limit} {learner.daily_new_words_limit === 3 ? 'нові слова' : 'нових слів'} на день. Нові слова додаються в межах денного ліміту.</p>}
      {summary.due_review_count === 0 && !summary.has_active_session && <p className="caption" id="today-no-reviews">{summary.course_state === 'completed' ? 'На зараз повторень немає. Завітайте пізніше.' : 'На зараз повторень немає. У занятті можуть бути нові слова за вашим темпом.'}</p>}
      {summary.has_active_session && <p className="caption">На вас чекає розпочате заняття. Продовжимо з того місця, де зупинилися.</p>}
      <Button ref={buttonRef} size="l" stretched loading={session.isPending} disabled={session.isPending || zone.isPending || !!blocked || noAvailableLesson} aria-describedby={noAvailableLesson ? 'today-no-reviews' : undefined} onClick={begin}>{session.isPending ? 'Відкриваємо заняття…' : summary.has_active_session ? 'Продовжити заняття' : noAvailableLesson ? 'На сьогодні все' : 'Почати заняття'}</Button>
      <div ref={messageRef} className="today-status" tabIndex={-1} aria-live="polite">{session.isPending && <p className="caption" role="status">Відкриваємо заняття…</p>}{message && <Result tone="error">{message}</Result>}{notices.map(notice => <p className="caption" role="status" key={notice}>{sessionNotices[notice]}</p>)}</div>
      {blocked === 'onboarding' && <Button size="l" stretched mode="outline" onClick={startOver}>До початку</Button>}
    </Card>
    {replenished && <Card className="card today-notice" aria-labelledby="today-replenished-title"><span className="eyebrow">ЩЕ ТРОХИ НОВОГО</span><h2 id="today-replenished-title">Курс поповнено</h2><p className="caption">З’явилися нові слова. Вивчатимемо їх у вашому щоденному темпі.</p><Button size="l" mode="outline" stretched onClick={() => setReplenished(false)}>Зрозуміло</Button></Card>}
    {proposeZone && <Card className="card today-zone" aria-labelledby="today-zone-title"><h2 id="today-zone-title">У вас інший місцевий час?</h2><p className="caption">На пристрої — <span className="today-zone-name">{deviceZone}</span>. Збережено — <span className="today-zone-name">{learner.time_zone}</span>.</p><p className="caption">Перейти на пояс пристрою для навчального дня й нагадувань? Зміна відбудеться лише після підтвердження.</p>
      <div className="today-actions"><Button size="l" stretched disabled={zone.isPending || session.isPending || !!blocked} loading={zone.isPending} onClick={() => { if (confirming.current || !deviceZone || !isTimeZone(deviceZone)) return; confirming.current = true; setZoneSaved(false); setZoneMessage(undefined); zone.mutate({ time_zone: deviceZone }); }}>{zone.isPending ? 'Зберігаємо часовий пояс…' : 'Підтвердити пояс пристрою'}</Button><Button size="l" stretched mode="plain" disabled={zone.isPending} onClick={() => { setDismissedZone(deviceZone); setZoneMessage(undefined); }}>Залишити збережений пояс</Button></div>
      {zone.isPending && <p className="caption today-status" role="status">Зберігаємо часовий пояс…</p>}
    </Card>}
    {zoneMessage && <Result tone="error">{zoneMessage}</Result>}{zoneSaved && <Result tone="success">Часовий пояс збережено.</Result>}
  </>;
}
