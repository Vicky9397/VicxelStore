import { useQuery } from '@tanstack/react-query';
import type { ReactElement } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { FormError } from '@/components/forms/FormError';
import { Spinner } from '@/components/ui/Spinner';
import { listOrders } from '@/features/cart/api/cartApi';

function formatAmount(amount: number, currency: string, locale: string): string {
  return new Intl.NumberFormat(locale, { style: 'currency', currency }).format(amount);
}

export function PurchasesPage(): ReactElement {
  const { t, i18n } = useTranslation();
  const orders = useQuery({ queryKey: ['orders'], queryFn: listOrders });

  if (orders.isPending) {
    return <Spinner />;
  }

  if (orders.isError) {
    return <FormError error={orders.error} />;
  }

  if (orders.data.length === 0) {
    return (
      <div>
        <h1 className="h3">{t('orders.title')}</h1>
        <div className="alert alert-info" role="status">
          {t('orders.empty')} <Link to="/search">{t('catalog.browseTitle')}</Link>
        </div>
      </div>
    );
  }

  return (
    <div>
      <h1 className="h3 mb-3">{t('orders.title')}</h1>

      <div className="table-responsive">
        <table className="table align-middle">
          <thead>
            <tr>
              <th scope="col">{t('orders.colPlaced')}</th>
              <th scope="col">{t('orders.colInvoice')}</th>
              <th scope="col">{t('orders.colItems')}</th>
              <th scope="col">{t('orders.colTotal')}</th>
              <th scope="col">{t('orders.colStatus')}</th>
              <th scope="col">{t('orders.colActions')}</th>
            </tr>
          </thead>
          <tbody>
            {orders.data.map((order) => (
              <tr key={order.id}>
                <td>{new Date(order.placedAtUtc).toLocaleDateString(i18n.language)}</td>
                <td className="text-muted small">{order.invoiceNo ?? '—'}</td>
                <td>{order.lines.map((line) => line.productTitle).join(', ')}</td>
                <td>{formatAmount(order.totals.grandTotal, order.totals.currency, i18n.language)}</td>
                <td>
                  <span
                    className={
                      order.status === 'Paid' || order.status === 'Completed'
                        ? 'badge text-bg-success'
                        : 'badge text-bg-secondary'
                    }
                  >
                    {order.status}
                  </span>
                </td>
                <td>
                  <Link className="btn btn-sm btn-outline-secondary" to={`/account/orders/${order.id}`}>
                    {t('orders.view')}
                  </Link>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
