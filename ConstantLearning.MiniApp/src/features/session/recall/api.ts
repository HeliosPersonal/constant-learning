import { api } from '../../../api/client';
import type { components } from '../../../api/schema';
import { SessionApiError } from '../shell/api';

type Answer = components['schemas']['AnswerRequest'];
export async function answer(sessionId: string, itemId: string, body: Answer) {
  const result = await api.POST('/api/v1/sessions/{session_id}/items/{item_id}/answer', { params: { path: { session_id: sessionId, item_id: itemId } }, body });
  if (result.response.status !== 200 || !result.data) throw new SessionApiError(result.response.status, result.error?.code);
  return result.data;
}
export async function continueResult(sessionId: string, itemId: string) {
  const result = await api.POST('/api/v1/sessions/{session_id}/items/{item_id}/continue', { params: { path: { session_id: sessionId, item_id: itemId } } });
  if (result.response.status !== 200 || !result.data) throw new SessionApiError(result.response.status, result.error?.code);
  return result.data;
}
export async function dispute(answerEventId: string) {
  const result = await api.POST('/api/v1/answers/{answer_event_id}/dispute', { params: { path: { answer_event_id: answerEventId } } });
  if (result.response.status !== 200 || !result.data) throw new SessionApiError(result.response.status, result.error?.code);
  return result.data;
}
