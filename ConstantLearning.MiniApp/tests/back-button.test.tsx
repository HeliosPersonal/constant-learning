import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import type { ReactElement } from 'react';
import { BackButton } from '../src/components/BackButton';
import { ThemeProvider } from '../src/theme/ThemeProvider';

const mount = (ui: ReactElement) => render(<ThemeProvider>{ui}</ThemeProvider>);

function telegramBackButton() {
  let listener: (() => void) | undefined;
  const native = { show: vi.fn(), hide: vi.fn(), onClick: vi.fn((value: () => void) => { listener = value; }), offClick: vi.fn() };
  window.Telegram = { WebApp: { version: '8.0', BackButton: native } };
  return { native, click: () => listener?.() };
}

describe('Кнопка «Назад»', () => {
  afterEach(() => { delete window.Telegram; });

  it('поза Telegram показує посилання на сторінці', () => {
    const onBack = vi.fn();
    mount(<BackButton onBack={onBack} navLabel="Навігація">← На головну</BackButton>);
    screen.getByRole('button', { name: '← На головну' }).click();
    expect(onBack).toHaveBeenCalledOnce();
  });

  it('у Telegram використовує нативну кнопку й ховає її після виходу з екрана', () => {
    const { native, click } = telegramBackButton();
    const onBack = vi.fn();
    const { unmount } = mount(<BackButton onBack={onBack}>← На головну</BackButton>);
    expect(screen.queryByRole('button')).toBeNull();
    expect(native.show).toHaveBeenCalledOnce();
    click(); expect(onBack).toHaveBeenCalledOnce();
    unmount();
    expect(native.offClick).toHaveBeenCalledOnce(); expect(native.hide).toHaveBeenCalledOnce();
  });

  it('ігнорує натискання, поки дія заблокована', () => {
    const { click } = telegramBackButton();
    const onBack = vi.fn();
    mount(<BackButton onBack={onBack} disabled>← На головну</BackButton>);
    click(); expect(onBack).not.toHaveBeenCalled();
  });
});
