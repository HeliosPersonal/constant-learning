import { useEffect, useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Button, Result } from '../../../components/ui';
import type { components } from '../../../api/schema';
import { todayQueryKey, type SessionResponse } from '../../today/api';
import { SessionApiError } from '../shell/api';
import { answer, continueResult, dispute } from './api';
import './recall.css';

type Draft = { sessionId: string; itemId: string; body: components['schemas']['AnswerRequest'] };
const storageKey = 'constant-learning.pending-answer.v1';
function readDraft(): Draft | null { try { return JSON.parse(sessionStorage.getItem(storageKey) ?? 'null') as Draft | null; } catch { return null; } }
function saveDraft(value: Draft | null) { try { if (value) sessionStorage.setItem(storageKey, JSON.stringify(value)); else sessionStorage.removeItem(storageKey); } catch { /* Стан зберігається в пам’яті поточного екрана. */ } }
const outcomes = { correct: 'Правильно!', mistake_typo: 'Цього разу є помилка.', mistake_diacritic: 'Зверніть увагу на діакритичний знак.', dont_know: 'Вивчимо це слово разом.', synonym: 'Це теж правильне слово, але тут вивчаємо інше. Відповідь нейтральна.' };
const genders: Record<string, string> = { m: 'чоловічий рід', f: 'жіночий рід', n: 'середній рід', pl: 'множина' };
type Props = { session: SessionResponse; refresh: () => Promise<SessionResponse>; replace: (value: SessionResponse) => void; onBusy: (value: boolean) => void; onTerminal: (value: 'expired' | 'missing' | 'auth') => void };
export function Recall({ session, refresh, replace, onBusy, onTerminal }: Props) {
  const client = useQueryClient();
  const item = session.current_item;
  const result = session.last_result;
  const [draft, setDraft] = useState<Draft | null>(() => { const saved = readDraft(); return saved?.sessionId === session.id && saved.itemId === session.current_item?.id && !session.last_result ? saved : null; });
  const [text, setText] = useState(() => draft?.body.answer ?? '');
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string>();
  const [needsRecovery, setNeedsRecovery] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const lock = useRef(false);
  useEffect(() => { if (result) { saveDraft(null); } }, [result]);
  const begin = () => { if (lock.current) return false; lock.current = true; onBusy(true); setPending(true); setError(undefined); return true; };
  const end = () => { lock.current = false; setPending(false); onBusy(false); };
  const terminal = (e: unknown) => { if (!(e instanceof SessionApiError)) return false; if (e.code === 'session.expired') onTerminal('expired'); else if (e.status === 401) onTerminal('auth'); else if (e.status === 404) onTerminal('missing'); else return false; return true; };
  async function recover() {
    try {
      const value = await refresh();
      if (value.last_result || value.current_item?.id !== draft?.itemId) { saveDraft(null); setDraft(null); }
      setNeedsRecovery(false);
      return true;
    } catch (e) { if (!terminal(e)) { setNeedsRecovery(true); setError('Не вдалося перевірити стан заняття. Спробуйте оновити його ще раз.'); } return false; }
  }
  async function submit(value: Draft) {
    if (!begin()) return;
    setDraft(value); saveDraft(value);
    try {
      await client.cancelQueries({ queryKey: ['session', 'current'] });
      await answer(value.sessionId, value.itemId, value.body);
      const current = await refresh();
      if (current.last_result || current.current_item?.id !== value.itemId) { saveDraft(null); setDraft(null); }
      else { setNeedsRecovery(false); setError('Стан заняття оновлено. Повторіть ту саму відповідь.'); }
      void client.invalidateQueries({ queryKey: todayQueryKey });
    } catch (e) {
      if (e instanceof SessionApiError && e.status === 400) { saveDraft(null); setDraft(null); setError(e.code === 'answer.wrong_layout' ? 'Перемкніть розкладку на польську або натисніть «Не знаю».' : 'Перевірте відповідь і спробуйте ще раз.'); }
      else if (!terminal(e)) {
        try {
          const current = await refresh();
          void client.invalidateQueries({ queryKey: todayQueryKey });
          if (current.last_result || current.current_item?.id !== value.itemId) { saveDraft(null); setDraft(null); setNeedsRecovery(false); }
          else { setNeedsRecovery(false); setError('Відповідь ще не підтверджено. Можна повторити саме цю відповідь.'); }
        } catch (recoveryError) { if (!terminal(recoveryError)) { setNeedsRecovery(true); setError('Не вдалося перевірити стан відповіді. Оновіть стан перед повтором.'); } }
      }
    } finally { end(); }
  }
  async function runRecovery() { if (!begin()) return; try { await recover(); } finally { end(); } }
  async function command(kind: 'continue' | 'dispute') {
    if (!result || !begin()) return;
    try {
      await client.cancelQueries({ queryKey: ['session', 'current'] });
      if (kind === 'continue') { const value = await continueResult(session.id, result.item_id); replace(value); void client.invalidateQueries({ queryKey: todayQueryKey }); }
      else { const neutralized = await dispute(result.answer.answer_event_id); replace({ ...session, last_result: { ...result, dispute: neutralized } }); setConfirming(false); void client.invalidateQueries({ queryKey: todayQueryKey }); await refresh(); }
    } catch (e) {
      if (!terminal(e)) {
        try { await refresh(); void client.invalidateQueries({ queryKey: todayQueryKey }); setConfirming(false); setError('Стан заняття оновлено. Перевірте результат і за потреби повторіть дію.'); }
        catch (recoveryError) { if (kind === 'continue' && (!(e instanceof SessionApiError) || e.status >= 500) && recoveryError instanceof SessionApiError && recoveryError.status === 404) { setError('Не вдалося підтвердити перехід. Повторіть продовження.'); } else if (!terminal(recoveryError)) { setNeedsRecovery(true); setError('Не вдалося оновити стан заняття. Спробуйте ще раз.'); } }
      }
    } finally { end(); }
  }
  if (result) {
    const a = result.answer; const neutral = result.dispute;
    return <><p className="eyebrow">РЕЗУЛЬТАТ</p><h1>{neutral ? 'Відповідь нейтралізовано' : outcomes[a.outcome]}</h1>
      {neutral ? <p role="status">{neutral.message}</p> : <>{a.diff.length > 0 && <div className="recall-diff" aria-label="Порівняння відповіді"><span>Ваша відповідь: </span><span lang="pl">{a.diff.map((part, i) => <span key={i} className={`recall-diff--${part.op}`}>{part.actual}</span>)}</span><br /><span>Правильна форма: </span><span lang="pl">{a.diff.map((part, i) => <span key={i} className={`recall-diff--${part.op}`}>{part.expected}</span>)}</span></div>}</>}
      <p className="recall-correct" lang="pl">{a.correct_form}</p><p className="caption">{a.pronunciation_uk}{a.gender && genders[a.gender] ? ` · ${genders[a.gender]}` : ''}</p>
      <div className="recall-example"><p lang="pl">{a.example_pl}</p><p>{a.example_uk}</p></div>
      {error && <Result tone="error">{error}</Result>}
      {needsRecovery ? <Button size="l" stretched disabled={pending} onClick={() => { void runRecovery(); }}>Оновити стан</Button> : <><Button size="l" stretched disabled={pending} onClick={() => { void command('continue'); }}>Продовжити</Button>
      {!neutral && a.dispute_allowed && (confirming ? <div className="recall-actions"><p>Передати відповідь на перевірку та нейтралізувати результат?</p><Button size="l" stretched disabled={pending} onClick={() => { void command('dispute'); }}>Підтвердити</Button><Button size="l" mode="plain" stretched disabled={pending} onClick={() => setConfirming(false)}>Скасувати</Button></div> : <div className="recall-dispute"><Button size="l" mode="outline" stretched disabled={pending} onClick={() => setConfirming(true)}>Моя відповідь теж правильна</Button></div>)}</>}
      {pending && <p role="status">Зберігаємо результат…</p>}</>;
  }
  if (!item) return null;
  return <><p className="eyebrow">ПРИГАДАЙТЕ ПОЛЬСЬКОЮ</p><h1>{item.prompt_uk}</h1>{item.context_uk && <p className="caption">{item.context_uk}</p>}
    <form onSubmit={event => { event.preventDefault(); if (!draft && text.trim()) void submit({ sessionId: session.id, itemId: item.id, body: { answer: text, dont_know: false } }); }}>
      <label className="recall-label" htmlFor="recall-answer">Ваша відповідь</label><input id="recall-answer" className="recall-input" lang="pl" autoComplete="off" maxLength={200} value={text} disabled={pending || !!draft} onChange={event => setText(event.target.value)} />
      {error && <Result tone="error">{error}</Result>}
      <div className="recall-actions">{draft ? <Button size="l" stretched disabled={pending} onClick={() => { if (needsRecovery) void runRecovery(); else void submit(draft); }}>{needsRecovery ? 'Оновити стан' : 'Повторити відповідь'}</Button> : <><Button type="submit" size="l" stretched disabled={pending || !text.trim()}>Перевірити</Button><Button type="button" size="l" mode="outline" stretched disabled={pending} onClick={() => { void submit({ sessionId: session.id, itemId: item.id, body: { answer: null, dont_know: true } }); }}>Не знаю</Button></>}</div>
      {pending && <p role="status">Перевіряємо відповідь…</p>}
    </form></>;
}
