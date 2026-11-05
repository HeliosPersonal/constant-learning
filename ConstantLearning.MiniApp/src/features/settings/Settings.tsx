import { DAILY_LIMIT_MAX, DAILY_LIMIT_MIN } from '../../components/LimitSlider';
import { LimitStepper } from '../../components/LimitStepper';
import { BackButton } from '../../components/BackButton';
import { useEffect, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button } from '../../components/ui';
import { learnerQueryKey } from '../onboarding/api';
import { timeZoneOptions } from '../onboarding/timeZone';
import { SettingsApiError, updateSettings } from './api';
import { learnerLanguagesQueryKey, listLearnerLanguages } from './language/api';
import { draftFromLearner, limitLabel, settingsChanges, validateSettings, weekDays, type Learner, type SettingsDraft, type SettingsErrors } from './model';
import { ThemeChoices } from './ThemeChoices';
import { ResetProgressSection } from './ResetProgressSection';
import { LanguageSection } from './language/LanguageSection';
import './settings.css';

const SAVED_VISIBLE_MS = 2000;

export default function Settings({ learner, onBack, onStart, onOpenEditor }: { learner: Learner; onBack: (notice?: string) => void; onStart: () => void; onOpenEditor?: () => void }) {
  const [saved, setSaved] = useState(learner);
  const [draft, setDraft] = useState(() => draftFromLearner(learner));
  const [touched, setTouched] = useState(false);
  const [serverErrors, setServerErrors] = useState<SettingsErrors>({});
  const [phase, setPhase] = useState<'idle' | 'saving' | 'saved'>('idle');
  const [failed, setFailed] = useState<string>();
  const [blocked, setBlocked] = useState<'auth' | 'onboarding' | null>(null);
  const delay = useRef(0);
  const sent = useRef<string>();
  const queryClient = useQueryClient();
  // Назва активної мови для заголовків груп: ліміт і нагадування належать саме їй (per-language, ADR-0005).
  const languages = useQuery({ queryKey: learnerLanguagesQueryKey, queryFn: ({ signal }) => listLearnerLanguages(signal), retry: false, refetchOnWindowFocus: false });
  const activeName = languages.data?.items.find(language => language.is_active)?.display_name_uk;
  const changes = settingsChanges(draft, saved);
  const key = JSON.stringify(changes);
  const validation = touched ? validateSettings(draft, saved) : {};
  const errors: SettingsErrors = { ...validation, ...serverErrors };
  const invalid = Object.keys(validation).length > 0;
  const mutation = useMutation({
    mutationFn: updateSettings, retry: false,
    onSuccess(updated) {
      setSaved(updated); setServerErrors({}); setFailed(undefined); setPhase('saved');
      queryClient.setQueryData(learnerQueryKey, updated);
    },
    onError(error, variables) {
      setPhase('idle'); setFailed(JSON.stringify(variables));
      if (error instanceof SettingsApiError && error.status === 401) {
        setBlocked('auth'); setServerErrors({ general: 'Відкрийте тренер знову в Telegram, щоб продовжити.' });
      } else if (error instanceof SettingsApiError && error.status === 404 && error.body?.code === 'learner.not_onboarded') {
        setBlocked('onboarding'); setServerErrors({ general: 'Спершу налаштуйте тренер.' });
      } else if (error instanceof SettingsApiError && error.status === 400) {
        const messages: Record<string, SettingsErrors> = {
          'settings.invalid_limit': { limit: 'Доступно від 1 до 10 нових слів на день.' },
          'settings.time_zone_required': { zone: 'Виберіть і підтвердьте часовий пояс.' },
          'settings.invalid_time_zone': { zone: 'Невідомий часовий пояс. Виберіть інший зі списку.' },
          'settings.reminder_days_required': { days: 'Виберіть хоча б один день або вимкніть нагадування.' },
          'settings.invalid_reminder': { time: 'Перевірте час і дні нагадування.' },
        };
        const next = messages[error.body?.code ?? ''] ?? { general: 'Не вдалося зберегти налаштування. Перевірте введені значення.' };
        if (next.days || next.time) setDraft(value => ({ ...value, reminder: true }));
        setServerErrors(next);
      } else setServerErrors({ general: 'Не вдалося зберегти налаштування. Ваші зміни залишилися на екрані.' });
    },
  });

  // Автозбереження: зміни йдуть на сервер після короткої паузи (миттєво для перемикачів). Невдалі зміни
  // автоматично не повторюються — лише за явним «Повторити». Однаковий запит двічі поспіль не надсилається.
  useEffect(() => {
    if (!touched || blocked || mutation.isPending || invalid || !Object.keys(changes).length || key === failed || key === sent.current) return;
    const timer = setTimeout(() => { sent.current = key; setPhase('saving'); mutation.mutate(changes); }, delay.current);
    return () => clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key, touched, blocked, mutation.isPending, invalid, failed]);
  useEffect(() => {
    if (phase !== 'saved') return;
    const timer = setTimeout(() => setPhase('idle'), SAVED_VISIBLE_MS);
    return () => clearTimeout(timer);
  }, [phase]);

  function change(patch: Partial<SettingsDraft>, wait = 0) {
    delay.current = wait; sent.current = undefined; setTouched(true); setServerErrors({}); setPhase('idle');
    setDraft(value => ({ ...value, ...patch }));
  }
  const setLimit = (limit: number) => change({ limit: Math.min(DAILY_LIMIT_MAX, Math.max(DAILY_LIMIT_MIN, limit)) }, 600);
  const retry = () => { delay.current = 0; sent.current = undefined; setServerErrors({}); setFailed(undefined); };
  const pendingLimit = saved.pending_daily_new_words_limit;
  const showCurrent = pendingLimit != null || draft.limit !== saved.daily_new_words_limit;

  return <>
    <BackButton className="settings-navigation" navLabel="Навігація налаштувань" onBack={() => onBack()}>← До «Сьогодні»</BackButton>
    <h1 className="ios-title">Налаштування</h1>
    <div className="ios-status" role="status" aria-live="polite">
      {phase === 'saving' && 'Зберігаємо…'}
      {phase === 'saved' && 'Збережено ✓'}
      {errors.general && <><span className="ios-danger">{errors.general}</span>{!blocked && <button type="button" className="ios-plain" onClick={retry}>Повторити</button>}</>}
    </div>
    {blocked === 'onboarding' && <Button size="l" mode="outline" stretched onClick={() => { queryClient.setQueryData(learnerQueryKey, null); onStart(); }}>До початку</Button>}

    <form onSubmit={event => event.preventDefault()} noValidate aria-label="Налаштування профілю" aria-busy={mutation.isPending}>
      <fieldset className="settings-fields" disabled={!!blocked}>
        <div className="ios-caption settings-first">Вигляд</div>
        <div className="ios-group"><div className="ios-row"><span>Тема</span><ThemeChoices /></div></div>
        {!blocked && <LanguageSection />}

        <div className="ios-caption">Навчання{activeName ? ` · ${activeName}` : ''}</div>
        <div className="ios-group"><div className="ios-row"><span id="settings-limit-label">Нових слів на день</span>
          <LimitStepper value={draft.limit} onChange={setLimit} labelledBy="settings-limit-label" describedBy="settings-limit-hint" />
        </div></div>
        <div className="ios-footnote" id="settings-limit-hint">
          <p>Зміна діятиме з наступної навчальної доби.</p>
          {showCurrent && <p>Сьогодні: <strong>{limitLabel(saved.daily_new_words_limit)}</strong>.</p>}
          {pendingLimit != null && <p className="ios-accent">З наступної доби: <strong>{limitLabel(pendingLimit)}</strong>.</p>}
          {pendingLimit != null && draft.limit === saved.daily_new_words_limit && <p>Повернення до чинного темпу скасує відкладену зміну.</p>}
        </div>
        {errors.limit && <p className="ios-footnote ios-footnote--error" role="alert">{errors.limit}</p>}

        <div className="ios-caption">Нагадування{activeName ? ` · ${activeName}` : ''}</div>
        <div className="ios-group">
          <label className="ios-row"><span>Нагадувати про заняття</span><input type="checkbox" role="switch" className="ios-switch" checked={draft.reminder} onChange={event => change({ reminder: event.target.checked })} /></label>
          {draft.reminder && <>
            <div className="ios-row"><span>Час</span><input type="time" className="ios-time" aria-label="Час нагадування" value={draft.time} aria-invalid={!!errors.time} onChange={event => change({ time: event.target.value }, 600)} /></div>
            <fieldset className="ios-days" aria-invalid={!!errors.days}><legend className="sr-only">Дні тижня</legend>
              {weekDays.map(day => <label key={day.value} className={`ios-day${draft.days.includes(day.value) ? ' ios-day--on' : ''}`}><input type="checkbox" aria-label={day.full} checked={draft.days.includes(day.value)} onChange={event => change({ days: event.target.checked ? [...draft.days, day.value] : draft.days.filter(item => item !== day.value) }, 600)} /><span aria-hidden="true">{day.label}</span></label>)}
            </fieldset>
          </>}
        </div>
        {!draft.reminder && <p className="ios-footnote">Нагадування вимкнені.</p>}
        {errors.time && <p className="ios-footnote ios-footnote--error" role="alert">{errors.time}</p>}
        {errors.days && <p className="ios-footnote ios-footnote--error" role="alert">{errors.days}</p>}

        <div className="ios-caption">Місцевий час</div>
        <div className="ios-group">
          <div className="ios-row ios-row--select"><span>Часовий пояс</span><span className="ios-value">{draft.zone}<span className="ios-chevron" aria-hidden="true">›</span></span>
            <select aria-label="Часовий пояс" value={draft.zone} aria-invalid={!!errors.zone} onChange={event => change({ zone: event.target.value, zoneConfirmed: true })}>
              {timeZoneOptions(saved.time_zone).map(zone => <option key={zone} value={zone}>{zone}</option>)}
            </select></div>
          {!draft.zoneConfirmed && <button type="button" className="ios-row ios-plain ios-accent" onClick={() => change({ zoneConfirmed: true })}>Підтвердити часовий пояс</button>}
        </div>
        <p className="ios-footnote">{draft.zoneConfirmed ? 'Навчальний день і нагадування враховують цей пояс.' : 'Підтвердьте часовий пояс, щоб нагадування приходили у ваш місцевий час.'}</p>
        {errors.zone && <p className="ios-footnote ios-footnote--error" role="alert">{errors.zone}</p>}
      </fieldset>
    </form>
    {onOpenEditor && !blocked && <>
      <div className="ios-caption">Редактор</div>
      <div className="ios-group"><button type="button" className="ios-row ios-plain" onClick={onOpenEditor}>Редактор словника<span className="ios-value ios-chevron" aria-hidden="true">›</span></button></div>
    </>}
    {!blocked && <ResetProgressSection onDone={onBack} />}
  </>;
}
