import tokens from '../../../docs/features/personal-trainer-mvp/design/theme-tokens.json';

export type Theme = 'light' | 'dark';
export interface TelegramWebApp {
  colorScheme?: string;
  initData?: string;
  version?: string;
  isVersionAtLeast?: (version: string) => boolean;
  setBackgroundColor?: (color: string) => void;
  setHeaderColor?: (color: string) => void;
  onEvent?: (event: 'themeChanged', listener: () => void) => void;
  offEvent?: (event: 'themeChanged', listener: () => void) => void;
  ready?: () => void;
  BackButton?: { show: () => void; hide: () => void; onClick: (listener: () => void) => void; offClick: (listener: () => void) => void };
}

declare global {
  interface Window { Telegram?: { WebApp?: TelegramWebApp } }
}

export const getTelegram = () => window.Telegram?.WebApp;
export const isTheme = (value: unknown): value is Theme => value === 'light' || value === 'dark';

export function supports(app: TelegramWebApp, minimum: string): boolean {
  try {
    if (app.isVersionAtLeast) return app.isVersionAtLeast(minimum);
    if (!app.version || !/^\d+\.\d+(\.\d+)?$/.test(app.version)) return false;
    const [major, minor] = app.version.split('.').map(Number);
    const [requiredMajor, requiredMinor] = minimum.split('.').map(Number);
    return major > requiredMajor || (major === requiredMajor && minor >= requiredMinor);
  } catch { return false; }
}

export function syncTelegramChrome(theme: Theme, app = getTelegram()): void {
  if (!app || !supports(app, '6.1')) return;
  try { app.setBackgroundColor?.(tokens.themes[theme].background); } catch { /* Старий клієнт зберігає власний фон оболонки. */ }
  try {
    app.setHeaderColor?.(supports(app, '6.9') ? tokens.themes[theme].background : 'bg_color');
  } catch { /* Недоступне API оболонки не блокує локальну тему. */ }
}
