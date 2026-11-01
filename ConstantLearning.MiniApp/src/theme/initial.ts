import type { ThemeEnvironment } from './store';
import type { Theme } from './telegram';

// Функція самодостатня: Vite також вбудовує її в head перед завантаженням модулів.
export function readInitialTheme(environment: Pick<ThemeEnvironment, 'storage' | 'telegram' | 'media'>): { manual?: Theme; theme: Theme } {
  const valid = (value: unknown): value is Theme => value === 'light' || value === 'dark';
  let manual: Theme | undefined;
  try {
    const saved = environment.storage().getItem('constant-learning.theme.v1');
    if (valid(saved)) manual = saved;
  } catch { /* Сховище може бути недоступне у WebView. */ }
  if (manual) return { manual, theme: manual };
  const telegram = environment.telegram()?.colorScheme;
  if (valid(telegram)) return { theme: telegram };
  try { return { theme: environment.media()?.matches ? 'dark' : 'light' }; }
  catch { return { theme: 'light' }; }
}
