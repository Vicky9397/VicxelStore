import type { ReactElement } from 'react';
import { createBrowserRouter, RouterProvider } from 'react-router-dom';
import { AppLayout } from '@/components/layout/AppLayout';
import { AccountPage } from '@/features/account/pages/AccountPage';
import { LoginPage } from '@/features/auth/pages/LoginPage';
import { RegisterPage } from '@/features/auth/pages/RegisterPage';
import { VerifyEmailPage } from '@/features/auth/pages/VerifyEmailPage';
import { HomePage } from '@/features/catalog/pages/HomePage';
import { ForbiddenPage, NotFoundPage, RouteErrorPage } from '@/routes/ErrorPages';
import { RequireAuth } from '@/routes/guards';

const router = createBrowserRouter([
  {
    path: '/',
    element: <AppLayout />,
    errorElement: <RouteErrorPage />,
    children: [
      { index: true, element: <HomePage /> },
      { path: 'login', element: <LoginPage /> },
      { path: 'register', element: <RegisterPage /> },
      { path: 'verify-email', element: <VerifyEmailPage /> },
      {
        element: <RequireAuth />,
        children: [{ path: 'account', element: <AccountPage /> }],
      },
      { path: '403', element: <ForbiddenPage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]);

export function AppRouter(): ReactElement {
  return <RouterProvider router={router} />;
}
