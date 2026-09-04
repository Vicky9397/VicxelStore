import type { ReactElement } from 'react';
import { useTranslation } from 'react-i18next';
import { useAuthStore } from '@/features/auth/store';

/** Buyer dashboard shell; purchases, licenses and settings land in M3. */
export function AccountPage(): ReactElement {
  const { t } = useTranslation();
  const user = useAuthStore((state) => state.user);

  if (user === null) {
    return <p>{t('common.loading')}</p>;
  }

  return (
    <div>
      <h1 className="h3">{t('nav.account')}</h1>
      <dl className="row">
        <dt className="col-sm-3">{t('auth.displayName')}</dt>
        <dd className="col-sm-9">{user.displayName}</dd>
        <dt className="col-sm-3">{t('auth.email')}</dt>
        <dd className="col-sm-9">
          {user.email}{' '}
          {user.emailVerified ? (
            <span className="badge text-bg-success">verified</span>
          ) : (
            <span className="badge text-bg-warning">unverified</span>
          )}
        </dd>
      </dl>
    </div>
  );
}
