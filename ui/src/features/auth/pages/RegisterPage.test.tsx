import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { RegisterPage } from '@/features/auth/pages/RegisterPage';
import { server } from '@/test/server';

function renderRegister(): void {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <RegisterPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

async function fillForm(
  user: ReturnType<typeof userEvent.setup>,
  values: { name: string; email: string; password: string },
): Promise<void> {
  await user.type(screen.getByLabelText('Display name'), values.name);
  await user.type(screen.getByLabelText('Email'), values.email);
  await user.type(screen.getByLabelText('Password'), values.password);
  await user.click(screen.getByRole('button', { name: 'Create account' }));
}

describe('RegisterPage', () => {
  it('rejects a password shorter than twelve characters', async () => {
    const user = userEvent.setup();
    renderRegister();

    await fillForm(user, { name: 'Buyer One', email: 'buyer@example.com', password: 'short' });

    expect(await screen.findByText('Password must be at least 12 characters.')).toBeInTheDocument();
  });

  it('rejects a password equal to the email local part', async () => {
    const user = userEvent.setup();
    renderRegister();

    await fillForm(user, {
      name: 'Buyer One',
      email: 'averylongbuyername@example.com',
      password: 'averylongbuyername',
    });

    expect(await screen.findByText('Password cannot equal the email local part.')).toBeInTheDocument();
  });

  it('shows the verification notice on success rather than logging the user in', async () => {
    const user = userEvent.setup();
    renderRegister();

    await fillForm(user, {
      name: 'Buyer One',
      email: 'buyer@example.com',
      password: 'a-long-enough-password',
    });

    expect(
      await screen.findByText('Check your email to verify your account before buying or selling.'),
    ).toBeInTheDocument();
  });

  it('merges server field errors into the form', async () => {
    server.use(
      http.post('/api/v1/auth/register', () =>
        HttpResponse.json(
          {
            title: 'Validation failed',
            status: 422,
            code: 'WEAK_PASSWORD',
            errors: [{ field: 'password', message: 'This password appears in a breach list.' }],
          },
          { status: 422 },
        ),
      ),
    );
    const user = userEvent.setup();
    renderRegister();

    await fillForm(user, {
      name: 'Buyer One',
      email: 'buyer@example.com',
      password: 'a-long-enough-password',
    });

    expect(await screen.findByText('This password appears in a breach list.')).toBeInTheDocument();
  });
});
