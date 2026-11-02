import createClient from 'openapi-fetch';
import { getTelegram } from '../theme/telegram';
import type { paths } from './schema';

export function createApiClient(fetcher: typeof fetch = (...args) => fetch(...args)) {
  const client = createClient<paths>({ baseUrl: window.location.origin, fetch: fetcher });
  client.use({
    onRequest({ request }) {
      const initData = getTelegram()?.initData;
      if (initData) request.headers.set('Authorization', `tma ${initData}`);
      return request;
    },
  });
  return client;
}

export const api = createApiClient();
