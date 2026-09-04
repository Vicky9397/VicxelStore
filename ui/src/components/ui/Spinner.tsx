import type { ReactElement } from 'react';
import { useTranslation } from 'react-i18next';

export function Spinner(): ReactElement {
  const { t } = useTranslation();

  return (
    <div className="d-flex justify-content-center py-5" role="status" aria-live="polite">
      <div className="spinner-border text-primary">
        <span className="visually-hidden">{t('common.loading')}</span>
      </div>
    </div>
  );
}
