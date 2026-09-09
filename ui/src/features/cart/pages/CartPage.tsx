import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { ReactElement } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { FormError } from '@/components/forms/FormError';
import { Spinner } from '@/components/ui/Spinner';
import { getCart, removeFromCart, setSavedForLater } from '@/features/cart/api/cartApi';
import { formatMoney } from '@/lib/money';

export function CartPage(): ReactElement {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();

  const cart = useQuery({ queryKey: ['cart'], queryFn: getCart });

  const invalidate = (): void => {
    void queryClient.invalidateQueries({ queryKey: ['cart'] });
  };

  const remove = useMutation({ mutationFn: removeFromCart, onSuccess: invalidate });
  const save = useMutation({
    mutationFn: ({ variantId, saved }: { variantId: string; saved: boolean }) =>
      setSavedForLater(variantId, saved),
    onSuccess: invalidate,
  });

  if (cart.isPending) {
    return <Spinner />;
  }

  if (cart.isError) {
    return <FormError error={cart.error} />;
  }

  const payable = cart.data.items.filter((item) => !item.savedForLater);
  const saved = cart.data.items.filter((item) => item.savedForLater);
  const hasUnavailable = payable.some((item) => !item.isAvailable);

  return (
    <div className="row g-4">
      <div className="col-lg-8">
        <h1 className="h3">{t('cart.title')}</h1>

        <FormError error={remove.error ?? save.error} />

        {payable.length === 0 ? (
          <div className="alert alert-info" role="status">
            {t('cart.empty')} <Link to="/search">{t('catalog.browseTitle')}</Link>
          </div>
        ) : (
          <ul className="list-group mb-4">
            {payable.map((item) => (
              <li className="list-group-item d-flex justify-content-between align-items-start" key={item.variantId}>
                <div>
                  <Link to={`/p/${item.productSlug}`}>{item.productTitle}</Link>
                  <div className="text-muted small">{item.variantName}</div>
                  {!item.isAvailable ? (
                    <div className="text-danger small">{t('cart.unavailable')}</div>
                  ) : null}
                </div>
                <div className="text-end">
                  <div className="fw-semibold">{formatMoney(item.price, i18n.language)}</div>
                  <button
                    type="button"
                    className="btn btn-link btn-sm p-0"
                    onClick={() => save.mutate({ variantId: item.variantId, saved: true })}
                  >
                    {t('cart.saveForLater')}
                  </button>
                  {' · '}
                  <button
                    type="button"
                    className="btn btn-link btn-sm p-0 text-danger"
                    onClick={() => remove.mutate(item.variantId)}
                  >
                    {t('cart.remove')}
                  </button>
                </div>
              </li>
            ))}
          </ul>
        )}

        {saved.length > 0 ? (
          <>
            <h2 className="h5">{t('cart.savedTitle')}</h2>
            <ul className="list-group">
              {saved.map((item) => (
                <li className="list-group-item d-flex justify-content-between" key={item.variantId}>
                  <div>
                    <Link to={`/p/${item.productSlug}`}>{item.productTitle}</Link>
                    <div className="text-muted small">{item.variantName}</div>
                  </div>
                  <button
                    type="button"
                    className="btn btn-link btn-sm p-0"
                    onClick={() => save.mutate({ variantId: item.variantId, saved: false })}
                  >
                    {t('cart.moveToCart')}
                  </button>
                </li>
              ))}
            </ul>
          </>
        ) : null}
      </div>

      <div className="col-lg-4">
        <div className="card">
          <div className="card-body">
            <h2 className="h6">{t('cart.summary')}</h2>
            <div className="d-flex justify-content-between">
              <span>{t('cart.subtotal')}</span>
              <span className="fw-semibold">{formatMoney(cart.data.subtotal, i18n.language)}</span>
            </div>
            <p className="text-muted small mt-2 mb-3">{t('cart.taxAtCheckout')}</p>
            <Link
              className={payable.length === 0 || hasUnavailable ? 'btn btn-primary w-100 disabled' : 'btn btn-primary w-100'}
              to="/checkout"
              aria-disabled={payable.length === 0 || hasUnavailable}
            >
              {t('cart.checkout')}
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}
