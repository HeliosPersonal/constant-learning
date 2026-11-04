import { useEffect, useRef, useState, type ReactNode } from 'react';
import { Button } from '@telegram-apps/telegram-ui';
import { getTelegram, supports } from '../theme/telegram';

type Entry = { current: { onBack: () => void; disabled: boolean } };
// Активний останній змонтований обробник: вкладений екран перекриває батьківський.
const stack: Entry[] = [];
const dispatch = () => { const top = stack.at(-1)?.current; if (top && !top.disabled) top.onBack(); };

function nativeBackButton() {
  const app = getTelegram();
  return app?.BackButton && supports(app, '6.1') ? app.BackButton : undefined;
}

/** Нативна кнопка «Назад» у шапці Telegram; поза Telegram — текстове посилання на сторінці. */
export function BackButton({ onBack, disabled = false, navLabel, className, children }: { onBack: () => void; disabled?: boolean; navLabel?: string; className?: string; children: ReactNode }) {
  const [native] = useState(nativeBackButton);
  const entry = useRef({ onBack, disabled });
  useEffect(() => { entry.current = { onBack, disabled }; });
  useEffect(() => {
    if (!native) return;
    if (!stack.length) { native.onClick(dispatch); native.show(); }
    stack.push(entry);
    return () => {
      stack.splice(stack.indexOf(entry), 1);
      if (!stack.length) { native.offClick(dispatch); native.hide(); }
    };
  }, [native]);
  if (native) return null;
  const button = <Button className={navLabel ? undefined : className} mode="plain" size="l" disabled={disabled} onClick={onBack}>{children}</Button>;
  return navLabel ? <nav className={className} aria-label={navLabel}>{button}</nav> : button;
}
