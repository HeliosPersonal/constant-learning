import { api } from '../../api/client';
import type { components } from '../../api/schema';
import type { Learner, SettingsUpdate } from './model';

export class SettingsApiError extends Error {
  constructor(readonly status: number, readonly body?: components['schemas']['Error']) { super('Не вдалося зберегти налаштування.'); }
}

export async function updateSettings(body: SettingsUpdate): Promise<Learner> {
  const result = await api.PATCH('/api/v1/learners/me/settings', { body });
  if (result.response.status !== 200 || !result.data?.onboarded_at) throw new SettingsApiError(result.response.status, result.error);
  return result.data;
}
