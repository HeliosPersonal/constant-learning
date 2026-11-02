import { createContext, useContext, useEffect, useSyncExternalStore, type ReactNode } from 'react';
import { AppRoot } from '@telegram-apps/telegram-ui';
import { themeStore } from './store';
import type { Theme } from './telegram';

const ThemeContext = createContext<{ theme: Theme; setTheme: (theme: Theme) => void } | null>(null);

export function ThemeProvider({ children }: { children: ReactNode }) {
  const theme = useSyncExternalStore(themeStore.subscribe, themeStore.getSnapshot);
  useEffect(() => themeStore.connect(), []);
  return (
    <ThemeContext.Provider value={{ theme, setTheme: themeStore.setTheme }}>
      <AppRoot appearance={theme} platform="base" className="everforest-root">{children}</AppRoot>
    </ThemeContext.Provider>
  );
}

export function useTheme() {
  const value = useContext(ThemeContext);
  if (!value) throw new Error('Провайдер теми не підключено.');
  return value;
}
