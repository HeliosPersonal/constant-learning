import { describe, expect, it, vi } from 'vitest';
import { createThemeStore, THEME_KEY, type ThemeEnvironment } from '../src/theme/store';
import { syncTelegramChrome, type TelegramWebApp } from '../src/theme/telegram';
import tokens from '../../docs/features/personal-trainer-mvp/design/theme-tokens.json';

function environment(saved: string | null = null, telegram?: TelegramWebApp, dark = false) {
  const storage = { getItem: vi.fn(() => saved), setItem: vi.fn() };
  const apply = vi.fn();
  const env: ThemeEnvironment = { storage: () => storage, telegram: () => telegram, media: () => ({ matches: dark }) as MediaQueryList, apply };
  return { env, storage, apply };
}

describe('Початкова тема та збереження', () => {
  it.each([
    ['dark', 'light', false, 'dark'], ['light', 'dark', true, 'light'],
    [null, 'dark', false, 'dark'], ['invalid', 'light', true, 'light'],
    [null, undefined, true, 'dark'], [null, undefined, false, 'light'],
  ])('пріоритет %s → Telegram %s → ОС %s', (saved, telegram, system, expected) => {
    const { env, apply } = environment(saved, { colorScheme: telegram }, system);
    const store = createThemeStore(env);
    expect(store.getSnapshot()).toBe(expected);
    expect(apply).toHaveBeenCalledExactlyOnceWith(expected);
  });
  it('повторне відкриття читає попередній ручний вибір', () => {
    const storage = new Map<string, string>();
    const { env } = environment();
    env.storage = () => ({ getItem: key => storage.get(key) ?? null, setItem: (key, value) => { storage.set(key, value); } });
    createThemeStore(env).setTheme('dark');
    expect(storage.get(THEME_KEY)).toBe('dark');
    expect(createThemeStore(env).getSnapshot()).toBe('dark');
  });
  it('помилка читання та запису сховища не блокує перемикання', () => {
    const { env, apply } = environment(null, { colorScheme: 'dark' });
    env.storage = () => { throw new DOMException('Недоступно', 'SecurityError'); };
    const store = createThemeStore(env);
    expect(store.getSnapshot()).toBe('dark');
    store.setTheme('light');
    expect(store.getSnapshot()).toBe('light');
    expect(apply).toHaveBeenLastCalledWith('light');
  });
  it('відсутні Telegram, matchMedia і сховище дають світлу тему', () => {
    const { env } = environment();
    env.storage = () => { throw new Error(); };
    env.media = () => undefined;
    expect(createThemeStore(env).getSnapshot()).toBe('light');
  });
  it.each([false, true])('themeChanged змінює лише автоматичну тему; збережено=%s', saved => {
    let listener = () => undefined as void;
    const app = { colorScheme: 'light', onEvent: vi.fn((_event, callback) => { listener = callback; }), offEvent: vi.fn() };
    const { env } = environment(saved ? 'light' : null, app);
    const store = createThemeStore(env);
    const disconnect = store.connect();
    app.colorScheme = 'dark'; listener();
    expect(store.getSnapshot()).toBe(saved ? 'light' : 'dark');
    store.setTheme('light'); listener();
    expect(store.getSnapshot()).toBe('light');
    disconnect(); expect(app.offEvent).toHaveBeenCalledWith('themeChanged', listener);
  });
});

describe('Сумісність оболонки Telegram', () => {
  it.each([['6.0', 0, undefined], ['6.1', 1, 'bg_color'], ['6.8', 1, 'bg_color'], ['6.9', 1, tokens.themes.dark.background], ['8.0', 1, tokens.themes.dark.background], [undefined, 0, undefined]])('версія %s', (version, calls, header) => {
    const app = { version, setBackgroundColor: vi.fn(), setHeaderColor: vi.fn() };
    syncTelegramChrome('dark', app);
    expect(app.setBackgroundColor).toHaveBeenCalledTimes(calls);
    if (calls) expect(app.setHeaderColor).toHaveBeenCalledWith(header);
  });
  it('непрацююче API оболонки не перериває застосунок', () => {
    expect(() => syncTelegramChrome('dark', { version: '8.0', setHeaderColor: () => { throw new Error(); }, setBackgroundColor: () => { throw new Error(); } })).not.toThrow();
  });
});

function luminance(hex: string) {
  const values = hex.slice(1).match(/../g)!.map(value => parseInt(value, 16) / 255).map(value => value <= .04045 ? value / 12.92 : ((value + .055) / 1.055) ** 2.4);
  return .2126 * values[0] + .7152 * values[1] + .0722 * values[2];
}
function contrast(a: string, b: string) { const values = [luminance(a), luminance(b)].sort((x, y) => y - x); return (values[0] + .05) / (values[1] + .05); }
describe.each(Object.entries(tokens.themes))('Контраст Everforest %s', (_name, theme) => {
  it('усі погоджені текстові пари мають 4.5:1 до округлення', () => {
    for (const [a, b] of [['text', 'background'], ['text', 'surface'], ['muted', 'background'], ['muted', 'surface'], ['onAccent', 'accent'], ['danger', 'surface']] as const) expect(contrast(theme[a], theme[b])).toBeGreaterThanOrEqual(4.5);
  });
  it('контури контролів і фокус мають 3:1', () => {
    for (const a of ['muted', 'accent'] as const) for (const b of ['background', 'surface'] as const) expect(contrast(theme[a], theme[b])).toBeGreaterThanOrEqual(3);
  });
});
