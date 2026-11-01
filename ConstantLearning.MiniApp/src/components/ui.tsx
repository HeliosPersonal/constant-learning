import { useEffect, useRef, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { Button, Spinner, Progress } from '@telegram-apps/telegram-ui';

export { Button, Spinner };

export function Card({ children, ...props }: React.HTMLAttributes<HTMLElement>) {
  return <section className="card" {...props}>{children}</section>;
}

export function Result({ tone, children }: { tone: 'success' | 'error'; children: ReactNode }) {
  return <p className={`result result--${tone}`} role={tone === 'error' ? 'alert' : 'status'}>
    <span aria-hidden="true">{tone === 'success' ? '✓' : '!'}</span>{children}
  </p>;
}

export function LearningProgress({ value, total }: { value: number; total: number }) {
  return <div className="learning-progress"><span>Картка {value} із {total}</span>
    <Progress value={value / total * 100} aria-label="Прогрес заняття" /></div>;
}

export function Dialog({ open, onClose, children }: { open: boolean; onClose: () => void; children: ReactNode }) {
  const ref = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    const dialog = ref.current;
    if (open && dialog && !dialog.open) dialog.showModal();
    else if (!open && dialog?.open) dialog.close();
  }, [open]);
  return createPortal(<dialog ref={ref} aria-labelledby="dialog-title" className="dialog" onCancel={onClose}>
    <h2 id="dialog-title">Зручний для вас вигляд</h2>
    {children}
    <Button size="l" stretched onClick={onClose}>Зрозуміло</Button>
  </dialog>, document.body);
}
