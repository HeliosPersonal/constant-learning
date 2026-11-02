import { api } from '../../api/client';
import type { components } from '../../api/schema';

export type Learner = components['schemas']['Learner'];
export type OnboardingRequest = components['schemas']['OnboardingRequest'];
export type ApiErrorBody = components['schemas']['Error'];
export const learnerQueryKey = ['current-learner'] as const;

export class OnboardingApiError extends Error {
  constructor(readonly status?: number, readonly body?: ApiErrorBody) {
    super('Не вдалося виконати запит тренера.');
  }
}

export async function getCurrentLearner(signal?: AbortSignal): Promise<Learner | null> {
  const result = await api.GET('/api/v1/learners/me', { signal });
  if (result.response.status === 404 && result.error?.code === 'learner.not_onboarded') return null;
  if (result.response.status !== 200 || !result.data?.onboarded_at) throw new OnboardingApiError(result.response.status, result.error);
  return result.data;
}

export async function completeOnboarding(body: OnboardingRequest): Promise<Learner> {
  const result = await api.POST('/api/v1/learners/me/onboarding', { body });
  if (![200, 201].includes(result.response.status) || !result.data?.onboarded_at) throw new OnboardingApiError(result.response.status, result.error);
  return result.data;
}
