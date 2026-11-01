import { lazy, Suspense, useCallback, useEffect, useState } from 'react';
import { Button, Card, Spinner } from './components/ui';
import { OnboardingGate, TodayPlaceholder } from './features/onboarding/OnboardingGate';
import type { Learner } from './features/onboarding/api';
import type { PrivacyContact } from './features/onboarding/privacy';

const ComponentPreview = lazy(() => import('./components/ComponentPreview').then(module => ({ default: module.ComponentPreview })));
const Settings = lazy(() => import('./features/settings/Settings'));

export function App({ onNavigateToToday, privacyContact }: { onNavigateToToday?: (learner: Learner) => void; privacyContact?: PrivacyContact | null }) {
  const [route, setRoute] = useState(window.location.pathname);
  useEffect(() => { const pop = () => setRoute(window.location.pathname); window.addEventListener('popstate', pop); return () => window.removeEventListener('popstate', pop); }, []);
  const go = useCallback((path: string) => { window.history.pushState(null, '', path); setRoute(path); }, []);
  const toToday = useCallback((learner: Learner) => { setRoute('/today'); onNavigateToToday?.(learner); }, [onNavigateToToday]);
  if (route === '/dev/components') return <Suspense fallback={<p role="status">Завантажуємо приклад…</p>}><ComponentPreview /></Suspense>;
  return <main className="app-shell">
    <header className="app-header"><a className="brand" href="/" aria-label="Constant Learning — головна"><span className="brand-icon" aria-hidden="true">cl.</span><span>Constant<br />Learning</span></a><span className="eyebrow">ПОЛЬСЬКА ЩОДНЯ</span></header>
    <OnboardingGate onNavigateToToday={toToday} privacyContact={privacyContact} preserveRoute={route === '/settings'}
      notOnboarded={route === '/settings' ? <Card><h1>Почнімо знайомство</h1><p>Спершу налаштуйте тренер.</p><Button size="l" stretched onClick={() => go('/')}>До початку</Button></Card> : undefined}
      renderLearner={learner => route === '/settings'
        ? <Suspense fallback={<div className="loading-state" role="status"><Spinner size="s" /> Відкриваємо налаштування…</div>}><Settings learner={learner} onBack={() => go('/today')} onStart={() => go('/')} /></Suspense>
        : <><TodayPlaceholder learner={learner} /><nav aria-label="Основна навігація"><Button mode="outline" size="l" stretched onClick={() => go('/settings')}>Налаштування</Button></nav></>} />
    <footer>По слову ближче до впевненості.</footer>
  </main>;
}
