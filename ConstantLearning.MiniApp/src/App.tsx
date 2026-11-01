import { lazy, Suspense } from 'react';
import { OnboardingGate } from './features/onboarding/OnboardingGate';
import type { Learner } from './features/onboarding/api';
import type { PrivacyContact } from './features/onboarding/privacy';

const ComponentPreview = lazy(() => import('./components/ComponentPreview').then(module => ({ default: module.ComponentPreview })));

export function App({ onNavigateToToday, privacyContact }: { onNavigateToToday?: (learner: Learner) => void; privacyContact?: PrivacyContact | null }) {
  if (window.location.pathname === '/dev/components') return <Suspense fallback={<p role="status">Завантажуємо приклад…</p>}><ComponentPreview /></Suspense>;
  return <main className="app-shell">
    <header className="app-header"><a className="brand" href="/" aria-label="Constant Learning — головна"><span className="brand-icon" aria-hidden="true">cl.</span><span>Constant<br />Learning</span></a><span className="eyebrow">ПОЛЬСЬКА ЩОДНЯ</span></header>
    <OnboardingGate onNavigateToToday={onNavigateToToday} privacyContact={privacyContact} />
    <footer>По слову ближче до впевненості.</footer>
  </main>;
}
