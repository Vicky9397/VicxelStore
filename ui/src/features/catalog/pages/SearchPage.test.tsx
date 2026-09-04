import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { SearchPage } from '@/features/catalog/pages/SearchPage';
import { server } from '@/test/server';

function renderSearch(): void {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/search']}>
        <SearchPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('SearchPage', () => {
  it('lists the products the API returns', async () => {
    renderSearch();

    expect(await screen.findByText('Sci-fi UI Kit')).toBeInTheDocument();
    expect(screen.getByText('Demo Studio')).toBeInTheDocument();
  });

  it('formats the starting price as currency', async () => {
    renderSearch();

    // Intl renders INR with a non-breaking space, so match on the digits.
    expect(await screen.findByText(/From.*900/)).toBeInTheDocument();
  });

  it('offers the categories the API returns', async () => {
    renderSearch();

    expect(await screen.findByRole('option', { name: 'UI Kits' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'Fonts' })).toBeInTheDocument();
  });

  it('pushes the chosen category into the request as a filter', async () => {
    let requestedUrl: string | null = null;
    server.use(
      http.get('/api/v1/products', ({ request }) => {
        requestedUrl = request.url;
        return HttpResponse.json({ data: [], page: { page: 1, pageSize: 20, total: 0 } });
      }),
    );
    const user = userEvent.setup();
    renderSearch();

    // The options arrive with the categories query, so wait for them first.
    await screen.findByRole('option', { name: 'UI Kits' });
    await user.selectOptions(screen.getByLabelText('Category'), 'ui-kits');

    await waitFor(() => {
      expect(requestedUrl).toContain('filter%5Bcategory%5D=ui-kits');
    });
  });

  it('shows an empty state rather than a blank grid', async () => {
    server.use(
      http.get('/api/v1/products', () =>
        HttpResponse.json({ data: [], page: { page: 1, pageSize: 20, total: 0 } }),
      ),
    );
    renderSearch();

    expect(await screen.findByText('No products match those filters.')).toBeInTheDocument();
  });
});
