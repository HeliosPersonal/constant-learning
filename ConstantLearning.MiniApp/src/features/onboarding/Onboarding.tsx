import { LimitStepper } from '../../components/LimitStepper';
import { useRef, useState, type FormEvent } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
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
  const [limit, setLimit] = useState(5);
  const [reminder, setReminder] = useState(false);
  const [selectedDays, setSelectedDays] = useState<Day[]>([]);
  const [time, setTime] = useState('19:00');
  const [detectedZone] = useState(() => getDeviceTimeZone());
  const [zone, setZone] = useState(detectedZone ?? '');
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
          'settings.invalid_limit': { limit: 'Виберіть від 1 до 10 нових слів на день.' },
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
    if (reminder && !isTimeZone(zone)) next.zone = 'Виберіть і підтвердьте часовий пояс для нагадування.';
    setErrors(next);
    if (Object.keys(next).length) { requestAnimationFrame(() => errorRef.current?.focus()); return; }
    submitting.current = true;
    mutation.mutate({
      daily_new_words_limit: limit,
      time_zone: zone || null,
      reminder: reminder ? { local_time: time, days_of_week: days.filter(day => selectedDays.includes(day.value)).map(day => day.value) } : null,
    });
  }

  return <>
    <div className="onboarding-hero"><h1>Ваш темп. Ваші слова.</h1><p>Кілька хвилин польської щодня — почнімо з комфортного ритму.</p></div>
    <form onSubmit={submit} noValidate aria-label="Налаштування навчання" aria-busy={mutation.isPending}>
      <fieldset className="onboarding-fields" disabled={mutation.isPending || authBlocked}>
        <div className="ios-caption">Навчання</div>
        <div className="ios-group"><div className="ios-row"><span id="pace-label">Нових слів на день</span><LimitStepper value={limit} onChange={setLimit} labelledBy="pace-label" describedBy="pace-hint" /></div></div>
        <p className="ios-footnote" id="pace-hint">Повторення знайомих слів додаються окремо. Темп можна змінити пізніше в налаштуваннях.</p>
        {errors.limit && <p className="ios-footnote ios-footnote--error" role="alert">{errors.limit}</p>}

        <div className="ios-caption">Місцевий час</div>
        <div className="ios-group"><div className="ios-row ios-row--select"><span>Часовий пояс</span><span className="ios-value">{zone || 'Не визначено'}<span className="ios-chevron" aria-hidden="true">›</span></span>
          <select aria-label="Часовий пояс" value={zone} aria-invalid={!!errors.zone} aria-describedby={errors.zone ? 'zone-error' : 'zone-hint'} onChange={event => { setZone(event.target.value); setErrors(value => ({ ...value, zone: undefined })); }}>
            <option value="">Не визначено — вибрати вручну</option>
            {timeZoneOptions(detectedZone).map(value => <option key={value} value={value}>{value}</option>)}
          </select></div></div>
        <p className="ios-footnote" id="zone-hint">{zone ? (zone === detectedZone ? 'Визначено за часом вашого пристрою.' : 'Часовий пояс вибрано.') : 'Без вибраного поясу навчальний день рахується за часом Варшави. Для нагадувань потрібно вибрати пояс.'}</p>
        {errors.zone && <p className="ios-footnote ios-footnote--error" id="zone-error" role="alert">{errors.zone}</p>}

        <div className="ios-caption">Нагадування</div>
        <div className="ios-group">
          <label className="ios-row"><span>Нагадувати про заняття</span><input type="checkbox" role="switch" className="ios-switch" id="reminder-enabled" checked={reminder} onChange={event => { setReminder(event.target.checked); setErrors({}); }} /></label>
          {reminder && <>
            <div className="ios-row"><span>Час</span><input type="time" className="ios-time" aria-label="Час нагадування" value={time} aria-invalid={!!errors.time} aria-describedby={errors.time ? 'time-error' : undefined} onChange={event => setTime(event.target.value)} /></div>
            <fieldset className="ios-days" aria-invalid={!!errors.days} aria-describedby={errors.days ? 'days-error' : undefined}><legend className="sr-only">Дні тижня</legend>
              {days.map(day => <label key={day.value} className={`ios-day${selectedDays.includes(day.value) ? ' ios-day--on' : ''}`}><input type="checkbox" aria-label={day.full} checked={selectedDays.includes(day.value)} onChange={event => setSelectedDays(value => event.target.checked ? [...value, day.value] : value.filter(item => item !== day.value))} /><span aria-hidden="true">{day.label}</span></label>)}
            </fieldset>
          </>}
        </div>
        <p className="ios-footnote">{reminder ? 'Бот нагадає про заняття у вибрані дні за вашим місцевим часом.' : 'Нагадування вимкнені. Можна ввімкнути пізніше.'}</p>
        {errors.time && <p className="ios-footnote ios-footnote--error" id="time-error" role="alert">{errors.time}</p>}
        {errors.days && <p className="ios-footnote ios-footnote--error" id="days-error" role="alert">{errors.days}</p>}
      </fieldset>

      <section aria-labelledby="privacy-title">
        <h2 className="ios-caption" id="privacy-title">Ваші дані — під вашим контролем</h2>
        <div className="ios-group">
          <div className="onboarding-privacy">
            <p>Для навчання ми обробляємо ваш Telegram-ідентифікатор, налаштування та навчальну історію.</p>
            <p>Власник проєкту видаляє ваші дані за запитом протягом 7 календарних днів. У зашифрованих резервних копіях вони можуть залишатися до 14 днів.</p>
          </div>
          {privacyContact ? <a className="ios-row ios-accent privacy-contact" href={privacyContact.url} rel="noopener noreferrer">Запит на видалення: {privacyContact.label}</a>
            : <p className="onboarding-privacy" role="status">Контакт для запитів на видалення тимчасово недоступний.</p>}
        </div>
      </section>

      <div ref={errorRef} tabIndex={-1} className="submission-message" aria-live="polite">{errors.general && <p className="ios-footnote ios-footnote--error" role="alert">{errors.general}</p>}{Object.keys(errors).some(key => key !== 'general' && errors[key as keyof Errors]) && <p className="ios-footnote">Перевірте позначені поля вище.</p>}</div>
      {!privacyContact && <p id="onboarding-unavailable" className="ios-footnote" role="status">Почати навчання зараз не вдається. Спробуйте пізніше.</p>}
      <button type="submit" className="ios-primary ios-plain" disabled={mutation.isPending || authBlocked || !privacyContact} aria-describedby={!privacyContact ? 'onboarding-unavailable' : undefined}>{mutation.isPending ? 'Зберігаємо налаштування…' : 'Почати навчання'}</button>
      <p className="ios-footnote submit-note">Ваші налаштування можна змінити будь-коли.</p>
    </form>
  </>;
}
