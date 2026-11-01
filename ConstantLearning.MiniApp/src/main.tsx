import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClientProvider } from '@tanstack/react-query';
import '@telegram-apps/telegram-ui/dist/styles.css';
import './theme/tokens.css';
import './styles.css';
import { ThemeProvider } from './theme/ThemeProvider';
import { createQueryClient } from './api/query';
import { getTelegram } from './theme/telegram';
import { App } from './App';

createRoot(document.getElementById('root')!).render(
  <StrictMode><QueryClientProvider client={createQueryClient()}><ThemeProvider><App /></ThemeProvider></QueryClientProvider></StrictMode>,
);
getTelegram()?.ready?.();
