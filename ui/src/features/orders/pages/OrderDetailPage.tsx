import { useQuery, useQueryClient } from '@tanstack/react-query';
import type { ReactElement } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router-dom';
import { FormError } from '@/components/forms/FormError';
import { Spinner } from '@/components/ui/Spinner';
import { getOrder } from '@/features/cart/api/cartApi';
import { LicenseCard } from '@/features/orders/pages/LicenseCard';

function formatAmount(amount: number, currency: string, locale: string): string {
  return new Intl.NumberFormat(locale, { style: 'currency', currency }).format(amount);
}

export function OrderDetailPage(): ReactElement {
  const { t, i18n } = useTranslation();
  const { orderId = '' } = useParams();
  const queryClient = useQueryClient();

  const order = useQuery({
    queryKey: ['order', orderId],
    queryFn: () => getOrder(orderId),
    retry: false,
  });

  if (order.isPending) {
    return <Spinner />;
  }

  if (order.isError) {
    return (
      <div>
        <FormError error={order.error} />
        <Link className="btn btn-outline-secondary" to="/account/purchases">
          {t('orders.backToPurchases')}
        </Link>
      </div>
    );
  }

  const detail = order.data;

  return (
    <div className="row g-4">
      <div className="col-lg-7">
        <h1 className="h3">{t('orders.detailTitle')}</h1>
        <p className="text-muted">
          {detail.invoiceNo ?? t('orders.noInvoiceYet')} ·{' '}
          {new Date(detail.placedAtUtc).toLocaleString(i18n.language)}
        </p>

        <h2 className="h5">{t('orders.downloadsTitle')}</h2>
        {detail.licenses.length === 0 ? (
          <div className="alert alert-info" role="status">
            {t('orders.licensesPending')}
          </div>
        ) : (
          detail.licenses.map((license) => (
            <LicenseCard
              key={license.id}
              license={license}
              onDownloaded={() => {
                void queryClient.invalidateQueries({ queryKey: ['order', orderId] });
              }}
            />
          ))
        )}
      </div>

      <div className="col-lg-5">
        <div className="card">
          <div className="card-body">
            <h2 className="h6">{t('checkout.summary')}</h2>
            <ul className="list-unstyled">
              {detail.lines.map((line, index) => (
                <li className="d-flex justify-content-between" key={`${line.productTitle}-${index}`}>
                  <span>{line.productTitle}</span>
                  <span>{formatAmount(line.unitPrice.amount, line.unitPrice.currency, i18n.language)}</span>
                </li>
              ))}
            </ul>
            <hr />
            <dl className="row mb-0">
              <dt className="col-7 fw-normal">{t('cart.subtotal')}</dt>
              <dd className="col-5 text-end">
                {formatAmount(detail.totals.subtotal, detail.totals.currency, i18n.language)}
              </dd>
              <dt className="col-7 fw-normal">{t('checkout.tax')}</dt>
              <dd className="col-5 text-end">
                {formatAmount(detail.totals.tax, detail.totals.currency, i18n.language)}
              </dd>
              <dt className="col-7">{t('checkout.total')}</dt>
              <dd className="col-5 text-end fw-bold">
                {formatAmount(detail.totals.grandTotal, detail.totals.currency, i18n.language)}
              </dd>
            </dl>
          </div>
        </div>
      </div>
    </div>
  );
}
