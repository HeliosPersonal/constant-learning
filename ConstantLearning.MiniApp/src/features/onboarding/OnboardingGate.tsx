import { useCallback, useEffect, useRef, type ReactNode } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Button, Card, Result, Spinner } from '../../components/ui';
import { getCurrentLearner, learnerQueryKey, OnboardingApiError, type Learner } from './api';
import { Onboarding } from './Onboarding';
import type { PrivacyContact } from './privacy';
import { Today } from '../today/Today';

export function OnboardingGate({ onNavigateToToday, privacyContact, preserveRoute = false, renderLearner, notOnboarded }: {
  onNavigateToToday?: (learner: Learner) => void;
  privacyContact?: PrivacyContact | null;
  preserveRoute?: boolean;
  renderLearner?: (learner: Learner) => ReactNode;
  notOnboarded?: ReactNode;
}) {
  const profile = useQuery({ queryKey: learnerQueryKey, queryFn: ({ signal }) => getCurrentLearner(signal), retry: false, refetchOnReconnect: false });
  const forwardedId = useRef<string | null>(null);
  const navigate = useCallback((learner: Learner) => {
    if (forwardedId.current === learner.id) return;
    forwardedId.current = learner.id;
    window.history.replaceState(null, '', '/today');
    onNavigateToToday?.(learner);
  }, [onNavigateToToday]);
  useEffect(() => { if (profile.data && !preserveRoute) navigate(profile.data); }, [profile.data, navigate, preserveRoute]);

  if (profile.isPending) return <div className="loading-state app-status" role="status"><Spinner size="s" /> Відкриваємо ваш тренер…</div>;
  if (profile.isError) {
    const unauthorized = profile.error instanceof OnboardingApiError && profile.error.status === 401;
    return <Card aria-labelledby="profile-error-title"><h1 id="profile-error-title">Повернімося до навчання</h1><Result tone="error">{unauthorized ? 'Відкрийте тренер знову в Telegram, щоб продовжити.' : 'Не вдалося відкрити тренер. Спробуйте ще раз.'}</Result>
      {!unauthorized && <Button size="l" stretched disabled={profile.isFetching} onClick={() => { void profile.refetch(); }}>{profile.isFetching ? 'Завантажуємо…' : 'Спробувати ще раз'}</Button>}
    </Card>;
  }
  if (profile.data) return renderLearner ? renderLearner(profile.data) : <Today learner={profile.data} />;
  if (notOnboarded) return notOnboarded;
  return <Onboarding privacyContact={privacyContact} onComplete={navigate} />;
}
