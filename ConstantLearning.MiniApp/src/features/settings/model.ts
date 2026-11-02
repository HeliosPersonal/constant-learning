import { DAILY_LIMIT_MAX, DAILY_LIMIT_MIN, newWordsLabel } from '../../components/LimitSlider';
import type { components } from '../../api/schema';
import { isTimeZone } from '../onboarding/timeZone';

export type Learner = components['schemas']['Learner'];
export type SettingsUpdate = components['schemas']['LearnerSettingsUpdate'];
export type Day = NonNullable<SettingsUpdate['reminder']>['days_of_week'][number];
export interface SettingsDraft { limit: number; zone: string; zoneConfirmed: boolean; reminder: boolean; time: string; days: Day[] }
export interface SettingsErrors { limit?: string; zone?: string; days?: string; time?: string; general?: string }
export const weekDays: { value: Day; label: string; full: string }[] = [
  { value: 'mon', label: 'Пн', full: 'Понеділок' }, { value: 'tue', label: 'Вт', full: 'Вівторок' },
  { value: 'wed', label: 'Ср', full: 'Середа' }, { value: 'thu', label: 'Чт', full: 'Четвер' },
  { value: 'fri', label: 'Пт', full: 'П’ятниця' }, { value: 'sat', label: 'Сб', full: 'Субота' }, { value: 'sun', label: 'Нд', full: 'Неділя' },
];

export const limitLabel = (limit: number) => `${limit} ${newWordsLabel(limit)}`;
export const isValidLimit = (limit: number) => Number.isInteger(limit) && limit >= DAILY_LIMIT_MIN && limit <= DAILY_LIMIT_MAX;

export function draftFromLearner(learner: Learner): SettingsDraft {
  return {
    limit: learner.pending_daily_new_words_limit ?? learner.daily_new_words_limit,
    zone: learner.time_zone, zoneConfirmed: learner.time_zone_confirmed,
    reminder: learner.reminder !== null, time: learner.reminder?.local_time ?? '19:00', days: learner.reminder?.days_of_week ?? [],
  };
}

export function settingsChanges(draft: SettingsDraft, learner: Learner): SettingsUpdate {
  const body: SettingsUpdate = {};
  if (draft.limit !== (learner.pending_daily_new_words_limit ?? learner.daily_new_words_limit) && isValidLimit(draft.limit)) body.daily_new_words_limit = draft.limit;
  if (draft.zoneConfirmed && (draft.zone !== learner.time_zone || !learner.time_zone_confirmed)) body.time_zone = draft.zone;
  const reminder = draft.reminder ? { local_time: draft.time, days_of_week: weekDays.filter(day => draft.days.includes(day.value)).map(day => day.value) } : null;
  const savedReminder = learner.reminder ? { local_time: learner.reminder.local_time, days_of_week: weekDays.filter(day => learner.reminder!.days_of_week.includes(day.value)).map(day => day.value) } : null;
  if (JSON.stringify(reminder) !== JSON.stringify(savedReminder)) body.reminder = reminder;
  return body;
}

export function validateSettings(draft: SettingsDraft, learner: Learner): SettingsErrors {
  const errors: SettingsErrors = {};
  if (!isValidLimit(draft.limit)) errors.limit = 'Доступно від 1 до 10 нових слів на день.';
  const zoneChanged = draft.zone !== learner.time_zone || (!learner.time_zone_confirmed && draft.zoneConfirmed);
  if ((zoneChanged || (draft.reminder && !draft.zoneConfirmed)) && (!isTimeZone(draft.zone) || !draft.zoneConfirmed)) errors.zone = 'Виберіть і підтвердьте часовий пояс.';
  if (draft.reminder && !draft.days.length) errors.days = 'Виберіть хоча б один день або вимкніть нагадування.';
  if (draft.reminder && !/^([01]\d|2[0-3]):[0-5]\d$/.test(draft.time)) errors.time = 'Виберіть коректний час нагадування.';
  return errors;
}
