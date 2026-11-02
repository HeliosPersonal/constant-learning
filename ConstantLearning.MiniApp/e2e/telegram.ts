import { mockTelegramEnv } from '@tma.js/sdk';

mockTelegramEnv({ launchParams: { tgWebAppVersion: '8.0', tgWebAppPlatform: 'tdesktop', tgWebAppThemeParams: { bg_color: '#ffffff', text_color: '#000000', button_color: '#000000', button_text_color: '#ffffff' } }, onEvent: () => undefined });
const initData = sessionStorage.getItem('e2e.initData') ?? '';
Object.defineProperty(window, 'Telegram', { configurable: true, value: { WebApp: { initData, version: '8.0', colorScheme: 'light', onEvent() {}, offEvent() {}, setBackgroundColor() {}, setHeaderColor() {}, ready() {} } } });
