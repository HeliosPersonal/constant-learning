import { api } from '../../../api/client';
import type { components } from '../../../api/schema';

export type SearchPage = components['schemas']['DictionarySearchPage'];
export type EntryCard = components['schemas']['DictionaryEntryCard'];
export type SearchCursor = { after?: string; before?: string };
export type MissingWordReportRequest = { origin: 'missing_word'; search_query: string };

export class DictionaryApiError extends Error {
  constructor(readonly status: number) { super('Не вдалося відкрити словник.'); }
}

export async function searchDictionary(query: string, cursor: SearchCursor, signal: AbortSignal): Promise<SearchPage> {
  const result = await api.GET('/api/v1/dictionary/search', { params: { query: { q: query, limit: 20, ...cursor } }, signal });
  if (result.response.status !== 200 || !result.data) throw new DictionaryApiError(result.response.status);
  return result.data;
}

export async function getDictionaryEntry(entryId: string, signal: AbortSignal): Promise<EntryCard> {
  const result = await api.GET('/api/v1/dictionary/entries/{entry_id}', { params: { path: { entry_id: entryId } }, signal });
  if (result.response.status !== 200 || !result.data) throw new DictionaryApiError(result.response.status);
  return result.data;
}
