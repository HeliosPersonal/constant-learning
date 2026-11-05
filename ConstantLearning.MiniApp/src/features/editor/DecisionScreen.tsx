import { useEffect, useRef, useState, type FormEvent } from 'react';
import { BackButton } from '../../components/BackButton';
import type { QueueItem } from './api';
import { initialDraft, queueField, rejectionReasons, validateDecision, type DecisionDraft, type DecisionErrors } from './model';

/** Окремий екран рішення щодо повідомлення: контекст, вибір «схвалити / відхилити», поле значення й одна головна дія. */
export function DecisionScreen({ item, draft = initialDraft(item), pending, error, stale, onDraft, onSubmit, onClose }: {
  item: QueueItem; draft?: DecisionDraft; pending: boolean; error?: string; stale: boolean;
  onDraft: (value: DecisionDraft) => void; onSubmit: (value: DecisionDraft) => void; onClose: () => void;
}) {
  const [errors, setErrors] = useState<DecisionErrors>({});
  const headingRef = useRef<HTMLHeadingElement>(null);
  const messageRef = useRef<HTMLDivElement>(null);
  useEffect(() => { headingRef.current?.focus(); window.scrollTo?.(0, 0); }, []);
  useEffect(() => { if (error) { const frame = requestAnimationFrame(() => { messageRef.current?.focus({ preventScroll: true }); messageRef.current?.scrollIntoView?.({ block: 'start' }); }); return () => cancelAnimationFrame(frame); } }, [error]);
  const missing = item.origin === 'missing_word';
  const rejecting = draft.decision === 'reject';
  const positive = missing ? 'add_to_candidates' : 'approve';
  const max = item.field === 'example' ? 500 : 200;
  const reasons = rejectionReasons(item.origin);
  const submit = (event: FormEvent) => {
    event.preventDefault(); if (pending || stale) return;
    const next = validateDecision(item, draft); setErrors(next);
    if (Object.keys(next).length) { requestAnimationFrame(() => { messageRef.current?.focus({ preventScroll: true }); messageRef.current?.scrollIntoView?.({ block: 'start' }); }); return; }
    onSubmit(draft);
  };
  const choose = (decision: DecisionDraft['decision']) => { setErrors({}); onDraft({ ...draft, decision }); };
  return <section className="editor-screen" aria-labelledby="editor-screen-title">
    <BackButton className="editor-navigation" navLabel="Навігація рішення" disabled={pending} onBack={onClose}>← До черги</BackButton>
    <h1 className="ios-title" id="editor-screen-title" ref={headingRef} tabIndex={-1}>{missing ? 'Розглянути відсутнє слово' : 'Розглянути повідомлення'}</h1>
    <p className="ios-caption editor-context">{item.lemma ? <span lang={item.target_language}>{item.lemma}</span> : 'Новий кандидат'} · {queueField(item)}</p>
    <p className="ios-footnote editor-author">Автор: <strong>{item.reporter_alias}</strong></p>
    <dl className="ios-group editor-values">
      {missing ? <div className="ios-row"><dt>Пошуковий запит</dt><dd className="ios-value">{item.search_query ?? 'Не вказано'}</dd></div> : <>
        <div className="ios-row"><dt>{item.origin === 'answer_dispute' ? 'Поточний варіант' : 'Поточне значення'}</dt><dd className="ios-value">{item.current_value ?? (item.origin === 'answer_dispute' ? 'Окремий варіант ще не додано' : 'Не вказано')}</dd></div>
        <div className="ios-row"><dt>Пропозиція</dt><dd className="ios-value">{item.proposed_value ?? 'Не вказано'}</dd></div></>}
      <div className="ios-row"><dt>Коментар</dt><dd className="ios-value">{item.comment ?? 'Без коментаря'}</dd></div>
    </dl>
    <form aria-label="Рішення щодо повідомлення" onSubmit={submit} noValidate>
      <fieldset disabled={pending || stale} className="editor-fields">
        <div className="ios-caption" id="editor-decision-label">Рішення</div>
        <div className="ios-segmented editor-decision" role="radiogroup" aria-labelledby="editor-decision-label">
          <label className={`ios-segment${!rejecting ? ' ios-segment--on' : ''}`}><input type="radio" name="editor-decision" checked={!rejecting} onChange={() => choose(positive)} />{missing ? 'Додати' : 'Схвалити'}</label>
          <label className={`ios-segment${rejecting ? ' ios-segment--on' : ''}`}><input type="radio" name="editor-decision" checked={rejecting} onChange={() => choose('reject')} />Відхилити</label>
        </div>
        {rejecting ? <>
          <div className="ios-caption">Причина відхилення</div>
          <div className="ios-group"><div className="ios-row ios-row--select"><span>Причина</span><span className="ios-value">{reasons.find(reason => reason.value === draft.reason)?.label ?? 'Оберіть'}<span className="ios-chevron" aria-hidden="true">›</span></span>
            <select aria-label="Причина відхилення" value={draft.reason} aria-invalid={!!errors.reason} aria-describedby={errors.reason ? 'editor-decision-result' : undefined} onChange={event => { setErrors({}); onDraft({ ...draft, reason: event.target.value as DecisionDraft['reason'] }); }}>
              <option value="">Оберіть причину</option>{reasons.map(reason => <option key={reason.value} value={reason.value}>{reason.label}</option>)}
            </select></div></div>
        </> : <>
          <label className="ios-caption" htmlFor="editor-corrected">{missing ? 'Польська лема' : 'Остаточне значення'} · необов’язково</label>
          <div className="ios-group"><textarea id="editor-corrected" className="editor-textarea" rows={item.field === 'example' ? 3 : 2} value={draft.corrected} aria-invalid={!!errors.corrected} aria-describedby="editor-corrected-hint editor-corrected-count editor-decision-result" onChange={event => { setErrors({}); onDraft({ ...draft, corrected: event.target.value }); }} /></div>
          <p className="ios-footnote" id="editor-corrected-hint">{missing ? 'Залиште порожнім, щоб використати пошуковий запит. Кандидат має бути однією польською лемою.' : 'Залиште порожнім, щоб використати пропозицію автора.'}</p>
          <p className="ios-footnote" id="editor-corrected-count">{draft.corrected.length} / {max} символів</p>
        </>}
      </fieldset>
      {missing && !rejecting && <p className="ios-footnote">Кандидат отримає статус чернетки. Він стане доступним для навчання після всіх перевірок.</p>}
      {item.origin === 'answer_dispute' && !rejecting && <p className="ios-footnote">Прийнятий варіант застосовуватиметься надалі. Попередні відповіді не перераховуються.</p>}
      <div ref={messageRef} tabIndex={-1} id="editor-decision-result" className="editor-message" aria-live="polite">
        {Object.values(errors).map((value, index) => <p className="ios-footnote ios-footnote--error" role="alert" key={index}>{value}</p>)}
        {error && <p className="ios-footnote ios-footnote--error" role="alert">{error}</p>}
      </div>
      <button type="submit" className={`editor-primary ios-plain${rejecting ? ' editor-primary--danger' : ''}`} disabled={pending || stale}>{pending ? 'Зберігаємо рішення…' : rejecting ? 'Відхилити з причиною' : missing ? 'Додати до кандидатів' : 'Схвалити зміну'}</button>
      {pending && <p className="ios-footnote" role="status">Зберігаємо рішення…</p>}
    </form>
  </section>;
}
