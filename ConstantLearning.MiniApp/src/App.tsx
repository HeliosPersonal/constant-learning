import { lazy, Suspense, useCallback, useEffect, useRef, useState } from 'react';
import { Button, Card, Spinner } from './components/ui';
import { OnboardingGate } from './features/onboarding/OnboardingGate';
import { Today } from './features/today/Today';
import type { SessionResponse } from './features/today/api';
import type { Learner } from './features/onboarding/api';
import type { PrivacyContact } from './features/onboarding/privacy';
import type { MissingWordReportRequest } from './features/dictionary/search/api';

const ComponentPreview = lazy(() => import('./components/ComponentPreview').then(module => ({ default: module.ComponentPreview })));
const SettingsScreen = lazy(() => import('./features/settings/SettingsScreen'));
const Dictionary = lazy(() => import('./features/dictionary/search/Dictionary'));
const Stats = lazy(() => import('./features/stats/Stats'));
const SessionRoute = lazy(() => import('./features/session/shell/SessionRoute'));
const Editor = lazy(() => import('./features/editor/Editor'));
const navigation = [['/stats', 'Мої слова'], ['/dictionary', 'Словник'], ['/settings', 'Налаштування']] as const;

export function App({ onNavigateToToday, privacyContact, onReportMissing, onOpenSession }: { onNavigateToToday?: (learner: Learner) => void; privacyContact?: PrivacyContact | null; onReportMissing?: (request: MissingWordReportRequest) => void; onOpenSession?: (session: SessionResponse) => void }) {
  const [route, setRoute] = useState(window.location.pathname);
  const [openedSession, setOpenedSession] = useState<SessionResponse>();
  const [todayNotice, setTodayNotice] = useState<string>();
  const sessionBusy = useRef(false);
  useEffect(() => { const pop = () => { if (sessionBusy.current) { window.history.pushState(null, '', '/session'); return; } setRoute(window.location.pathname); }; window.addEventListener('popstate', pop); return () => window.removeEventListener('popstate', pop); }, []);
  const go = useCallback((path: string) => { window.history.pushState(null, '', path); setRoute(path); }, []);
  const toToday = useCallback((learner: Learner) => { setRoute('/today'); onNavigateToToday?.(learner); }, [onNavigateToToday]);
  if (route === '/dev/components') return <Suspense fallback={<p role="status">Завантажуємо приклад…</p>}><ComponentPreview /></Suspense>;
  return <main className="app-shell">
    {route === '/editor' ? <Suspense fallback={<div className="loading-state" role="status"><Spinner size="s" /> Відкриваємо редактор…</div>}><Editor onBack={() => go('/today')} /></Suspense> : <OnboardingGate onNavigateToToday={toToday} privacyContact={privacyContact} preserveRoute={route === '/settings' || route === '/dictionary' || route === '/session' || route === '/stats'}
      notOnboarded={route === '/settings' || route === '/dictionary' || route === '/session' || route === '/stats' ? <Card><h1>Почнімо знайомство</h1><p>Спершу налаштуйте тренер.</p><Button size="l" stretched onClick={() => go('/')}>До початку</Button></Card> : undefined}
      renderLearner={learner => route === '/session'
        ? <Suspense fallback={<div className="loading-state" role="status"><Spinner size="s" /> Відкриваємо заняття…</div>}><SessionRoute initialSession={openedSession} onBusy={value => { sessionBusy.current = value; }} onToday={notice => { setOpenedSession(undefined); setTodayNotice(notice); go('/today'); }} /></Suspense>
        : route === '/dictionary'
        ? <Suspense fallback={<div className="loading-state" role="status"><Spinner size="s" /> Відкриваємо словник…</div>}><Dictionary key={learner.active_target_language} learner={learner} onBack={() => go('/today')} onReportMissing={onReportMissing} /></Suspense>
        : route === '/stats'
        ? <Suspense fallback={<div className="loading-state" role="status"><Spinner size="s" /> Відкриваємо ваші слова…</div>}><Stats key={learner.active_target_language} learner={learner} onBack={() => go('/today')} onOpenSession={value => { setOpenedSession(value); onOpenSession?.(value); go('/session'); }} /></Suspense>
        : route === '/settings'
        ? <Suspense fallback={<div className="loading-state" role="status"><Spinner size="s" /> Відкриваємо налаштування…</div>}><SettingsScreen learner={learner} onBack={notice => { setTodayNotice(notice); go('/today'); }} onStart={() => go('/')} onOpenEditor={() => go('/editor')} /></Suspense>
        : <><Today learner={learner} notice={todayNotice} onNoticeShown={() => setTodayNotice(undefined)} onOpenSession={value => { setOpenedSession(value); onOpenSession?.(value); go('/session'); }} onStart={() => go('/')} /><nav className="ios-group" aria-label="Основна навігація">{navigation.map(([path, label]) => <button key={path} type="button" className="ios-row ios-plain" onClick={() => go(path)}>{label}<span className="ios-value ios-chevron" aria-hidden="true">›</span></button>)}</nav></>} />}
  </main>;
}
