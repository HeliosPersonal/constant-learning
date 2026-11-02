import { getTelegram, isTheme, syncTelegramChrome, type TelegramWebApp, type Theme } from './telegram';
import { readInitialTheme } from './initial';

export const THEME_KEY = 'constant-learning.theme.v1';
export interface ThemeEnvironment {
  storage: () => Pick<Storage, 'getItem' | 'setItem'>;
  telegram: () => TelegramWebApp | undefined;
  media: () => MediaQueryList | undefined;
  apply: (theme: Theme) => void;
}

export function applyTheme(theme: Theme): void {
  document.documentElement.dataset.theme = theme;
  document.documentElement.style.colorScheme = theme;
  document.querySelector('meta[name="theme-color"]')?.setAttribute('content',
    getComputedStyle(document.documentElement).getPropertyValue('--cl-background').trim());
  syncTelegramChrome(theme);
}

export function createThemeStore(environment: ThemeEnvironment) {
  const initial = readInitialTheme(environment);
  let manual = initial.manual;
  const systemTheme = (): Theme => {
    try { return environment.media()?.matches ? 'dark' : 'light'; } catch { return 'light'; }
  };
  const automaticTheme = (): Theme => {
    const telegram = environment.telegram()?.colorScheme;
    return isTheme(telegram) ? telegram : systemTheme();
  };
  let current = initial.theme;
  const listeners = new Set<() => void>();
  const update = (theme: Theme) => {
    current = theme;
    environment.apply(theme);
    listeners.forEach(listener => listener());
  };
  environment.apply(current);
  return {
    getSnapshot: () => current,
    subscribe(listener: () => void) { listeners.add(listener); return () => { listeners.delete(listener); }; },
    setTheme(theme: Theme) {
      manual = theme;
      try { environment.storage().setItem(THEME_KEY, theme); } catch { /* Перемикання працює без сховища. */ }
      update(theme);
    },
    connect() {
      const app = environment.telegram();
      let media: MediaQueryList | undefined;
      try { media = environment.media(); } catch { /* ОС не повідомляє свою схему. */ }
      const automaticChanged = () => { if (!manual) update(automaticTheme()); };
      app?.onEvent?.('themeChanged', automaticChanged);
      media?.addEventListener?.('change', automaticChanged);
      automaticChanged();
      return () => {
        app?.offEvent?.('themeChanged', automaticChanged);
        media?.removeEventListener?.('change', automaticChanged);
      };
    },
  };
}

export const themeStore = createThemeStore({
  storage: () => window.localStorage,
  telegram: getTelegram,
  media: () => window.matchMedia?.('(prefers-color-scheme: dark)'),
  apply: applyTheme,
});
