import { lazy, Suspense, useCallback, useEffect, useRef, useState } from 'react';
import { Button, Card, Spinner } from './components/ui';
import { OnboardingGate } from './features/onboarding/OnboardingGate';
import { Today } from './features/today/Today';
import type { SessionResponse } from './features/today/api';
import type { Learner } from './features/onboarding/api';
import type { PrivacyContact } from './features/onboarding/privacy';
import type { MissingWordReportRequest } from './features/dictionary/search/api';

const ComponentPreview = lazy(() => import('./components/ComponentPreview').then(module => ({ default: module.ComponentPreview })));
const Settings = lazy(() => import('./features/settings/Settings'));
const Dictionary = lazy(() => import('./features/dictionary/search/Dictionary'));
const SessionRoute = lazy(() => import('./features/session/shell/SessionRoute'));
const Editor = lazy(() => import('./features/editor/Editor'));

export function App({ onNavigateToToday, privacyContact, onReportMissing, onOpenSession }: { onNavigateToToday?: (learner: Learner) => void; privacyContact?: PrivacyContact | null; onReportMissing?: (request: MissingWordReportRequest) => void; onOpenSession?: (session: SessionResponse) => void }) {
  const [route, setRoute] = useState(window.location.pathname);
  const [openedSession, setOpenedSession] = useState<SessionResponse>();
  const sessionBusy = useRef(false);
  useEffect(() => { const pop = () => { if (sessionBusy.current) { window.history.pushState(null, '', '/session'); return; } setRoute(window.location.pathname); }; window.addEventListener('popstate', pop); return () => window.removeEventListener('popstate', pop); }, []);
  const go = useCallback((path: string) => { window.history.pushState(null, '', path); setRoute(path); }, []);
  const toToday = useCallback((learner: Learner) => { setRoute('/today'); onNavigateToToday?.(learner); }, [onNavigateToToday]);
  if (route === '/dev/components') return <Suspense fallback={<p role="status">Завантажуємо приклад…</p>}><ComponentPreview /></Suspense>;
  return <main className="app-shell">
    <header className="app-header"><a className="brand" href="/" onClick={event => { if (sessionBusy.current) event.preventDefault(); }} aria-label="Constant Learning — головна"><span className="brand-icon" aria-hidden="true">cl.</span><span>Constant<br />Learning</span></a><span className="eyebrow">ПОЛЬСЬКА ЩОДНЯ</span></header>
    {route === '/editor' ? <Suspense fallback={<div className="loading-state" role="status"><Spinner size="s" /> Відкриваємо редактор…</div>}><Editor onBack={() => go('/today')} /></Suspense> : <OnboardingGate onNavigateToToday={toToday} privacyContact={privacyContact} preserveRoute={route === '/settings' || route === '/dictionary' || route === '/session'}
      notOnboarded={route === '/settings' || route === '/dictionary' || route === '/session' ? <Card><h1>Почнімо знайомство</h1><p>Спершу налаштуйте тренер.</p><Button size="l" stretched onClick={() => go('/')}>До початку</Button></Card> : undefined}
      renderLearner={learner => route === '/session'
        ? <Suspense fallback={<div className="loading-state" role="status"><Spinner size="s" /> Відкриваємо заняття…</div>}><SessionRoute initialSession={openedSession} onBusy={value => { sessionBusy.current = value; }} onToday={() => { setOpenedSession(undefined); go('/today'); }} /></Suspense>
        : route === '/dictionary'
        ? <Suspense fallback={<div className="loading-state" role="status"><Spinner size="s" /> Відкриваємо словник…</div>}><Dictionary onBack={() => go('/today')} onReportMissing={onReportMissing} /></Suspense>
        : route === '/settings'
        ? <Suspense fallback={<div className="loading-state" role="status"><Spinner size="s" /> Відкриваємо налаштування…</div>}><Settings learner={learner} onBack={() => go('/today')} onStart={() => go('/')} /></Suspense>
        : <><Today learner={learner} onOpenSession={value => { setOpenedSession(value); onOpenSession?.(value); go('/session'); }} onStart={() => go('/')} /><nav className="app-navigation" aria-label="Основна навігація"><Button mode="outline" size="l" stretched onClick={() => go('/dictionary')}>Словник</Button><Button mode="outline" size="l" stretched onClick={() => go('/settings')}>Налаштування</Button></nav></>} />}
    <footer>По слову ближче до впевненості.</footer>
  </main>;
}
