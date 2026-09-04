import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import type { ReactElement } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it } from 'vitest';
import { LoginPage } from '@/features/auth/pages/LoginPage';
import { useAuthStore } from '@/features/auth/store';
import { server } from '@/test/server';

function renderLogin(): ReactElement {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <LoginPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
  return <div />;
}

describe('LoginPage', () => {
  beforeEach(() => {
    useAuthStore.setState({ user: null, status: 'anonymous' });
  });

  it('rejects a malformed email before calling the API', async () => {
    const user = userEvent.setup();
    renderLogin();

    await user.type(screen.getByLabelText('Email'), 'not-an-email');
    await user.type(screen.getByLabelText('Password'), 'a-long-enough-password');
    await user.click(screen.getByRole('button', { name: 'Log in' }));

    expect(await screen.findByText('Enter a valid email address.')).toBeInTheDocument();
    expect(useAuthStore.getState().status).toBe('anonymous');
  });

  it('stores the session on a successful login', async () => {
    const user = userEvent.setup();
    renderLogin();

    await user.type(screen.getByLabelText('Email'), 'buyer@example.com');
    await user.type(screen.getByLabelText('Password'), 'a-long-enough-password');
    await user.click(screen.getByRole('button', { name: 'Log in' }));

    await waitFor(() => {
      expect(useAuthStore.getState().status).toBe('authenticated');
    });
    expect(useAuthStore.getState().user?.email).toBe('buyer@example.com');
  });

  it('surfaces the generic credential error without revealing the cause', async () => {
    server.use(
      http.post('/api/v1/auth/login', () =>
        HttpResponse.json(
          { title: 'Invalid email or password.', status: 401, code: 'UNAUTHENTICATED' },
          { status: 401 },
        ),
      ),
    );
    const user = userEvent.setup();
    renderLogin();

    await user.type(screen.getByLabelText('Email'), 'buyer@example.com');
    await user.type(screen.getByLabelText('Password'), 'wrong-password-here');
    await user.click(screen.getByRole('button', { name: 'Log in' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid email or password.');
    expect(useAuthStore.getState().status).toBe('anonymous');
  });
});
