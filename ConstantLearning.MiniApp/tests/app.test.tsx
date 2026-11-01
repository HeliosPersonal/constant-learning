import { fireEvent, render, screen } from '@testing-library/react';
import { expect, it, vi } from 'vitest';
import { QueryClientProvider } from '@tanstack/react-query';
import { App } from '../src/App';
import { ThemeProvider } from '../src/theme/ThemeProvider';
import { createQueryClient } from '../src/api/query';

it('ручна тема зберігає відповідь, картку й portal, не надсилаючи запитів', () => {
  const network = vi.spyOn(globalThis, 'fetch');
  render(<QueryClientProvider client={createQueryClient()}><ThemeProvider><App /></ThemeProvider></QueryClientProvider>);
  fireEvent.change(screen.getByLabelText('Ваш варіант польською'), { target: { value: 'spokój' } });
  fireEvent.click(screen.getByRole('button', { name: 'Про оформлення' }));
  expect(screen.getByRole('dialog')).toBeVisible();
  expect(document.querySelector('dialog')?.parentElement).toBe(document.body);
  fireEvent.click(screen.getByRole('radio', { name: /Темна/ }));
  expect(document.documentElement.dataset.theme).toBe('dark');
  expect(screen.getByLabelText('Ваш варіант польською')).toHaveValue('spokój');
  expect(screen.getByText('Картка 1 із 5')).toBeInTheDocument();
  expect(screen.getByRole('dialog')).toBeVisible();
  expect(network).not.toHaveBeenCalled();
  network.mockRestore();
});

it('згенерований клієнт передає свіжі initData без збереження ідентичності', async () => {
  const { createApiClient } = await import('../src/api/client');
  window.Telegram = { WebApp: { initData: 'signed=test' } };
  const fetcher = vi.fn<typeof fetch>().mockImplementation(async () => new Response(JSON.stringify({ code: 'learner.not_onboarded', message: 'Спершу налаштуйте тренер.' }), { status: 404, headers: { 'Content-Type': 'application/json' } }));
  const client = createApiClient(fetcher);
  const result = await client.GET('/api/v1/learners/me');
  const request = fetcher.mock.calls[0][0] as Request;
  expect(request.url).toBe(`${window.location.origin}/api/v1/learners/me`);
  expect(request.headers.get('Authorization')).toBe('tma signed=test');
  expect(result.error?.code).toBe('learner.not_onboarded');
  window.Telegram.WebApp!.initData = 'signed=next';
  await client.GET('/api/v1/learners/me');
  expect((fetcher.mock.calls[1][0] as Request).headers.get('Authorization')).toBe('tma signed=next');
  delete window.Telegram;
});
