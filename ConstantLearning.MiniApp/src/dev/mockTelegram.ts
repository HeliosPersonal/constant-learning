import { mockTelegramEnv } from '@tma.js/sdk';
import tokens from '../../../docs/features/personal-trainer-mvp/design/theme-tokens.json';
import { themeStore } from '../theme/store';
import type { TelegramWebApp } from '../theme/telegram';

export function setupMockTelegram(): void {
  const theme = themeStore.getSnapshot();
  const colors = tokens.themes[theme];
  mockTelegramEnv({
    launchParams: {
      tgWebAppVersion: '8.0', tgWebAppPlatform: 'tdesktop',
      tgWebAppThemeParams: { bg_color: colors.background as `#${string}`, text_color: colors.text as `#${string}`, button_color: colors.accent as `#${string}`, button_text_color: colors.onAccent as `#${string}` },
    },
    onEvent: () => { /* Локальний mock не пересилає виклики клієнтові Telegram. */ },
  });
  const listeners = new Set<() => void>();
  const app: TelegramWebApp = {
    colorScheme: theme,
    version: '8.0',
    onEvent: (_event, listener) => { listeners.add(listener); },
    offEvent: (_event, listener) => { listeners.delete(listener); },
    setBackgroundColor: () => undefined,
    setHeaderColor: () => undefined,
    ready: () => undefined,
  };
  window.Telegram = { WebApp: app };
}
