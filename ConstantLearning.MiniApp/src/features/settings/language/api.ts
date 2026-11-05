import { api } from '../../../api/client';
import type { components } from '../../../api/schema';

export type TargetLanguage = components['schemas']['TargetLanguage'];
export type LearnerLanguage = components['schemas']['LearnerLanguage'];
export type LearnerLanguages = components['schemas']['LearnerLanguages'];

export const languageCatalogQueryKey = ['languages'] as const;
export const learnerLanguagesQueryKey = ['learner', 'languages'] as const;

export class LanguageApiError extends Error {
  constructor(readonly status: number, readonly body?: components['schemas']['Error']) { super('Не вдалося змінити мову навчання.'); }
}

/** Каталог усіх target languages у порядку sort_order; `available` — лише увімкнені з контентом (AC-01, AC-02). */
export async function listTargetLanguages(signal?: AbortSignal): Promise<TargetLanguage[]> {
  const result = await api.GET('/api/v1/languages', { signal });
  if (result.response.status !== 200 || !result.data) throw new LanguageApiError(result.response.status, result.error);
  return result.data.items;
}

/** Мови, які learner вивчає, й активна мова (active_target_language; null, якщо жодної ще не почато). */
export async function listLearnerLanguages(signal?: AbortSignal): Promise<LearnerLanguages> {
  const result = await api.GET('/api/v1/learners/me/languages', { signal });
  if (result.response.status !== 200 || !result.data) throw new LanguageApiError(result.response.status, result.error);
  return result.data;
}

/** Починає вивчати мову й робить її активною; повтор на вже початій мові безпечний (AC-01, AC-12). */
export async function startLearnerLanguage(target_language: string): Promise<LearnerLanguage> {
  const result = await api.POST('/api/v1/learners/me/languages', { body: { target_language } });
  if (![200, 201].includes(result.response.status) || !result.data) throw new LanguageApiError(result.response.status, result.error);
  return result.data;
}

/** Перемикає активну мову серед уже вивчених; 409 language.not_studying, якщо мову ще не почато (AC-03). */
export async function setActiveLanguage(target_language: string): Promise<string> {
  const result = await api.PUT('/api/v1/learners/me/active-language', { body: { target_language } });
  if (result.response.status !== 200 || !result.data) throw new LanguageApiError(result.response.status, result.error);
  return result.data.active_target_language;
}

/** Клієнтська телеметрія тривалості перемикання мови (NFR ≤1500 ms); помилки телеметрії мовчазні. */
export async function reportLanguageSwitch(value_ms: number): Promise<void> {
  if (!Number.isFinite(value_ms) || value_ms < 0 || value_ms > 60_000) return;
  await api.POST('/api/v1/telemetry/client-timings', { body: { metric: 'language_switch_ms', value_ms: Math.round(value_ms) } });
}
