import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { Spinner } from '@/components/ui/Spinner';
import { addToCart } from '@/features/cart/api/cartApi';
import { getProduct } from '@/features/catalog/api/catalogApi';
import { useAuthStore } from '@/features/auth/store';
import { FormError } from '@/components/forms/FormError';
import { ApiRequestError } from '@/lib/apiClient';
import { formatMoney } from '@/lib/money';

export function ProductDetailPage(): ReactElement {
  const { t, i18n } = useTranslation();
  const { slug = '' } = useParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const isSignedIn = useAuthStore((state) => state.status === 'authenticated');
  const [selectedVariantId, setSelectedVariantId] = useState<string | null>(null);

  const addItem = useMutation({
    mutationFn: (variantId: string) => addToCart(variantId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['cart'] });
      navigate('/cart');
    },
  });

  const product = useQuery({
    queryKey: ['product', slug],
    queryFn: () => getProduct(slug),
    retry: false,
  });

  if (product.isPending) {
    return <Spinner />;
  }

  if (product.isError) {
    const notFound = product.error instanceof ApiRequestError && product.error.status === 404;
    return (
      <div className="text-center py-5">
        <h1 className="h4">{notFound ? t('errors.notFoundTitle') : t('errors.generic')}</h1>
        <Link className="btn btn-outline-primary mt-3" to="/search">
          {t('catalog.browseTitle')}
        </Link>
      </div>
    );
  }

  const detail = product.data;
  const selectedVariant =
    detail.variants.find((v) => v.id === selectedVariantId) ?? detail.variants[0];

  return (
    <article className="row g-4">
      <div className="col-lg-8">
        <h1 className="h3">{detail.title}</h1>
        <p className="text-muted">
          {t('catalog.byStore')}{' '}
          <Link to={`/s/${detail.storeSlug}`}>{detail.storeName}</Link>
        </p>

        {detail.description !== null ? (
          <p style={{ whiteSpace: 'pre-wrap' }}>{detail.description}</p>
        ) : null}

        {detail.tags.length > 0 ? (
          <p className="mb-4">
            {detail.tags.map((tag) => (
              <span className="badge text-bg-secondary me-1" key={tag}>
                {tag}
              </span>
            ))}
          </p>
        ) : null}

        <h2 className="h5">{t('catalog.versionsTitle')}</h2>
        <ul className="list-unstyled">
          {detail.versions.map((version) => (
            <li className="mb-2" key={version.versionNumber}>
              <strong>{version.versionNumber}</strong>
              {version.changelog !== null ? <span className="text-muted"> — {version.changelog}</span> : null}
            </li>
          ))}
        </ul>
      </div>

      <div className="col-lg-4">
        <div className="card">
          <div className="card-body">
            <h2 className="h6">{t('catalog.chooseLicense')}</h2>

            {detail.variants.map((variant) => (
              <div className="form-check mb-2" key={variant.id}>
                <input
                  className="form-check-input"
                  type="radio"
                  name="variant"
                  id={`variant-${variant.id}`}
                  checked={selectedVariant?.id === variant.id}
                  onChange={() => setSelectedVariantId(variant.id)}
                />
                <label className="form-check-label" htmlFor={`variant-${variant.id}`}>
                  <span className="d-block">{variant.name}</span>
                  <span className="text-muted small">
                    {formatMoney(variant.price, i18n.language)} ·{' '}
                    {t('catalog.downloadLimit', { count: variant.downloadLimit })}
                  </span>
                </label>
              </div>
            ))}

            <FormError error={addItem.error} />

            <button
              className="btn btn-primary w-100 mt-3"
              type="button"
              disabled={selectedVariant === undefined || addItem.isPending}
              onClick={() => {
                if (!isSignedIn) {
                  navigate('/login', { state: { from: `/p/${slug}` } });
                  return;
                }

                if (selectedVariant !== undefined) {
                  addItem.mutate(selectedVariant.id);
                }
              }}
            >
              {t('catalog.addToCart')}
            </button>
          </div>
        </div>
      </div>
    </article>
  );
}
