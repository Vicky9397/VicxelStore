import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import { LicenseCard } from '@/features/orders/pages/LicenseCard';
import { server } from '@/test/server';
import type { License } from '@/types/api';

const license: License = {
  id: '77777777-7777-7777-7777-777777777777',
  productTitle: 'Sci-fi UI Kit',
  variantName: 'Personal',
  downloadLimit: 5,
  downloadsUsed: 1,
  issuedAtUtc: '2026-02-01T10:00:05Z',
  files: [{ id: '88888888-8888-8888-8888-888888888888', fileName: 'kit.zip', sizeBytes: 5_242_880 }],
};

function renderCard(overrides: Partial<License> = {}): void {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={queryClient}>
      <LicenseCard license={{ ...license, ...overrides }} onDownloaded={vi.fn()} />
    </QueryClientProvider>,
  );
}

function stubDownload(status: number, code: string): void {
  server.use(
    http.get('/api/v1/licenses/:licenseId/download', () =>
      HttpResponse.json({ title: 'Refused', status, code }, { status }),
    ),
  );
}

describe('LicenseCard', () => {
  it('shows the remaining quota rather than only the limit', () => {
    renderCard();

    expect(screen.getByText('4 of 5 downloads left')).toBeInTheDocument();
  });

  it('disables downloading once the quota is spent', () => {
    renderCard({ downloadsUsed: 5 });

    expect(screen.getByRole('button', { name: 'Download' })).toBeDisabled();
  });

  it('explains a quota refusal in the buyer\'s terms', async () => {
    stubDownload(429, 'DOWNLOAD_LIMIT');
    const user = userEvent.setup();
    renderCard();

    await user.click(screen.getByRole('button', { name: 'Download' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'You have used every download this license allows.',
    );
  });

  it('explains that a file is still being scanned', async () => {
    stubDownload(409, 'FILE_NOT_READY');
    const user = userEvent.setup();
    renderCard();

    await user.click(screen.getByRole('button', { name: 'Download' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('still being scanned');
  });

  it('explains a quarantined file rather than showing a dead link', async () => {
    stubDownload(410, 'FILE_QUARANTINED');
    const user = userEvent.setup();
    renderCard();

    await user.click(screen.getByRole('button', { name: 'Download' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('failed a virus scan');
  });

  it('tells the buyer when a license has no published file yet', () => {
    renderCard({ files: [] });

    expect(screen.getByText(/has not published a file/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Download' })).not.toBeInTheDocument();
  });
});
