import './theme/tokens.css';
import './styles.css';
import { themeStore } from './theme/store';

// Модуль теми застосовує збережений вибір перед завантаженням React і видимим рендером.
void themeStore;
if (import.meta.env.DEV && !window.Telegram?.WebApp?.initData) {
  await import('./dev/mockTelegram').then(module => module.setupMockTelegram());
}
await import('./main');
