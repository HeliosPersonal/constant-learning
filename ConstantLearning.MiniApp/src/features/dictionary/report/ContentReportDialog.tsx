import { useEffect, useRef, useState, type FormEvent } from 'react';
import { createPortal } from 'react-dom';
import { Button, Result } from '../../../components/ui';
import { ContentReportApiError, createContentReport } from './api';
import { reportBody, reportFields, textLength, validateReport, type ReportContext, type ReportDraft, type ReportErrors, type ReportField } from './model';
import './report.css';

export default function ContentReportDialog({ open, context, onClose }: { open: boolean; context: ReportContext; onClose: () => void }) {
  const [draft, setDraft] = useState<ReportDraft>({ field: '', proposed: '', comment: '' });
  const [errors, setErrors] = useState<ReportErrors>({});
  const [pending, setPending] = useState(false);
  const [accepted, setAccepted] = useState(false);
  const [blocked, setBlocked] = useState(false);
  const submitting = useRef(false);
  const dialogRef = useRef<HTMLDialogElement>(null);
  const toolbarRef = useRef<HTMLDivElement>(null);
  const headingRef = useRef<HTMLHeadingElement>(null);
  const messageRef = useRef<HTMLDivElement>(null);
  const mounted = useRef(true);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
  useEffect(() => {
    const dialog = dialogRef.current;
    if (open && dialog && !dialog.open) { setAccepted(false); dialog.showModal(); headingRef.current?.focus(); }
    else if (!open && dialog?.open) dialog.close();
  }, [open]);
  function focusMessage() {
    requestAnimationFrame(() => {
      const message = messageRef.current;
      if (!message) return;
      const dialogPadding = dialogRef.current ? parseFloat(getComputedStyle(dialogRef.current).paddingTop) : 0;
      message.style.scrollMarginTop = `${(toolbarRef.current?.getBoundingClientRect().height ?? 52) + dialogPadding + 16}px`;
      message.focus({ preventScroll: true });
      message.scrollIntoView?.({ block: 'start', behavior: 'auto' });
    });
  }
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (submitting.current || blocked || accepted) return;
    const validation = validateReport(context, draft); setErrors(validation);
    if (Object.keys(validation).length) { focusMessage(); return; }
    submitting.current = true; setPending(true);
    try {
      await createContentReport(reportBody(context, draft));
      if (mounted.current) { setAccepted(true); setErrors({}); focusMessage(); }
    } catch (error) {
      if (!mounted.current) return;
      let general = 'Не вдалося підтвердити надсилання. Ваше введення збережено. Спробуйте ще раз.';
      if (error instanceof ContentReportApiError) {
        if (error.status === 429) general = 'Сьогоднішній ліміт 20 повідомлень вичерпано. Нові можна надіслати після 00:00 за вашим часом. Ваше введення збережено.';
        else if (error.status === 401) { general = 'Відкрийте тренер знову в Telegram, щоб надіслати повідомлення. Ваше введення збережено.'; setBlocked(true); }
        else if (error.status === 404) { general = error.body?.code === 'learner.not_onboarded' ? 'Спершу налаштуйте тренер. Ваше введення збережено.' : 'Цей запис більше недоступний у словнику. Поверніться до результатів і оновіть пошук. Ваше введення збережено.'; setBlocked(true); }
        else if (error.status === 400) general = 'Перевірте вибране поле, запропоноване значення й коментар. Ваше введення збережено.';
      }
      setErrors({ general }); focusMessage();
    } finally {
      submitting.current = false;
      if (mounted.current) setPending(false);
    }
  }
  const query = context.origin === 'missing_word' ? context.search_query : null;
  return createPortal(<dialog className="dialog content-report-dialog" aria-labelledby="content-report-title" ref={dialogRef} onCancel={event => { event.preventDefault(); if (!submitting.current) onClose(); }}>
    <div className="content-report-toolbar" ref={toolbarRef}><p className="eyebrow">ДОПОМОЖІТЬ ПОКРАЩИТИ СЛОВНИК</p><Button mode="plain" size="l" aria-label="Закрити повідомлення" disabled={pending} onClick={onClose}>×</Button></div>
    <h2 id="content-report-title" ref={headingRef} tabIndex={-1}>{context.origin === 'card' ? 'Повідомити про неточність' : 'Повідомити про відсутній запис'}</h2>
    <p className="content-report-context">{context.origin === 'card' ? <span lang="pl">{context.entry.lemma_pl}</span> : <>Пошуковий запит: <span>«{query}»</span></>}</p>
    <div ref={messageRef} tabIndex={-1} className="content-report-message" aria-label="Результат повідомлення">
      {accepted ? <><Result tone="success">Дякуємо, передали на перевірку</Result><p className="caption">Зміни з’являться після підтвердження.</p></> : Object.values(errors).map((message, index) => <Result key={index} tone="error">{message}</Result>)}
    </div>
    {accepted ? <Button size="l" stretched onClick={onClose}>Повернутися до словника</Button> : <form aria-label="Повідомлення про словник" noValidate onSubmit={event => { void submit(event); }}>
      <p className="caption">Опишіть, що варто перевірити. Запропоноване значення й коментар можна залишити порожніми.</p>
      <fieldset className="content-report-fields" disabled={pending}>
        {context.origin === 'card' && <><label htmlFor="report-field">Що потрібно перевірити?</label><select id="report-field" value={draft.field} aria-required="true" aria-invalid={Boolean(errors.field)} aria-describedby={errors.field ? 'report-field-error' : undefined} onChange={event => setDraft(value => ({ ...value, field: event.target.value as ReportField }))}><option value="" disabled>Виберіть поле</option>{reportFields.map(field => <option key={field.value} value={field.value}>{field.label}</option>)}</select>{errors.field && <p className="sr-only" id="report-field-error">{errors.field}</p>}</>}
        <label htmlFor="report-proposed">Запропоноване значення <span className="caption">(необов’язково)</span></label>
        <textarea id="report-proposed" rows={3} value={draft.proposed} aria-invalid={Boolean(errors.proposed)} aria-describedby="report-proposed-count" onChange={event => setDraft(value => ({ ...value, proposed: event.target.value }))} />
        <p id="report-proposed-count" className={`caption content-report-count${errors.proposed ? ' content-report-count--error' : ''}`}>{textLength(draft.proposed)} / 500 символів{errors.proposed ? `. ${errors.proposed}` : ''}</p>
        <label htmlFor="report-comment">Коментар <span className="caption">(необов’язково)</span></label>
        <textarea id="report-comment" rows={4} value={draft.comment} aria-invalid={Boolean(errors.comment)} aria-describedby="report-comment-count report-plain-text" onChange={event => setDraft(value => ({ ...value, comment: event.target.value }))} />
        <p id="report-comment-count" className={`caption content-report-count${errors.comment ? ' content-report-count--error' : ''}`}>{textLength(draft.comment)} / 1000 символів{errors.comment ? `. ${errors.comment}` : ''}</p>
        <p id="report-plain-text" className="caption">Коментар зберігається як звичайний текст.</p>
      </fieldset>
      <p className="caption">До 20 повідомлень на день за вашим часом. Слово й навчальний прогрес не змінюються від надсилання.</p>
      <div className="content-report-actions"><Button type="submit" size="l" stretched disabled={pending || blocked}>{pending ? 'Надсилаємо повідомлення…' : 'Надіслати на перевірку'}</Button><Button mode="outline" size="l" stretched disabled={pending} onClick={onClose}>Назад до словника</Button></div>
      {pending && <p className="caption content-report-pending" role="status">Чекаємо підтвердження. Будь ласка, залишайте форму відкритою.</p>}
    </form>}
  </dialog>, document.body);
}
