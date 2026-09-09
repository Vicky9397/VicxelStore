import { useQuery } from '@tanstack/react-query';
import type { ReactElement } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { Spinner } from '@/components/ui/Spinner';
import { listOrders } from '@/features/cart/api/cartApi';

/**
 * Where the buyer lands after paying. Capture is confirmed by the provider's
 * webhook, not by this page, so the most recent order may still read Pending for
 * a moment. The page polls briefly rather than claiming success the client
 * cannot actually verify.
 */
export function CheckoutReturnPage(): ReactElement {
  const { t } = useTranslation();

  const orders = useQuery({
    queryKey: ['orders'],
    queryFn: listOrders,
    refetchInterval: (query) => {
      const latest = query.state.data?.[0];
      return latest !== undefined && latest.status === 'Pending' ? 2000 : false;
    },
  });

  if (orders.isPending) {
    return <Spinner />;
  }

  const latest = orders.data?.[0];
  const settled = latest !== undefined && latest.status !== 'Pending';

  return (
    <div className="row justify-content-center">
      <div className="col-lg-6 text-center py-4">
        {settled ? (
          <>
            <h1 className="h3">{t('checkout.thanksTitle')}</h1>
            <p className="text-muted">{t('checkout.thanksBody')}</p>
            <Link className="btn btn-primary" to={`/account/orders/${latest.id}`}>
              {t('orders.viewDownloads')}
            </Link>
          </>
        ) : (
          <>
            <h1 className="h3">{t('checkout.confirmingTitle')}</h1>
            <p className="text-muted">{t('checkout.confirmingBody')}</p>
            <Spinner />
          </>
        )}
      </div>
    </div>
  );
}
