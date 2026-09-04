import { HttpResponse, http } from 'msw';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import {
  ApiRequestError,
  apiRequest,
  getAccessToken,
  setAccessToken,
  setAuthLostHandler,
} from '@/lib/apiClient';
import { server } from '@/test/server';

describe('apiClient', () => {
  beforeEach(() => {
    setAccessToken(null);
    setAuthLostHandler(null);
  });

  it('sends the bearer token once a session exists', async () => {
    let seenAuthorization: string | null = null;
    server.use(
      http.get('/api/v1/me', ({ request }) => {
        seenAuthorization = request.headers.get('Authorization');
        return HttpResponse.json({ ok: true });
      }),
    );

    setAccessToken('token-abc');
    await apiRequest('/me');

    expect(seenAuthorization).toBe('Bearer token-abc');
  });

  it('omits the authorization header while anonymous', async () => {
    let seenAuthorization: string | null = 'unset';
    server.use(
      http.get('/api/v1/me', ({ request }) => {
        seenAuthorization = request.headers.get('Authorization');
        return HttpResponse.json({ ok: true });
      }),
    );

    await apiRequest('/me');

    expect(seenAuthorization).toBeNull();
  });

  it('normalizes a problem+json failure into an ApiRequestError', async () => {
    server.use(
      http.post('/api/v1/auth/register', () =>
        HttpResponse.json(
          {
            title: 'Validation failed',
            status: 422,
            code: 'VALIDATION_ERROR',
            errors: [{ field: 'email', message: 'Invalid email.' }],
          },
          { status: 422 },
        ),
      ),
    );

    const failure = apiRequest('/auth/register', { method: 'POST', body: {} });

    await expect(failure).rejects.toBeInstanceOf(ApiRequestError);
    await failure.catch((error: unknown) => {
      const apiError = error as ApiRequestError;
      expect(apiError.code).toBe('VALIDATION_ERROR');
      expect(apiError.status).toBe(422);
      expect(apiError.fieldErrors).toEqual([{ field: 'email', message: 'Invalid email.' }]);
    });
  });

  it('refreshes once on a 401 and replays the original request', async () => {
    let meCalls = 0;
    server.use(
      http.get('/api/v1/me', () => {
        meCalls += 1;
        return meCalls === 1
          ? HttpResponse.json({ code: 'UNAUTHENTICATED', title: 'expired', status: 401 }, { status: 401 })
          : HttpResponse.json({ ok: true });
      }),
      http.post('/api/v1/auth/token/refresh', () =>
        HttpResponse.json({ accessToken: 'refreshed-token', expiresInSeconds: 900 }),
      ),
    );

    const result = await apiRequest<{ ok: boolean }>('/me');

    expect(result.ok).toBe(true);
    expect(meCalls).toBe(2);
    expect(getAccessToken()).toBe('refreshed-token');
  });

  it('clears the session and notifies when the refresh also fails', async () => {
    const authLost = vi.fn();
    setAuthLostHandler(authLost);
    setAccessToken('stale-token');
    server.use(
      http.get('/api/v1/me', () =>
        HttpResponse.json({ code: 'UNAUTHENTICATED', title: 'expired', status: 401 }, { status: 401 }),
      ),
    );

    await expect(apiRequest('/me')).rejects.toBeInstanceOf(ApiRequestError);

    expect(authLost).toHaveBeenCalledOnce();
    expect(getAccessToken()).toBeNull();
  });

  it('does not attempt a refresh when the caller opts out', async () => {
    let refreshCalls = 0;
    server.use(
      http.post('/api/v1/auth/login', () =>
        HttpResponse.json({ code: 'UNAUTHENTICATED', title: 'bad credentials', status: 401 }, { status: 401 }),
      ),
      http.post('/api/v1/auth/token/refresh', () => {
        refreshCalls += 1;
        return HttpResponse.json({ accessToken: 'should-not-happen', expiresInSeconds: 900 });
      }),
    );

    await expect(
      apiRequest('/auth/login', { method: 'POST', body: {}, skipRefresh: true }),
    ).rejects.toBeInstanceOf(ApiRequestError);

    expect(refreshCalls).toBe(0);
  });
});
