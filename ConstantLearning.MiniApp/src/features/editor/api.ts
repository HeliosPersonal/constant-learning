import { api } from '../../api/client';
import type { components } from '../../api/schema';

export type QueueItem = components['schemas']['EditorQueueItem'];
export type QueuePage = components['schemas']['EditorQueuePage'];
export type AppliedChange = components['schemas']['AppliedChange'];
export type AppliedPage = components['schemas']['AppliedChangePage'];
export type Decision = components['schemas']['EditorDecision'];
export type RejectionReason = components['schemas']['RejectionReason'];
export type Cursor = { after: string; before?: never } | { before: string; after?: never } | { after?: never; before?: never };
export const editorQueryKey = ['editor'] as const;

export class EditorApiError extends Error {
  constructor(readonly status: number, readonly body?: components['schemas']['Error']) { super('Не вдалося виконати редакторську дію.'); }
}

export async function listQueue(cursor: Cursor, targetLanguage?: string, signal?: AbortSignal): Promise<QueuePage> {
  const query = { ...cursor, limit: 20, ...(targetLanguage ? { target_language: targetLanguage } : {}) };
  const result = await api.GET('/api/v1/editor/content-reports', { params: { query }, signal });
  if (result.response.status !== 200 || !result.data) throw new EditorApiError(result.response.status, result.error);
  return result.data;
}

export async function listApplied(cursor: Cursor, signal?: AbortSignal): Promise<AppliedPage> {
  const result = await api.GET('/api/v1/editor/applied-changes', { params: { query: { ...cursor, limit: 20 } }, signal });
  if (result.response.status !== 200 || !result.data) throw new EditorApiError(result.response.status, result.error);
  return result.data;
}

export async function decideReport(id: string, body: Decision): Promise<components['schemas']['ContentReportClosed']> {
  const result = await api.POST('/api/v1/editor/content-reports/{report_id}/decision', { params: { path: { report_id: id } }, body });
  if (result.response.status !== 200 || result.data?.status !== 'closed' || result.data.id !== id) throw new EditorApiError(result.response.status, result.error);
  return result.data;
}

export async function rollbackChange(id: string): Promise<components['schemas']['RollbackResult']> {
  const result = await api.POST('/api/v1/editor/applied-changes/{change_id}/rollback', { params: { path: { change_id: id } } });
  if (result.response.status !== 200 || result.data?.change_id !== id) throw new EditorApiError(result.response.status, result.error);
  return result.data;
}
