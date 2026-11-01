import { useCallback, useEffect, useRef } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Button, Card, Result, Spinner } from '../../components/ui';
import { getCurrentLearner, learnerQueryKey, OnboardingApiError, type Learner } from './api';
import { Onboarding } from './Onboarding';
import type { PrivacyContact } from './privacy';

export function OnboardingGate({ onNavigateToToday, privacyContact }: { onNavigateToToday?: (learner: Learner) => void; privacyContact?: PrivacyContact | null }) {
  const profile = useQuery({ queryKey: learnerQueryKey, queryFn: ({ signal }) => getCurrentLearner(signal), retry: false, refetchOnReconnect: false });
  const forwardedId = useRef<string | null>(null);
  const navigate = useCallback((learner: Learner) => {
    if (forwardedId.current === learner.id) return;
    forwardedId.current = learner.id;
    window.history.replaceState(null, '', '/today');
    onNavigateToToday?.(learner);
  }, [onNavigateToToday]);
  useEffect(() => { if (profile.data) navigate(profile.data); }, [profile.data, navigate]);

  if (profile.isPending) return <div className="loading-state app-status" role="status"><Spinner size="s" /> Відкриваємо ваш тренер…</div>;
  if (profile.isError) {
    const unauthorized = profile.error instanceof OnboardingApiError && profile.error.status === 401;
    return <Card aria-labelledby="profile-error-title"><h1 id="profile-error-title">Повернімося до навчання</h1><Result tone="error">{unauthorized ? 'Відкрийте тренер знову в Telegram, щоб продовжити.' : 'Не вдалося відкрити тренер. Спробуйте ще раз.'}</Result>
      {!unauthorized && <Button size="l" stretched disabled={profile.isFetching} onClick={() => { void profile.refetch(); }}>{profile.isFetching ? 'Завантажуємо…' : 'Спробувати ще раз'}</Button>}
    </Card>;
  }
  if (profile.data) return <Card aria-labelledby="today-title"><p className="eyebrow">ВАШ РИТМ</p><h1 id="today-title">Сьогодні</h1><p>Раді бачити вас у вашому просторі для польської.</p><p className="caption">Ваш темп — {profile.data.daily_new_words_limit} {profile.data.daily_new_words_limit === 3 ? 'нові слова' : 'нових слів'} на день.</p></Card>;
  return <Onboarding privacyContact={privacyContact} onComplete={navigate} />;
}
