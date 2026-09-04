import type { ReactElement } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { formatMoney } from '@/lib/money';
import type { ProductSummary } from '@/types/api';

export function ProductCard({ product }: { product: ProductSummary }): ReactElement {
  const { t, i18n } = useTranslation();

  return (
    <div className="card h-100">
      <div className="card-body d-flex flex-column">
        <h2 className="h6 card-title">
          <Link to={`/p/${product.slug}`}>{product.title}</Link>
        </h2>
        <p className="text-muted small mb-2">
          <Link to={`/s/${product.storeSlug}`}>{product.storeName}</Link>
        </p>
        <p className="mb-2">
          {product.ratingCount > 0 ? (
            <span aria-label={t('catalog.ratingLabel', { rating: product.ratingAvg })}>
              {product.ratingAvg.toFixed(1)} ({product.ratingCount})
            </span>
          ) : (
            <span className="text-muted small">{t('catalog.noReviews')}</span>
          )}
        </p>
        <p className="mt-auto mb-0 fw-semibold">
          {t('catalog.fromPrice', { price: formatMoney(product.fromPrice, i18n.language) })}
        </p>
      </div>
    </div>
  );
}
