import type { ReactElement } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { useAuthStore } from '@/features/auth/store';

/**
 * Landing page. Featured and trending rails arrive with the recommendation work
 * in a later phase; browse and search are live.
 */
export function HomePage(): ReactElement {
  const { t } = useTranslation();
  const user = useAuthStore((state) => state.user);

  return (
    <div>
      <h1 className="h2">{t('app.name')}</h1>
      <p className="lead text-muted">{t('app.tagline')}</p>

      <div className="d-flex gap-2 align-items-center">
        <Link className="btn btn-primary" to="/search">
          {t('catalog.browseTitle')}
        </Link>
        {user === null ? (
          <Link className="btn btn-outline-primary" to="/register">
            {t('nav.register')}
          </Link>
        ) : (
          <span className="text-muted">{t('auth.loggedInAs', { name: user.displayName })}</span>
        )}
      </div>
    </div>
  );
}
