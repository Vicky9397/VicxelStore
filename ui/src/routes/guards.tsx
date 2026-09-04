import type { ReactElement } from 'react';
import { useTranslation } from 'react-i18next';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { Spinner } from '@/components/ui/Spinner';
import { useAuthStore } from '@/features/auth/store';
import type { Role } from '@/types/api';

/** Redirects anonymous visitors to login, preserving where they were going. */
export function RequireAuth(): ReactElement {
  const status = useAuthStore((state) => state.status);
  const location = useLocation();

  if (status === 'unknown') {
    return <Spinner />;
  }

  if (status === 'anonymous') {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  }

  return <Outlet />;
}

export function RequireRole({ roles }: { roles: Role[] }): ReactElement {
  const status = useAuthStore((state) => state.status);
  const user = useAuthStore((state) => state.user);

  if (status === 'unknown') {
    return <Spinner />;
  }

  if (user === null) {
    return <Navigate to="/login" replace />;
  }

  return user.roles.some((role) => roles.includes(role)) ? <Outlet /> : <Navigate to="/403" replace />;
}

/** Blocks selling and downloading until the account's email is verified. */
export function RequireVerified(): ReactElement {
  const { t } = useTranslation();
  const user = useAuthStore((state) => state.user);
  const status = useAuthStore((state) => state.status);

  if (status === 'unknown') {
    return <Spinner />;
  }

  if (user === null) {
    return <Navigate to="/login" replace />;
  }

  if (!user.emailVerified) {
    return (
      <div className="alert alert-warning" role="alert">
        {t('auth.unverifiedBanner')}
      </div>
    );
  }

  return <Outlet />;
}
