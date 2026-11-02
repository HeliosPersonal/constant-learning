import { api } from '../../../api/client';
import type { components } from '../../../api/schema';

export class ContentReportApiError extends Error {
  constructor(readonly status: number, readonly body?: components['schemas']['Error']) { super('Не вдалося надіслати повідомлення.'); }
}

export async function createContentReport(body: components['schemas']['ContentReportCreate']): Promise<components['schemas']['ContentReportAccepted']> {
  const result = await api.POST('/api/v1/content-reports', { body });
  if (result.response.status !== 202 || !result.data?.id || !['pending_verification', 'in_editor_queue'].includes(result.data.status)) throw new ContentReportApiError(result.response.status, result.error);
  return result.data;
}
