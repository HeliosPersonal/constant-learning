import { useEffect, useRef, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { Button } from '../../components/ui';

export function EditorDialog({ title, pending, onClose, children }: { title: string; pending: boolean; onClose: () => void; children: ReactNode }) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const headingRef = useRef<HTMLHeadingElement>(null);
  const closeRef = useRef(onClose);
  closeRef.current = onClose;
  useEffect(() => {
    const previous = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const dialog = dialogRef.current;
    dialog?.showModal(); headingRef.current?.focus();
    return () => { dialog?.close(); if (previous?.isConnected && !previous.matches(':disabled')) previous.focus(); else document.getElementById('editor-view-title')?.focus(); };
  }, []);
  return createPortal(<dialog ref={dialogRef} className="dialog editor-dialog" aria-labelledby="editor-dialog-title" aria-busy={pending} onCancel={event => { event.preventDefault(); if (!pending) closeRef.current(); }} onKeyDown={event => {
    if (event.key !== 'Tab') return;
    const controls = [...event.currentTarget.querySelectorAll<HTMLElement>('button:not(:disabled), a[href], input:not(:disabled), select:not(:disabled), textarea:not(:disabled), [tabindex]:not([tabindex="-1"])')].filter(control => control.getClientRects().length > 0);
    const first = controls[0], last = controls.at(-1);
    if (!first) { event.preventDefault(); headingRef.current?.focus(); }
    else if (event.shiftKey && (document.activeElement === first || document.activeElement === headingRef.current)) { event.preventDefault(); last?.focus(); }
    else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
  }}>
    <div className="editor-dialog-toolbar"><p className="eyebrow">ПЕРЕВІРКА СЛОВНИКА</p><Button mode="plain" size="m" aria-label="Закрити" disabled={pending} onClick={onClose}>×</Button></div>
    <h2 ref={headingRef} id="editor-dialog-title" tabIndex={-1}>{title}</h2>{children}
  </dialog>, document.body);
}
