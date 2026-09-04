import type { ReactElement } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useRouteError } from 'react-router-dom';

export function NotFoundPage(): ReactElement {
  const { t } = useTranslation();

  return (
    <div className="text-center py-5">
      <h1 className="h3">{t('errors.notFoundTitle')}</h1>
      <p className="text-muted">{t('errors.notFoundBody')}</p>
      <Link className="btn btn-outline-primary" to="/">
        {t('common.home')}
      </Link>
    </div>
  );
}

export function ForbiddenPage(): ReactElement {
  const { t } = useTranslation();

  return (
    <div className="text-center py-5">
      <h1 className="h3">{t('errors.forbiddenTitle')}</h1>
      <p className="text-muted">{t('errors.forbiddenBody')}</p>
      <Link className="btn btn-outline-primary" to="/">
        {t('common.home')}
      </Link>
    </div>
  );
}

export function RouteErrorPage(): ReactElement {
  const { t } = useTranslation();
  const error = useRouteError();
  const message = error instanceof Error ? error.message : t('errors.generic');

  return (
    <div className="container py-5 text-center">
      <h1 className="h3">{t('errors.generic')}</h1>
      <p className="text-muted">{message}</p>
      <Link className="btn btn-outline-primary" to="/">
        {t('common.home')}
      </Link>
    </div>
  );
}
