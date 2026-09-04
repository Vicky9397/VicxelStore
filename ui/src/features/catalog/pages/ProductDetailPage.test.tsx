import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { HttpResponse, http } from 'msw';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { ProductDetailPage } from '@/features/catalog/pages/ProductDetailPage';
import { server } from '@/test/server';

function renderDetail(slug = 'sci-fi-ui-kit'): void {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[`/p/${slug}`]}>
        <Routes>
          <Route path="/p/:slug" element={<ProductDetailPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('ProductDetailPage', () => {
  it('shows the product, its store and its versions', async () => {
    renderDetail();

    expect(await screen.findByRole('heading', { name: 'Sci-fi UI Kit' })).toBeInTheDocument();
    expect(screen.getByText('Demo Studio')).toBeInTheDocument();
    expect(screen.getByText('1.0.0')).toBeInTheDocument();
  });

  it('lists every active variant as a selectable license', async () => {
    renderDetail();

    expect(await screen.findByLabelText(/Personal/)).toBeInTheDocument();
    expect(screen.getByLabelText(/Commercial/)).toBeInTheDocument();
  });

  it('renders a not-found page when the product is not published', async () => {
    server.use(
      http.get('/api/v1/products/:slug', () =>
        HttpResponse.json(
          { title: 'Product not found.', status: 404, code: 'NOT_FOUND' },
          { status: 404 },
        ),
      ),
    );
    renderDetail('missing');

    expect(await screen.findByText('Page not found')).toBeInTheDocument();
  });
});
