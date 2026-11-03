import { api } from '../../api/client';
import type { components } from '../../api/schema';

export type LearnedWordStat = components['schemas']['LearnedWordStat'];
export type LearnedWordCard = components['schemas']['LearnedWordCard'];
export type LearnedWordsPage = components['schemas']['LearnedWordsPage'];
export type LearnedWordsSummary = components['schemas']['LearnedWordsSummary'];
export type StatsTab = components['schemas']['StatsTab'];
export type StatsSort = components['schemas']['StatsSort'];
export type StatsCursor = { after?: string; before?: string };

export class StatsApiError extends Error {
  constructor(readonly status: number, readonly body?: components['schemas']['Error']) { super('Не вдалося відкрити статистику.'); }
}

export async function getLearnedWordsSummary(signal?: AbortSignal): Promise<LearnedWordsSummary> {
  const result = await api.GET('/api/v1/stats/summary', { signal });
  if (result.response.status !== 200 || !result.data) throw new StatsApiError(result.response.status, result.error);
  return result.data;
}

export async function listLearnedWords(params: { tab: StatsTab; sort: StatsSort; q?: string; cursor?: StatsCursor }, signal?: AbortSignal): Promise<LearnedWordsPage> {
  const trimmed = params.q?.trim();
  const query = { tab: params.tab, sort: params.sort, limit: 20, ...params.cursor, ...(trimmed ? { q: trimmed } : {}) };
  const result = await api.GET('/api/v1/stats/words', { params: { query }, signal });
  if (result.response.status !== 200 || !result.data) throw new StatsApiError(result.response.status, result.error);
  return result.data;
}

export async function getLearnedWordCard(entryId: string, signal?: AbortSignal): Promise<LearnedWordCard> {
  const result = await api.GET('/api/v1/stats/words/{entry_id}', { params: { path: { entry_id: entryId } }, signal });
  if (result.response.status !== 200 || !result.data) throw new StatsApiError(result.response.status, result.error);
  return result.data;
}
