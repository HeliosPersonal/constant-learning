import { useRef, useState, type FormEvent } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Switch } from '@telegram-apps/telegram-ui';
import { Button, Card, Result } from '../../components/ui';
import { completeOnboarding, learnerQueryKey, OnboardingApiError, type Learner, type OnboardingRequest } from './api';
import { configuredPrivacyContact, type PrivacyContact } from './privacy';
import { detectTimeZone, isTimeZone, timeZoneOptions } from './timeZone';
import './onboarding.css';

type Day = NonNullable<OnboardingRequest['reminder']>['days_of_week'][number];
const days: { value: Day; label: string; full: string }[] = [
  { value: 'mon', label: 'Пн', full: 'Понеділок' }, { value: 'tue', label: 'Вт', full: 'Вівторок' },
  { value: 'wed', label: 'Ср', full: 'Середа' }, { value: 'thu', label: 'Чт', full: 'Четвер' },
  { value: 'fri', label: 'Пт', full: 'П’ятниця' }, { value: 'sat', label: 'Сб', full: 'Субота' }, { value: 'sun', label: 'Нд', full: 'Неділя' },
];
interface Errors { days?: string; zone?: string; time?: string; limit?: string; general?: string }
interface OnboardingProps {
  onComplete: (learner: Learner) => void;
  privacyContact?: PrivacyContact | null;
  getDeviceTimeZone?: () => string | undefined;
}

export function Onboarding({ onComplete, privacyContact = configuredPrivacyContact, getDeviceTimeZone = detectTimeZone }: OnboardingProps) {
  const [limit, setLimit] = useState<3 | 5 | 10>(5);
  const [reminder, setReminder] = useState(false);
  const [selectedDays, setSelectedDays] = useState<Day[]>([]);
  const [time, setTime] = useState('19:00');
  const [detectedZone] = useState(() => getDeviceTimeZone());
  const [zone, setZone] = useState(detectedZone ?? '');
  const [zoneConfirmed, setZoneConfirmed] = useState(!!detectedZone);
  const [errors, setErrors] = useState<Errors>({});
  const [authBlocked, setAuthBlocked] = useState(false);
  const submitting = useRef(false);
  const errorRef = useRef<HTMLDivElement>(null);
  const client = useQueryClient();
  const mutation = useMutation({
    mutationFn: completeOnboarding,
    retry: false,
    onSuccess(learner) { client.setQueryData(learnerQueryKey, learner); onComplete(learner); },
    onError(error) {
      if (error instanceof OnboardingApiError && error.status === 401) {
        setAuthBlocked(true);
        setErrors({ general: 'Відкрийте тренер знову в Telegram, щоб продовжити.' });
      } else if (error instanceof OnboardingApiError && error.status === 400) {
        const messages: Record<string, Errors> = {
          'settings.reminder_days_required': { days: 'Виберіть хоча б один день або вимкніть нагадування.' },
          'settings.time_zone_required': { zone: 'Виберіть і підтвердьте часовий пояс для нагадування.' },
          'settings.invalid_reminder': { time: 'Перевірте час і дні нагадування.' },
          'settings.invalid_limit': { limit: 'Виберіть 3, 5 або 10 нових слів на день.' },
        };
        const next = messages[error.body?.code ?? ''] ?? { general: 'Не вдалося зберегти налаштування. Перевірте введені значення.' };
        if (next.days || next.time) setReminder(true);
        setErrors(next);
      } else setErrors({ general: 'Не вдалося зберегти налаштування. Ваш вибір збережено на екрані — спробуйте ще раз.' });
      requestAnimationFrame(() => errorRef.current?.focus());
    },
    onSettled() { submitting.current = false; },
  });

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (submitting.current || mutation.isPending || authBlocked || !privacyContact) return;
    const next: Errors = {};
    if (reminder && selectedDays.length === 0) next.days = 'Виберіть хоча б один день або вимкніть нагадування.';
    if (reminder && !/^([01]\d|2[0-3]):[0-5]\d$/.test(time)) next.time = 'Виберіть час нагадування у форматі годин і хвилин.';
    if ((reminder || zone) && (!isTimeZone(zone) || !zoneConfirmed)) next.zone = reminder ? 'Виберіть і підтвердьте часовий пояс для нагадування.' : 'Підтвердьте вибраний часовий пояс або залиште його невизначеним.';
    setErrors(next);
    if (Object.keys(next).length) { requestAnimationFrame(() => errorRef.current?.focus()); return; }
    submitting.current = true;
    mutation.mutate({
      daily_new_words_limit: limit,
      time_zone: zoneConfirmed && zone ? zone : null,
      reminder: reminder ? { local_time: time, days_of_week: days.filter(day => selectedDays.includes(day.value)).map(day => day.value) } : null,
    });
  }

  return <>
    <div className="intro"><p className="eyebrow">ПЕРШИЙ КРОК</p><h1>Ваш темп. Ваші слова.</h1><p>Кілька хвилин польської щодня — почнімо з комфортного ритму.</p></div>
    <Card aria-labelledby="privacy-title"><span className="card-index">ПЕРЕД ПОЧАТКОМ</span><h2 id="privacy-title">Ваші дані — під вашим контролем</h2>
      <p className="caption">Для навчання ми обробляємо ваш Telegram-ідентифікатор, налаштування та навчальну історію.</p>
      <p className="caption">Власник проєкту видаляє ваші дані за запитом протягом 7 календарних днів. У зашифрованих резервних копіях вони можуть залишатися до 14 днів.</p>
      {privacyContact ? <a className="privacy-contact" href={privacyContact.url} rel="noopener noreferrer">Запит на видалення: {privacyContact.label}</a>
        : <p className="caption" role="status">Контакт для запитів на видалення тимчасово недоступний.</p>}
    </Card>
    <form onSubmit={submit} noValidate aria-label="Налаштування навчання" aria-busy={mutation.isPending}>
      <fieldset className="onboarding-fields" disabled={mutation.isPending || authBlocked}>
        <Card aria-labelledby="pace-title"><span className="card-index">01 / ТЕМП</span><h2 id="pace-title">Скільки нових слів на день?</h2>
          <p className="caption">Повторення знайомих слів додаватимуться окремо.</p>
          <fieldset className="pace-options"><legend className="sr-only">Денний ліміт нових слів</legend>
            {([3, 5, 10] as const).map(value => <label key={value} className="pace-option"><input type="radio" name="daily-limit" aria-label={`${value} ${value === 3 ? 'нові слова' : 'нових слів'}`} checked={limit === value} onChange={() => setLimit(value)} /><span>{value}</span><small>{value === 3 ? 'слова' : 'слів'}</small>{limit === value && <span className="pace-selected">Обрано ✓</span>}</label>)}
          </fieldset>
          {errors.limit && <Result tone="error">{errors.limit}</Result>}
          <p className="caption onboarding-hint">Темп можна змінити пізніше в налаштуваннях.</p>
        </Card>
        <Card aria-labelledby="zone-title"><span className="card-index">02 / МІСЦЕВИЙ ЧАС</span><h2 id="zone-title">Коли починається ваш день</h2>
          <label className="field-label" htmlFor="time-zone">Часовий пояс</label>
          <select id="time-zone" value={zone} aria-invalid={!!errors.zone} aria-describedby={errors.zone ? 'zone-error' : 'zone-hint'} onChange={event => { setZone(event.target.value); setZoneConfirmed(false); setErrors(value => ({ ...value, zone: undefined })); }}>
            <option value="">Не визначено — вибрати вручну</option>
            {timeZoneOptions(detectedZone).map(value => <option key={value} value={value}>{value}</option>)}
          </select>
          <p className="caption onboarding-hint" id="zone-hint">{zoneConfirmed ? (zone === detectedZone ? 'Визначено за часом вашого пристрою.' : 'Часовий пояс підтверджено.') : 'Без вибраного поясу навчальний день рахується за часом Варшави. Для нагадувань потрібно вибрати пояс.'}</p>
          {!!zone && !zoneConfirmed && <Button mode="outline" size="l" stretched onClick={() => { setZoneConfirmed(true); setErrors(value => ({ ...value, zone: undefined })); }}>Підтвердити часовий пояс</Button>}
          {errors.zone && <div id="zone-error"><Result tone="error">{errors.zone}</Result></div>}
        </Card>
        <Card aria-labelledby="reminder-title"><span className="card-index">03 / НАГАДУВАННЯ</span><div className="reminder-heading"><h2 id="reminder-title">Лагідне нагадування</h2><Switch id="reminder-enabled" checked={reminder} onChange={event => { setReminder(event.target.checked); setErrors({}); }} aria-label="Нагадувати про заняття" /></div>
          <p className="caption">За бажанням бот нагадає про заняття у вибрані дні.</p>
          {reminder ? <div className="reminder-options">
            <label className="field-label" htmlFor="reminder-time">Час нагадування</label><input id="reminder-time" type="time" value={time} aria-invalid={!!errors.time} aria-describedby={errors.time ? 'time-error' : undefined} onChange={event => setTime(event.target.value)} />
            {errors.time && <div id="time-error"><Result tone="error">{errors.time}</Result></div>}
            <fieldset className="day-options" aria-describedby={errors.days ? 'days-error' : undefined}><legend>Дні тижня</legend>
              {days.map(day => <label key={day.value}><input type="checkbox" aria-label={day.full} checked={selectedDays.includes(day.value)} onChange={event => setSelectedDays(value => event.target.checked ? [...value, day.value] : value.filter(item => item !== day.value))} /><span>{selectedDays.includes(day.value) ? '✓ ' : ''}{day.label}</span></label>)}
            </fieldset>{errors.days && <div id="days-error"><Result tone="error">{errors.days}</Result></div>}
            <Button mode="plain" size="l" stretched onClick={() => { setReminder(false); setErrors({}); }}>Пропустити нагадування</Button>
          </div> : <p className="caption onboarding-hint">Нагадування вимкнені. Можна ввімкнути пізніше.</p>}
        </Card>
      </fieldset>
      <div ref={errorRef} tabIndex={-1} className="submission-message" aria-live="polite">{errors.general && <Result tone="error">{errors.general}</Result>}{Object.keys(errors).some(key => key !== 'general' && errors[key as keyof Errors]) && <p className="caption">Перевірте позначені поля вище.</p>}</div>
      {!privacyContact && <p id="onboarding-unavailable" className="caption" role="status">Почати навчання зараз не вдається. Спробуйте пізніше.</p>}
      <Button type="submit" size="l" stretched loading={mutation.isPending} disabled={mutation.isPending || authBlocked || !privacyContact} aria-describedby={!privacyContact ? 'onboarding-unavailable' : undefined}>{mutation.isPending ? 'Зберігаємо налаштування…' : 'Почати навчання'}</Button>
      <p className="caption submit-note">Ваші налаштування можна змінити будь-коли.</p>
    </form>
  </>;
}
