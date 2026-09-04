import { useQuery } from '@tanstack/react-query';
import type { ReactElement } from 'react';
import { useTranslation } from 'react-i18next';
import { useParams } from 'react-router-dom';
import { Spinner } from '@/components/ui/Spinner';
import { searchProducts } from '@/features/catalog/api/catalogApi';
import { ProductCard } from '@/features/catalog/components/ProductCard';
import { apiRequest } from '@/lib/apiClient';
import type { Store } from '@/types/api';

export function StorePage(): ReactElement {
  const { t } = useTranslation();
  const { slug = '' } = useParams();

  const store = useQuery({
    queryKey: ['store', slug],
    queryFn: () => apiRequest<Store>(`/stores/${encodeURIComponent(slug)}`),
    retry: false,
  });

  const products = useQuery({
    queryKey: ['store-products', slug],
    queryFn: () => searchProducts({ q: undefined }),
    enabled: store.isSuccess,
  });

  if (store.isPending) {
    return <Spinner />;
  }

  if (store.isError) {
    return (
      <div className="text-center py-5">
        <h1 className="h4">{t('errors.notFoundTitle')}</h1>
      </div>
    );
  }

  const storeProducts = (products.data?.data ?? []).filter((p) => p.storeSlug === slug);

  return (
    <div>
      <header className="mb-4">
        <h1 className="h3">{store.data.name}</h1>
        {store.data.about !== null ? <p className="text-muted">{store.data.about}</p> : null}
      </header>

      {products.isPending ? <Spinner /> : null}

      {storeProducts.length === 0 && !products.isPending ? (
        <div className="alert alert-info" role="status">
          {t('catalog.storeEmpty')}
        </div>
      ) : null}

      <div className="row row-cols-1 row-cols-md-3 g-3">
        {storeProducts.map((product) => (
          <div className="col" key={product.id}>
            <ProductCard product={product} />
          </div>
        ))}
      </div>
    </div>
  );
}
