import { HttpResponse, http } from 'msw';
import type {
  AuthResponse,
  Category,
  ProductDetail,
  ProductPage,
  SellerProductDetail,
  User,
} from '@/types/api';

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

export const testCategories: Category[] = [
  { id: 1, slug: 'ui-kits', name: 'UI Kits', parentId: null },
  { id: 2, slug: 'fonts', name: 'Fonts', parentId: null },
];

export const testProductPage: ProductPage = {
  data: [
    {
      id: '22222222-2222-2222-2222-222222222222',
      slug: 'sci-fi-ui-kit',
      title: 'Sci-fi UI Kit',
      storeSlug: 'demo-studio',
      storeName: 'Demo Studio',
      categorySlug: 'ui-kits',
      fromPrice: { amount: 900, currency: 'INR' },
      ratingAvg: 4.5,
      ratingCount: 12,
      publishedAtUtc: '2026-01-01T00:00:00Z',
    },
  ],
  page: { page: 1, pageSize: 20, total: 1 },
};

export const testProductDetail: ProductDetail = {
  id: '22222222-2222-2222-2222-222222222222',
  slug: 'sci-fi-ui-kit',
  title: 'Sci-fi UI Kit',
  description: 'A dark interface kit.',
  storeSlug: 'demo-studio',
  storeName: 'Demo Studio',
  categorySlug: 'ui-kits',
  ratingAvg: 4.5,
  ratingCount: 12,
  publishedAtUtc: '2026-01-01T00:00:00Z',
  variants: [
    {
      id: '33333333-3333-3333-3333-333333333333',
      name: 'Personal',
      price: { amount: 900, currency: 'INR' },
      licenseType: 'Personal',
      downloadLimit: 5,
      isActive: true,
      hasCleanFile: true,
    },
    {
      id: '44444444-4444-4444-4444-444444444444',
      name: 'Commercial',
      price: { amount: 2400, currency: 'INR' },
      licenseType: 'Commercial',
      downloadLimit: 10,
      isActive: true,
      hasCleanFile: true,
    },
  ],
  versions: [
    { versionNumber: '1.0.0', changelog: 'Initial release.', releasedAtUtc: '2026-01-01T00:00:00Z' },
  ],
  tags: ['figma', 'dark'],
};

export const testSellerProductDetail: SellerProductDetail = {
  id: '22222222-2222-2222-2222-222222222222',
  slug: 'sci-fi-ui-kit',
  title: 'Sci-fi UI Kit',
  description: 'A dark interface kit.',
  categorySlug: 'ui-kits',
  status: 'Draft',
  isPublishReady: false,
  variants: [
    {
      id: '33333333-3333-3333-3333-333333333333',
      name: 'Personal',
      price: { amount: 900, currency: 'INR' },
      licenseType: 'Personal',
      downloadLimit: 5,
      isActive: true,
      hasCleanFile: false,
    },
  ],
  versions: [
    { versionNumber: '1.0.0', changelog: 'Initial release.', releasedAtUtc: '2026-01-01T00:00:00Z' },
  ],
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
  http.get('/api/v1/categories', () => HttpResponse.json(testCategories)),
  http.get('/api/v1/products', () => HttpResponse.json(testProductPage)),
  http.get('/api/v1/products/mine', () => HttpResponse.json([])),
  http.get('/api/v1/products/:slug', () => HttpResponse.json(testProductDetail)),
];
