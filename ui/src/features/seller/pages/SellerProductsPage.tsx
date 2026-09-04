import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { ReactElement } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { FormError } from '@/components/forms/FormError';
import { Spinner } from '@/components/ui/Spinner';
import {
  listMyProducts,
  publishProduct,
  submitProduct,
  unpublishProduct,
} from '@/features/seller/api/sellerApi';

/**
 * The seller's catalog with the lifecycle actions available in each state. The
 * server remains the authority: these buttons mirror what it will allow, and a
 * refused action surfaces its reason rather than being hidden.
 */
export function SellerProductsPage(): ReactElement {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const products = useQuery({ queryKey: ['my-products'], queryFn: listMyProducts });

  const invalidate = (): void => {
    void queryClient.invalidateQueries({ queryKey: ['my-products'] });
  };

  const submit = useMutation({ mutationFn: submitProduct, onSuccess: invalidate });
  const publish = useMutation({ mutationFn: publishProduct, onSuccess: invalidate });
  const unpublish = useMutation({ mutationFn: unpublishProduct, onSuccess: invalidate });

  if (products.isPending) {
    return <Spinner />;
  }

  if (products.isError) {
    return <FormError error={products.error} />;
  }

  return (
    <div>
      <div className="d-flex justify-content-between align-items-center mb-3">
        <h1 className="h3 mb-0">{t('seller.productsTitle')}</h1>
        <Link className="btn btn-primary" to="/seller/products/new">
          {t('seller.newProduct')}
        </Link>
      </div>

      <FormError error={submit.error ?? publish.error ?? unpublish.error} />

      {products.data.length === 0 ? (
        <div className="alert alert-info" role="status">
          {t('seller.noProducts')}
        </div>
      ) : (
        <div className="table-responsive">
          <table className="table align-middle">
            <thead>
              <tr>
                <th scope="col">{t('seller.colTitle')}</th>
                <th scope="col">{t('seller.colStatus')}</th>
                <th scope="col">{t('seller.colVariants')}</th>
                <th scope="col">{t('seller.colReady')}</th>
                <th scope="col">{t('seller.colActions')}</th>
              </tr>
            </thead>
            <tbody>
              {products.data.map((product) => (
                <tr key={product.id}>
                  <td>{product.title}</td>
                  <td>
                    <span className="badge text-bg-secondary">{product.status}</span>
                  </td>
                  <td>{product.variantCount}</td>
                  <td>
                    {product.isPublishReady ? (
                      <span className="badge text-bg-success">{t('seller.ready')}</span>
                    ) : (
                      <span className="badge text-bg-warning">{t('seller.needsFile')}</span>
                    )}
                  </td>
                  <td className="d-flex gap-2">
                    <Link
                      className="btn btn-sm btn-outline-secondary"
                      to={`/seller/products/${product.id}`}
                    >
                      {t('seller.manageFiles')}
                    </Link>
                    {product.status === 'Draft' || product.status === 'Rejected' ? (
                      <button
                        type="button"
                        className="btn btn-sm btn-outline-primary"
                        disabled={submit.isPending}
                        onClick={() => submit.mutate(product.id)}
                      >
                        {t('seller.submitForReview')}
                      </button>
                    ) : null}
                    {product.status === 'Approved' ? (
                      <button
                        type="button"
                        className="btn btn-sm btn-primary"
                        disabled={publish.isPending}
                        onClick={() => publish.mutate(product.id)}
                      >
                        {t('seller.publish')}
                      </button>
                    ) : null}
                    {product.status === 'Published' ? (
                      <button
                        type="button"
                        className="btn btn-sm btn-outline-secondary"
                        disabled={unpublish.isPending}
                        onClick={() => unpublish.mutate(product.id)}
                      >
                        {t('seller.unpublish')}
                      </button>
                    ) : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
