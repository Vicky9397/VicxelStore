import '@testing-library/jest-dom/vitest';
import { afterAll, afterEach, beforeAll } from 'vitest';
import { setAccessToken } from '@/lib/apiClient';
import { server } from '@/test/server';
import '@/lib/i18n';

beforeAll(() => {
  server.listen({ onUnhandledRequest: 'error' });
});

afterEach(() => {
  server.resetHandlers();
  setAccessToken(null);
});

afterAll(() => {
  server.close();
});
