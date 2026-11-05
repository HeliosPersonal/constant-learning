import { useEffect, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Result, Spinner } from '../../components/ui';
import { ActiveLanguageBadge } from '../../components/ActiveLanguageBadge';
import { learnerQueryKey, type Learner } from '../onboarding/api';
import { detectTimeZone, isTimeZone } from '../onboarding/timeZone';
import { SettingsApiError, updateSettings } from '../settings/api';
import { getToday, openSession, reportTodayReady, startExtraPractice, startExtraWord, todayQueryKey, TodayApiError, type ExtraActionReason, type SessionResponse, type TodaySummary } from './api';
import { todayTiming } from './timing';
import './today.css';

const shownNotices = new WeakSet<TodaySummary>();
const sessionNotices = {
  new_words_limit_reached: 'Денний ліміт нових слів вичерпано. Продовжуємо повторення за розкладом.',
  course_completed: 'Базовий курс завершено. Продовжуємо повторення за розкладом.',
  course_replenished: 'Курс поповнено новими словами. Вони з’являтимуться у вашому щоденному темпі.',
};
const extraReasons: Record<NonNullable<ExtraActionReason>, string> = {
  plan_not_done: 'Спершу завершіть заняття за планом.',
  course_completed: 'Нових слів у курсі більше немає.',
  no_eligible_words: 'Зараз немає слів для практики. Нова практика стане доступною, коли з’являться вивчені слова без відповіді сьогодні.',
};

export function Today({ learner, onOpenSession, onStart, notice, onNoticeShown, getDeviceTimeZone = detectTimeZone }: {
  learner: Learner;
  onOpenSession?: (session: SessionResponse) => void;
  onStart?: () => void;
  notice?: string;
  onNoticeShown?: () => void;
  getDeviceTimeZone?: () => string | undefined;
}) {
  const queryClient = useQueryClient();
  const [notification, setNotification] = useState(notice);
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

  const extra = useMutation({
    mutationFn: (variant: 'word' | 'practice') => variant === 'word' ? startExtraWord() : startExtraPractice(),
    retry: false,
    onSuccess(value) { setNotices(value.notices); setMessage(undefined); onOpenSession?.(value); },
    onError(error) {
      const terminalMessage = terminal(error);
      if (terminalMessage) setMessage(terminalMessage);
      else if (error instanceof TodayApiError && error.status === 429) setMessage('Занадто швидко — зачекайте кілька секунд і спробуйте ще раз.');
      else if (error instanceof TodayApiError && error.status === 409) { setMessage('Ця дія зараз недоступна. Оновлюємо ваш день.'); void today.refetch(); }
      else setMessage('Не вдалося почати. Спробуйте ще раз.');
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

  useEffect(() => { if (notice) onNoticeShown?.(); }, [notice, onNoticeShown]);
  useEffect(() => {
    if (today.data?.course_replenished_notice && !shownNotices.has(today.data)) {
      // Показ повідомлення про поповнення курсу один раз на нові дані — навмисний setState у ефекті.
      // eslint-disable-next-line react-hooks/set-state-in-effect
      shownNotices.add(today.data); setReplenished(true);
    }
  }, [today.data]);
  useEffect(() => {
    if (!today.data || today.isError || blocked || session.isPending || extra.isPending || zone.isPending) return;
    const frame = requestAnimationFrame(() => {
      if (buttonRef.current) todayTiming.ready(reportTodayReady);
    });
    return () => cancelAnimationFrame(frame);
  }, [today.data, today.isError, blocked, session.isPending, extra.isPending, zone.isPending]);

  const startOver = () => { queryClient.setQueryData(learnerQueryKey, null); queryClient.removeQueries({ queryKey: todayQueryKey }); onStart?.(); };
  const heading = <><h1 id="today-title">Сьогодні</h1><ActiveLanguageBadge learner={learner} /></>;
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
  const noNewWords = !summary.new_words_available_today;
  const extraActions = summary.extra_actions;
  const planDone = extraActions.plan_done;
  const noAvailableLesson = summary.due_review_count === 0 && !summary.has_active_session && (summary.course_state === 'completed' || noNewWords);
  const extraBusy = extra.isPending || session.isPending || zone.isPending || !!blocked;
  const date = new Intl.DateTimeFormat('uk-UA', { day: 'numeric', month: 'long', timeZone: 'UTC' }).format(new Date(`${summary.learner_day}T12:00:00Z`));
  const proposeZone = deviceZone && deviceZone !== learner.time_zone && deviceZone !== dismissedZone;

  function begin() {
    if (submitting.current || session.isPending || extra.isPending || zone.isPending || blocked || noAvailableLesson) return;
    if (!onOpenSession) { setMessage('Заняття зараз недоступне. Спробуйте відкрити тренер пізніше.'); return; }
    submitting.current = true; setMessage(undefined); setNotices([]); session.mutate(summary.has_active_session);
  }

  function beginExtra(variant: 'word' | 'practice') {
    if (submitting.current || session.isPending || extra.isPending || zone.isPending || blocked) return;
    if (!onOpenSession) { setMessage('Заняття зараз недоступне. Спробуйте відкрити тренер пізніше.'); return; }
    submitting.current = true; setMessage(undefined); setNotices([]); extra.mutate(variant);
  }

  return <>
    {notification && <Card className="card today-notice" role="status" aria-label="Повідомлення про прогрес"><p className="caption">{notification}</p><Button size="l" mode="outline" stretched onClick={() => setNotification(undefined)}>Зрозуміло</Button></Card>}
    <Card className="card today" aria-labelledby="today-title">{heading}<p className="caption today-date"><time dateTime={summary.learner_day}>{date}</time> · <span className="today-zone-name">{learner.time_zone}</span></p>
      <dl className="today-statistics" aria-label="Ваш навчальний прогрес">
        <div><dt>Слів до повторення</dt><dd>{summary.due_review_count}</dd></div>
        <div><dt>Пригадуєте</dt><dd>{summary.recalled_words_count}</dd></div>
        <div><dt>Занять за 7 днів</dt><dd>{summary.completed_sessions_last_7_days}</dd></div>
      </dl>
      {summary.course_state === 'completed' ? <div className="today-course"><h2>Базовий курс завершено</h2><p className="caption">Ви вже отримали всі слова курсу. Повторення триватимуть за вашим розкладом.</p></div>
        : <p className="caption today-pace">Темп — {learner.daily_new_words_limit} {learner.daily_new_words_limit === 3 ? 'нові слова' : 'нових слів'} на день</p>}
      {summary.due_review_count === 0 && !summary.has_active_session && !planDone && <p className="caption" id="today-no-reviews">{summary.course_state === 'completed' ? 'На зараз повторень немає. Завітайте пізніше.' : noNewWords ? 'Нові слова на сьогодні вже пройдено. Повторення з’являться за розкладом.' : 'На зараз повторень немає. У занятті можуть бути нові слова за вашим темпом.'}</p>}
      {summary.has_active_session && <p className="caption">На вас чекає розпочате заняття. Продовжимо з того місця, де зупинилися.</p>}
      {planDone
        ? <div className="today-extra">
            <p className="eyebrow" id="today-plan-done">ПЛАН ДНЯ ВИКОНАНО</p>
            <p className="caption">Можна взяти ще одне нове слово або потренувати вже вивчені.</p>
            <div className="today-actions">
              <Button ref={buttonRef} size="l" stretched loading={extra.isPending && extra.variables === 'word'} disabled={extraBusy || !extraActions.extra_new_word.available} aria-describedby={extraActions.extra_new_word.available ? undefined : 'today-extra-word-reason'} onClick={() => beginExtra('word')}>{extra.isPending && extra.variables === 'word' ? 'Беремо нове слово…' : 'Ще одне слово'}</Button>
              {!extraActions.extra_new_word.available && extraActions.extra_new_word.reason && <p className="caption" id="today-extra-word-reason">{extraReasons[extraActions.extra_new_word.reason]}</p>}
              <Button size="l" mode="outline" stretched loading={extra.isPending && extra.variables === 'practice'} disabled={extraBusy || !extraActions.extra_practice.available} aria-describedby={extraActions.extra_practice.available ? undefined : 'today-extra-practice-reason'} onClick={() => beginExtra('practice')}>{extra.isPending && extra.variables === 'practice' ? 'Готуємо практику…' : 'Потренувати вивчені'}</Button>
              {!extraActions.extra_practice.available && extraActions.extra_practice.reason && <p className="caption" id="today-extra-practice-reason">{extraReasons[extraActions.extra_practice.reason]}</p>}
            </div>
          </div>
        : <Button ref={buttonRef} size="l" stretched loading={session.isPending} disabled={session.isPending || extra.isPending || zone.isPending || !!blocked || noAvailableLesson} aria-describedby={noAvailableLesson ? 'today-no-reviews' : undefined} onClick={begin}>{session.isPending ? 'Відкриваємо заняття…' : summary.has_active_session ? 'Продовжити заняття' : noAvailableLesson ? 'На сьогодні все' : 'Почати заняття'}</Button>}
      <div ref={messageRef} className="today-status" tabIndex={-1} aria-live="polite">{(session.isPending || extra.isPending) && <p className="caption" role="status">{extra.isPending ? 'Готуємо заняття…' : 'Відкриваємо заняття…'}</p>}{message && <Result tone="error">{message}</Result>}{notices.map(notice => <p className="caption" role="status" key={notice}>{sessionNotices[notice]}</p>)}</div>
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
