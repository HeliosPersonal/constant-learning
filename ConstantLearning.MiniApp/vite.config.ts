import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import { readFileSync } from 'node:fs';
import { readInitialTheme } from './src/theme/initial';

export default defineConfig({
  plugins: [react(), {
    name: 'early-everforest-theme',
    transformIndexHtml(html) {
      const css = `${readFileSync(new URL('./src/theme/tokens.css', import.meta.url), 'utf8')}html,body{background:var(--cl-background);color:var(--cl-text)}`;
      const script = `(()=>{const initial=(${readInitialTheme.toString()})({storage:()=>localStorage,telegram:()=>window.Telegram?.WebApp,media:()=>window.matchMedia?.('(prefers-color-scheme: dark)')});document.documentElement.dataset.theme=initial.theme;document.documentElement.style.colorScheme=initial.theme;})();`;
      return html.replace('<!-- Початкова тема -->', `<style>${css}</style><script>${script}</script>`);
    },
  }],
  build: { outDir: '../ConstantLearning/wwwroot', emptyOutDir: true },
  server: { port: 5173, strictPort: true, proxy: { '/api': 'http://127.0.0.1:8888' } },
  test: { environment: 'jsdom', setupFiles: ['./tests/setup.ts'], include: ['tests/**/*.test.{ts,tsx}'] },
});
