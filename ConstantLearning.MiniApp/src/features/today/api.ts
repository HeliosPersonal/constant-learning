import { api } from '../../api/client';
import type { components } from '../../api/schema';

export type TodaySummary = components['schemas']['TodaySummary'];
export type SessionResponse = components['schemas']['Session'];
export type ExtraActions = components['schemas']['ExtraActions'];
export type ExtraActionReason = components['schemas']['ExtraActionAvailability']['reason'];
export const todayQueryKey = ['today'] as const;

export class TodayApiError extends Error {
  constructor(readonly status: number, readonly body?: components['schemas']['Error']) { super('Не вдалося відкрити навчання.'); }
}

export async function getToday(signal?: AbortSignal): Promise<TodaySummary> {
  const result = await api.GET('/api/v1/today', { signal });
  if (result.response.status !== 200 || !result.data) throw new TodayApiError(result.response.status, result.error);
  return result.data;
}

export async function openSession(resume: boolean): Promise<SessionResponse> {
  const result = resume ? await api.GET('/api/v1/sessions/current') : await api.POST('/api/v1/sessions');
  if (![200, 201].includes(result.response.status) || !result.data) throw new TodayApiError(result.response.status, result.error);
  return result.data;
}

export async function startExtraWord(): Promise<SessionResponse> {
  const result = await api.POST('/api/v1/sessions/extra-word');
  if (![200, 201].includes(result.response.status) || !result.data) throw new TodayApiError(result.response.status, result.error);
  return result.data;
}

export async function startExtraPractice(): Promise<SessionResponse> {
  const result = await api.POST('/api/v1/sessions/extra-practice');
  if (![200, 201].includes(result.response.status) || !result.data) throw new TodayApiError(result.response.status, result.error);
  return result.data;
}

export async function reportTodayReady(value_ms: number): Promise<void> {
  await api.POST('/api/v1/telemetry/client-timings', { body: { metric: 'today_ready_ms', value_ms } });
}
