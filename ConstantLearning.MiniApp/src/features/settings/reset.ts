import { api } from '../../api/client';
import type { components } from '../../api/schema';

export class ProgressResetApiError extends Error {
  constructor(readonly status: number, readonly body?: components['schemas']['Error']) { super('Не вдалося скинути прогрес.'); }
}

/** Скидає весь навчальний прогрес learner. Повертає true, якщо дані стерто, false — якщо прогрес уже порожній (AC-09). */
export async function resetProgress(): Promise<boolean> {
  const result = await api.POST('/api/v1/learners/me/progress/reset');
  if (result.response.status !== 200 || !result.data) throw new ProgressResetApiError(result.response.status, result.error);
  return result.data.erased;
}
