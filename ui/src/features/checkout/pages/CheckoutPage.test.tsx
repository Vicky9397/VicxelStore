import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { CheckoutPage } from '@/features/checkout/pages/CheckoutPage';
import { server } from '@/test/server';

function renderCheckout(): void {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <CheckoutPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('CheckoutPage', () => {
  it('shows the tax and total the quote returned', async () => {
    renderCheckout();

    expect(await screen.findByText(/162/)).toBeInTheDocument();
    expect(screen.getByText(/1,062|1062/)).toBeInTheDocument();
  });

  it('re-quotes when the billing country changes, because tax follows the region', async () => {
    const requestedCountries: string[] = [];
    server.use(
      http.post('/api/v1/checkout/quote', async ({ request }) => {
        const body = (await request.json()) as { billingCountry: string };
        requestedCountries.push(body.billingCountry);
        return HttpResponse.json({
          totals: { subtotal: 900, discount: 0, tax: 180, grandTotal: 1080, currency: 'INR' },
          items: [],
        });
      }),
    );
    const user = userEvent.setup();
    renderCheckout();

    await screen.findByLabelText('Billing country');
    await user.selectOptions(screen.getByLabelText('Billing country'), 'GB');

    await waitFor(() => {
      expect(requestedCountries).toContain('GB');
    });
  });

  it('sends an idempotency key and the total the buyer was shown', async () => {
    let sentKey: string | null = null;
    let sentTotal: number | null = null;
    server.use(
      http.post('/api/v1/checkout/confirm', async ({ request }) => {
        sentKey = request.headers.get('Idempotency-Key');
        const body = (await request.json()) as { expectedGrandTotal: number };
        sentTotal = body.expectedGrandTotal;
        return HttpResponse.json({
          orderPublicId: '66666666-6666-6666-6666-666666666666',
          paymentIntent: { provider: 'sandbox', clientSecret: 'sbx_1_secret_x', instructions: null },
          totals: { subtotal: 900, discount: 0, tax: 162, grandTotal: 1062, currency: 'INR' },
        });
      }),
    );
    const user = userEvent.setup();
    renderCheckout();

    await user.click(await screen.findByRole('button', { name: 'Place order' }));

    await waitFor(() => {
      expect(sentKey).not.toBeNull();
    });
    expect(sentTotal).toBe(1062);
  });

  it('surfaces a price change instead of charging the new total', async () => {
    server.use(
      http.post('/api/v1/checkout/confirm', () =>
        HttpResponse.json(
          {
            title: 'A price changed while you were checking out. Review the new total.',
            status: 409,
            code: 'PRICE_CHANGED',
          },
          { status: 409 },
        ),
      ),
    );
    const user = userEvent.setup();
    renderCheckout();

    await user.click(await screen.findByRole('button', { name: 'Place order' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('price changed');
  });
});
