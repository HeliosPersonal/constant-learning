import { api } from '../../../api/client';
import type { SessionResponse } from '../../today/api';

export class SessionApiError extends Error {
  constructor(readonly status: number, readonly code?: string) { super(code); }
}

export async function currentSession(signal?: AbortSignal): Promise<SessionResponse> {
  const result = await api.GET('/api/v1/sessions/current', { signal });
  if (result.response.status !== 200 || !result.data) throw new SessionApiError(result.response.status, result.error?.code);
  return result.data;
}

export async function actOnNewWord(sessionId: string, itemId: string, action: 'acknowledge' | 'already-known') {
  const params = { path: { session_id: sessionId, item_id: itemId } };
  const result = action === 'acknowledge'
    ? await api.POST('/api/v1/sessions/{session_id}/items/{item_id}/acknowledge', { params })
    : await api.POST('/api/v1/sessions/{session_id}/items/{item_id}/already-known', { params });
  if (result.response.status !== 200 || !result.data) throw new SessionApiError(result.response.status, result.error?.code);
  return result.data;
}
