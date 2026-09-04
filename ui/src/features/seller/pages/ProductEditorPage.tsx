import { useQuery, useQueryClient } from '@tanstack/react-query';
import type { ReactElement } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router-dom';
import { FormError } from '@/components/forms/FormError';
import { Spinner } from '@/components/ui/Spinner';
import { FileUploader } from '@/features/seller/components/FileUploader';
import { getMyProduct } from '@/features/seller/api/sellerApi';

/**
 * Attaches files to a product's variants. It reads the owner-scoped view so a
 * draft is manageable before it is ever published.
 */
export function ProductEditorPage(): ReactElement {
  const { t } = useTranslation();
  const { productId = '' } = useParams();
  const queryClient = useQueryClient();

  const product = useQuery({
    queryKey: ['my-product', productId],
    queryFn: () => getMyProduct(productId),
    retry: false,
  });

  if (product.isPending) {
    return <Spinner />;
  }

  if (product.isError) {
    return (
      <div>
        <FormError error={product.error} />
        <Link className="btn btn-outline-secondary" to="/seller/products">
          {t('seller.backToProducts')}
        </Link>
      </div>
    );
  }

  const detail = product.data;

  const refresh = (): void => {
    void queryClient.invalidateQueries({ queryKey: ['my-product', productId] });
    void queryClient.invalidateQueries({ queryKey: ['my-products'] });
  };

  return (
    <div className="row justify-content-center">
      <div className="col-lg-8">
        <h1 className="h3">{detail.title}</h1>
        <p className="text-muted">
          <span className="badge text-bg-secondary me-2">{detail.status}</span>
          {detail.isPublishReady ? t('seller.ready') : t('seller.needsFile')}
        </p>

        <h2 className="h5 mt-4">{t('seller.filesTitle')}</h2>
        <p className="text-muted small">{t('seller.filesHelp')}</p>

        {detail.variants.map((variant) => (
          <FileUploader
            key={variant.id}
            variantId={variant.id}
            variantName={variant.name}
            hasCleanFile={variant.hasCleanFile}
            onScanSettled={refresh}
          />
        ))}

        <Link className="btn btn-outline-secondary mt-3" to="/seller/products">
          {t('seller.backToProducts')}
        </Link>
      </div>
    </div>
  );
}
