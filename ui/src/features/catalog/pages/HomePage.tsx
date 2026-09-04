import type { ReactElement } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { useAuthStore } from '@/features/auth/store';

/**
 * Placeholder storefront. The catalog feature (featured, trending, categories)
 * arrives with milestone M2; this keeps the shell navigable until then.
 */
export function HomePage(): ReactElement {
  const { t } = useTranslation();
  const user = useAuthStore((state) => state.user);

  return (
    <div>
      <h1 className="h2">{t('app.name')}</h1>
      <p className="lead text-muted">{t('app.tagline')}</p>

      {user === null ? (
        <Link className="btn btn-primary" to="/register">
          {t('nav.register')}
        </Link>
      ) : (
        <p>{t('auth.loggedInAs', { name: user.displayName })}</p>
      )}
    </div>
  );
}
