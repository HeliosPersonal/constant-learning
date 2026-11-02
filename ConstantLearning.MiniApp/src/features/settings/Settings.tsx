import { LimitSlider } from '../../components/LimitSlider';
import { useRef, useState, type FormEvent } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Switch } from '@telegram-apps/telegram-ui';
import { Button, Card, Dialog, Result } from '../../components/ui';
import { learnerQueryKey } from '../onboarding/api';
import { timeZoneOptions } from '../onboarding/timeZone';
import { SettingsApiError, updateSettings } from './api';
import { draftFromLearner, limitLabel, settingsChanges, validateSettings, weekDays, type Learner, type SettingsErrors } from './model';
import { ThemeChoices } from './ThemeChoices';
import './settings.css';

export default function Settings({ learner, onBack, onStart }: { learner: Learner; onBack: () => void; onStart: () => void }) {
  const [saved, setSaved] = useState(learner);
  const [draft, setDraft] = useState(() => draftFromLearner(learner));
  const [errors, setErrors] = useState<SettingsErrors>({});
  const [success, setSuccess] = useState(false);
  const [blocked, setBlocked] = useState<'auth' | 'onboarding' | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const submitting = useRef(false);
  const messageRef = useRef<HTMLDivElement>(null);
  const queryClient = useQueryClient();
  const changes = settingsChanges(draft, saved);
  const dirty = Object.keys(changes).length > 0 || draft.zone !== saved.time_zone;
  const mutation = useMutation({
    mutationFn: updateSettings, retry: false,
    onSuccess(updated) {
      setSaved(updated); setDraft(draftFromLearner(updated)); setErrors({}); setSuccess(true);
      queryClient.setQueryData(learnerQueryKey, updated);
    },
    onError(error) {
      setSuccess(false);
      if (error instanceof SettingsApiError && error.status === 401) {
        setBlocked('auth'); setErrors({ general: 'Відкрийте тренер знову в Telegram, щоб продовжити.' });
      } else if (error instanceof SettingsApiError && error.status === 404 && error.body?.code === 'learner.not_onboarded') {
        setBlocked('onboarding'); setErrors({ general: 'Спершу налаштуйте тренер.' });
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
        setErrors(next);
      } else setErrors({ general: 'Не вдалося зберегти налаштування. Ваші зміни залишилися на екрані — спробуйте ще раз.' });
      requestAnimationFrame(() => messageRef.current?.focus());
    },
    onSettled() { submitting.current = false; },
  });

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (submitting.current || mutation.isPending || blocked) return;
    const next = validateSettings(draft, saved);
    setErrors(next); setSuccess(false);
    if (Object.keys(next).length) { requestAnimationFrame(() => messageRef.current?.focus()); return; }
    if (!Object.keys(changes).length) return;
    submitting.current = true; mutation.mutate(changes);
  }

  return <>
    <nav className="settings-navigation" aria-label="Навігація налаштувань"><Button mode="plain" size="l" onClick={onBack}>← До «Сьогодні»</Button></nav>
    <div className="intro"><p className="eyebrow">ВАШ ПРОСТІР</p><h1>Налаштування</h1><p>Знайдіть зручний ритм і вигляд для щоденної польської.</p></div>
    <Card aria-labelledby="settings-theme-title"><span className="card-index">ВИГЛЯД</span><h2 id="settings-theme-title">Світла чи темна?</h2>
      <ThemeChoices /><p className="caption">Тема змінюється одразу й зберігається на цьому пристрої.</p><Button size="l" mode="outline" stretched onClick={() => setDialogOpen(true)}>Про оформлення</Button>
    </Card>
    <form onSubmit={submit} noValidate aria-label="Налаштування профілю" aria-busy={mutation.isPending}>
      <fieldset className="settings-fields" disabled={mutation.isPending || !!blocked}>
        <Card aria-labelledby="settings-limit-title"><span className="card-index">01 / ТЕМП</span><h2 id="settings-limit-title">Нові слова на день</h2>
          <p className="caption settings-current">Сьогодні: <strong>{limitLabel(saved.daily_new_words_limit)}</strong>.</p>
          {saved.pending_daily_new_words_limit != null && <p className="caption settings-pending">З наступної доби: <strong>{limitLabel(saved.pending_daily_new_words_limit)}</strong>.</p>}
          <fieldset className="settings-limit-options" aria-describedby="settings-limit-hint"><legend className="sr-only">Денний ліміт нових слів</legend><LimitSlider value={draft.limit} describedBy="settings-limit-hint" onChange={value => { setDraft(current => ({ ...current, limit: value })); setSuccess(false); }} /></fieldset>
          <p className="caption settings-hint" id="settings-limit-hint">Новий ліміт діятиме з наступної навчальної доби у вибраному часовому поясі.</p>
          {saved.pending_daily_new_words_limit != null && draft.limit === saved.daily_new_words_limit && <p className="caption">Повернення до чинного темпу скасує відкладену зміну.</p>}
          {errors.limit && <Result tone="error">{errors.limit}</Result>}
        </Card>
        <Card aria-labelledby="settings-zone-title"><span className="card-index">02 / МІСЦЕВИЙ ЧАС</span><h2 id="settings-zone-title">Час вашого дня</h2>
          <label className="settings-label" htmlFor="settings-zone">Часовий пояс</label><select id="settings-zone" value={draft.zone} aria-invalid={!!errors.zone} aria-describedby={errors.zone ? 'settings-zone-error' : 'settings-zone-hint'} onChange={event => { setDraft(value => ({ ...value, zone: event.target.value, zoneConfirmed: event.target.value === saved.time_zone && saved.time_zone_confirmed })); setErrors(value => ({ ...value, zone: undefined })); setSuccess(false); }}>
            {timeZoneOptions(saved.time_zone).map(zone => <option key={zone} value={zone}>{zone}</option>)}
          </select>
          <p className="caption settings-hint" id="settings-zone-hint">{draft.zoneConfirmed ? 'Навчальний день і нагадування враховують цей пояс.' : 'Підтвердьте часовий пояс, щоб нагадування приходили у ваш місцевий час.'}</p>
          {!draft.zoneConfirmed && <Button size="l" stretched mode="outline" onClick={() => { setDraft(value => ({ ...value, zoneConfirmed: true })); setErrors(value => ({ ...value, zone: undefined })); setSuccess(false); }}>Підтвердити часовий пояс</Button>}
          {errors.zone && <div id="settings-zone-error"><Result tone="error">{errors.zone}</Result></div>}
        </Card>
        <Card aria-labelledby="settings-reminder-title"><span className="card-index">03 / НАГАДУВАННЯ</span><div className="settings-reminder-heading"><h2 id="settings-reminder-title">Трохи підтримки</h2><Switch aria-label="Нагадувати про заняття" checked={draft.reminder} onChange={event => { setDraft(value => ({ ...value, reminder: event.target.checked })); setErrors({}); setSuccess(false); }} /></div>
          <p className="caption">Бот нагадає про заняття у вибрані дні за вашим місцевим часом.</p>
          {draft.reminder ? <>
            <label className="settings-label" htmlFor="settings-time">Час нагадування</label><input id="settings-time" type="time" value={draft.time} aria-invalid={!!errors.time} aria-describedby={errors.time ? 'settings-time-error' : undefined} onChange={event => { setDraft(value => ({ ...value, time: event.target.value })); setSuccess(false); }} />
            {errors.time && <div id="settings-time-error"><Result tone="error">{errors.time}</Result></div>}
            <fieldset className="settings-days" aria-invalid={!!errors.days} aria-describedby={errors.days ? 'settings-days-error' : undefined}><legend>Дні тижня</legend>
              {weekDays.map(day => <label key={day.value}><input type="checkbox" aria-label={day.full} checked={draft.days.includes(day.value)} onChange={event => { setDraft(value => ({ ...value, days: event.target.checked ? [...value.days, day.value] : value.days.filter(item => item !== day.value) })); setSuccess(false); }} /><span>{draft.days.includes(day.value) ? '✓ ' : ''}{day.label}</span></label>)}
            </fieldset>{errors.days && <div id="settings-days-error"><Result tone="error">{errors.days}</Result></div>}
          </> : <p className="caption settings-hint">Нагадування вимкнені.</p>}
        </Card>
      </fieldset>
      <div ref={messageRef} tabIndex={-1} className="settings-message" aria-live="polite">{success && <Result tone="success">Налаштування збережено.</Result>}{errors.general && <Result tone="error">{errors.general}</Result>}{Object.entries(errors).some(([key, value]) => key !== 'general' && value) && <p className="caption">Перевірте позначені поля вище.</p>}</div>
      {blocked === 'onboarding' && <Button size="l" mode="outline" stretched onClick={() => { queryClient.setQueryData(learnerQueryKey, null); onStart(); }}>До початку</Button>}
      <Button type="submit" size="l" stretched loading={mutation.isPending} disabled={!dirty || mutation.isPending || !!blocked}>{mutation.isPending ? 'Зберігаємо зміни…' : 'Зберегти зміни'}</Button>
    </form>
    <Dialog open={dialogOpen} onClose={() => setDialogOpen(false)}><p>Приглушені теми Everforest допомагають зосередитися на словах.</p><ThemeChoices group="settings-dialog-theme" /><p className="caption">Вигляд змінюється лише на цьому пристрої. Ваші відповіді та незбережені налаштування залишаться на місці.</p></Dialog>
  </>;
}
