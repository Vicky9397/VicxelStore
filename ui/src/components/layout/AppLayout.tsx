import type { ReactElement } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, Outlet } from 'react-router-dom';
import { logout } from '@/features/auth/api/authApi';
import { useAuthStore } from '@/features/auth/store';

export function AppLayout(): ReactElement {
  const { t, i18n } = useTranslation();
  const user = useAuthStore((state) => state.user);
  const clearSession = useAuthStore((state) => state.clearSession);

  const handleLogout = (): void => {
    void logout().finally(() => {
      clearSession();
    });
  };

  const toggleLocale = (): void => {
    void i18n.changeLanguage(i18n.language === 'en' ? 'ta' : 'en');
  };

  return (
    <div className="d-flex flex-column min-vh-100">
      <header className="navbar navbar-expand navbar-dark bg-dark">
        <div className="container">
          <Link className="navbar-brand" to="/">
            {t('app.name')}
          </Link>
          <ul className="navbar-nav me-auto">
            <li className="nav-item">
              <Link className="nav-link" to="/search">
                {t('nav.browse')}
              </Link>
            </li>
          </ul>
          <div className="d-flex align-items-center gap-2">
            <button className="btn btn-sm btn-outline-light" type="button" onClick={toggleLocale}>
              {i18n.language === 'en' ? 'தமிழ்' : 'English'}
            </button>
            {user === null ? (
              <>
                <Link className="btn btn-sm btn-outline-light" to="/login">
                  {t('nav.login')}
                </Link>
                <Link className="btn btn-sm btn-primary" to="/register">
                  {t('nav.register')}
                </Link>
              </>
            ) : (
              <>
                <Link className="btn btn-sm btn-outline-light" to="/seller/store">
                  {t('nav.seller')}
                </Link>
                <Link className="btn btn-sm btn-outline-light" to="/seller/products">
                  {t('nav.sellerProducts')}
                </Link>
                <Link className="btn btn-sm btn-outline-light" to="/account">
                  {t('nav.account')}
                </Link>
                <button className="btn btn-sm btn-outline-light" type="button" onClick={handleLogout}>
                  {t('nav.logout')}
                </button>
              </>
            )}
          </div>
        </div>
      </header>

      {user !== null && !user.emailVerified ? (
        <div className="alert alert-warning mb-0 rounded-0 text-center" role="alert">
          {t('auth.unverifiedBanner')}{' '}
          <Link to="/verify-email">{t('auth.resendTitle')}</Link>
        </div>
      ) : null}

      <main className="container flex-grow-1 py-4">
        <Outlet />
      </main>

      <footer className="border-top py-3">
        <div className="container text-muted small">
          {t('app.name')} — {t('app.tagline')}
        </div>
      </footer>
    </div>
  );
}
