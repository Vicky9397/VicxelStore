import { HttpResponse, http } from 'msw';
import type { AuthResponse, User } from '@/types/api';

export const testUser: User = {
  id: '11111111-1111-1111-1111-111111111111',
  email: 'buyer@example.com',
  emailVerified: true,
  displayName: 'Buyer One',
  roles: ['Buyer'],
};

export const testAuthResponse: AuthResponse = {
  accessToken: 'test-access-token',
  expiresInSeconds: 900,
  user: testUser,
};

/** Default happy paths; individual tests override with server.use(). */
export const handlers = [
  http.post('/api/v1/auth/login', () => HttpResponse.json(testAuthResponse)),
  http.post('/api/v1/auth/register', () =>
    HttpResponse.json({ message: 'Check your email to verify your account.' }, { status: 201 }),
  ),
  http.post('/api/v1/auth/token/refresh', () =>
    HttpResponse.json(
      {
        type: 'https://vicxelstore.example/errors/unauthenticated',
        title: 'The token is invalid or has expired.',
        status: 401,
        code: 'UNAUTHENTICATED',
      },
      { status: 401 },
    ),
  ),
  http.post('/api/v1/auth/email/verify', () => HttpResponse.json({ message: 'Email verified.' })),
  http.post('/api/v1/auth/email/resend', () =>
    HttpResponse.json({ message: 'If the account exists, a verification email was sent.' }, { status: 202 }),
  ),
  http.post('/api/v1/auth/logout', () => new HttpResponse(null, { status: 204 })),
  http.get('/api/v1/me', () => HttpResponse.json(testUser)),
];
