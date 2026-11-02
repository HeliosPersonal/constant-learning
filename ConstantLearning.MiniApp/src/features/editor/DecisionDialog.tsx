import { useEffect, useRef, useState, type FormEvent } from 'react';
import { Button, Result } from '../../components/ui';
import { EditorDialog } from './EditorDialog';
import type { QueueItem } from './api';
import { initialDraft, queueField, rejectionReasons, validateDecision, type DecisionDraft, type DecisionErrors } from './model';

export function DecisionDialog({ item, draft = initialDraft(item), pending, error, stale, onDraft, onSubmit, onClose }: {
  item: QueueItem; draft?: DecisionDraft; pending: boolean; error?: string; stale: boolean;
  onDraft: (value: DecisionDraft) => void; onSubmit: (value: DecisionDraft) => void; onClose: () => void;
}) {
  const [errors, setErrors] = useState<DecisionErrors>({});
  const messageRef = useRef<HTMLDivElement>(null);
  useEffect(() => { if (error) { const frame = requestAnimationFrame(() => { messageRef.current?.focus({ preventScroll: true }); messageRef.current?.scrollIntoView?.({ block: 'start' }); }); return () => cancelAnimationFrame(frame); } }, [error]);
  const missing = item.origin === 'missing_word';
  const max = item.field === 'example' ? 500 : 200;
  const submit = (event: FormEvent) => {
    event.preventDefault(); if (pending || stale) return;
    const next = validateDecision(item, draft); setErrors(next);
    if (Object.keys(next).length) { requestAnimationFrame(() => { messageRef.current?.focus({ preventScroll: true }); messageRef.current?.scrollIntoView?.({ block: 'start' }); }); return; }
    onSubmit(draft);
  };
  return <EditorDialog title={missing ? 'Розглянути відсутнє слово' : 'Розглянути повідомлення'} pending={pending} onClose={onClose}>
    <p className="editor-context">{item.lemma_pl ? <span lang="pl">{item.lemma_pl}</span> : 'Новий кандидат'} · {queueField(item)}</p>
    <p className="caption">Автор: <strong>{item.reporter_alias}</strong></p>
    <dl className="editor-values">
      {missing ? <div><dt>Пошуковий запит</dt><dd>{item.search_query ?? 'Не вказано'}</dd></div> : <><div><dt>{item.origin === 'answer_dispute' ? 'Поточний варіант' : 'Поточне значення'}</dt><dd>{item.current_value ?? (item.origin === 'answer_dispute' ? 'Окремий варіант ще не додано' : 'Не вказано')}</dd></div><div><dt>Пропозиція</dt><dd>{item.proposed_value ?? 'Не вказано'}</dd></div></>}
      <div><dt>Коментар</dt><dd>{item.comment ?? 'Без коментаря'}</dd></div>
    </dl>
    <form aria-label="Рішення щодо повідомлення" onSubmit={submit} noValidate>
      <fieldset disabled={pending || stale} className="editor-fields">
        <label htmlFor="editor-decision">Рішення</label><select id="editor-decision" value={draft.decision} onChange={event => { setErrors({}); onDraft({ ...draft, decision: event.target.value as DecisionDraft['decision'] }); }}>
          <option value={missing ? 'add_to_candidates' : 'approve'}>{missing ? 'Додати до кандидатів' : 'Схвалити зміну'}</option><option value="reject">Відхилити</option>
        </select>
        {draft.decision === 'reject' ? <><label htmlFor="editor-reason">Причина відхилення</label><select id="editor-reason" value={draft.reason} aria-invalid={!!errors.reason} aria-describedby={errors.reason ? 'editor-decision-result' : undefined} onChange={event => { setErrors({}); onDraft({ ...draft, reason: event.target.value as DecisionDraft['reason'] }); }}><option value="">Оберіть причину</option>{rejectionReasons(item.origin).map(reason => <option key={reason.value} value={reason.value}>{reason.label}</option>)}</select></>
          : <><label htmlFor="editor-corrected">{missing ? 'Польська лема' : 'Остаточне значення'} <span className="caption">(необов’язково)</span></label><textarea id="editor-corrected" rows={item.field === 'example' ? 3 : 2} value={draft.corrected} aria-invalid={!!errors.corrected} aria-describedby="editor-corrected-hint editor-corrected-count editor-decision-result" onChange={event => { setErrors({}); onDraft({ ...draft, corrected: event.target.value }); }} /><p className="caption" id="editor-corrected-hint">{missing ? 'Залиште порожнім, щоб використати пошуковий запит. Кандидат має бути однією польською лемою.' : 'Залиште порожнім, щоб використати пропозицію автора.'}</p><p className="caption" id="editor-corrected-count">{draft.corrected.length} / {max} символів</p></>}
      </fieldset>
      {missing && draft.decision !== 'reject' && <p className="caption">Кандидат отримає статус чернетки. Він стане доступним для навчання після всіх перевірок.</p>}
      {item.origin === 'answer_dispute' && draft.decision === 'approve' && <p className="caption">Прийнятий варіант застосовуватиметься надалі. Попередні відповіді не перераховуються.</p>}
      <div ref={messageRef} tabIndex={-1} id="editor-decision-result" className="editor-message" aria-live="polite">{Object.values(errors).map((value, index) => <Result tone="error" key={index}>{value}</Result>)}{error && <Result tone="error">{error}</Result>}</div>
      <div className="editor-actions"><Button type="submit" size="l" stretched disabled={pending || stale}>{pending ? 'Зберігаємо рішення…' : draft.decision === 'reject' ? 'Відхилити з причиною' : missing ? 'Додати до кандидатів' : 'Схвалити зміну'}</Button><Button size="l" stretched mode="outline" disabled={pending} onClick={onClose}>Повернутися до черги</Button></div>
      {pending && <p className="caption editor-pending" role="status">Зберігаємо рішення…</p>}
    </form>
  </EditorDialog>;
}
