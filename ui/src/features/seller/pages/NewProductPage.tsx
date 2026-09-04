import { useMutation, useQuery } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { FormError } from '@/components/forms/FormError';
import { listCategories } from '@/features/catalog/api/catalogApi';
import { createProduct, type VariantPayload } from '@/features/seller/api/sellerApi';

const emptyVariant: VariantPayload = {
  name: '',
  price: 0,
  currency: 'INR',
  licenseType: 'Personal',
  downloadLimit: 5,
};

/** Creates a draft with its initial variants; files are attached afterwards. */
export function NewProductPage(): ReactElement {
  const { t } = useTranslation();
  const navigate = useNavigate();

  const [title, setTitle] = useState('');
  const [categorySlug, setCategorySlug] = useState('');
  const [description, setDescription] = useState('');
  const [tags, setTags] = useState('');
  const [variants, setVariants] = useState<VariantPayload[]>([{ ...emptyVariant }]);

  const categories = useQuery({ queryKey: ['categories'], queryFn: listCategories });

  const mutation = useMutation({
    mutationFn: () =>
      createProduct({
        title,
        categorySlug,
        description: description || undefined,
        tags: tags
          .split(',')
          .map((tag) => tag.trim())
          .filter((tag) => tag.length > 0),
        variants,
      }),
    onSuccess: (product) => navigate(`/seller/products/${product.id}`),
  });

  const updateVariant = (index: number, patch: Partial<VariantPayload>): void => {
    setVariants((current) =>
      current.map((variant, i) => (i === index ? { ...variant, ...patch } : variant)),
    );
  };

  return (
    <div className="row justify-content-center">
      <div className="col-lg-8">
        <h1 className="h3">{t('seller.newProductTitle')}</h1>
        <p className="text-muted">{t('seller.newProductSubtitle')}</p>

        <FormError error={mutation.error} />

        <form
          onSubmit={(event) => {
            event.preventDefault();
            mutation.mutate();
          }}
        >
          <div className="mb-3">
            <label className="form-label" htmlFor="product-title">
              {t('seller.productTitle')}
            </label>
            <input
              id="product-title"
              className="form-control"
              value={title}
              onChange={(event) => setTitle(event.target.value)}
              required
              minLength={3}
              maxLength={200}
            />
          </div>

          <div className="mb-3">
            <label className="form-label" htmlFor="product-category">
              {t('catalog.categoryLabel')}
            </label>
            <select
              id="product-category"
              className="form-select"
              value={categorySlug}
              onChange={(event) => setCategorySlug(event.target.value)}
              required
            >
              <option value="">{t('seller.chooseCategory')}</option>
              {(categories.data ?? []).map((category) => (
                <option key={category.id} value={category.slug}>
                  {category.name}
                </option>
              ))}
            </select>
          </div>

          <div className="mb-3">
            <label className="form-label" htmlFor="product-description">
              {t('seller.productDescription')}
            </label>
            <textarea
              id="product-description"
              className="form-control"
              rows={5}
              value={description}
              onChange={(event) => setDescription(event.target.value)}
            />
          </div>

          <div className="mb-4">
            <label className="form-label" htmlFor="product-tags">
              {t('seller.productTags')}
            </label>
            <input
              id="product-tags"
              className="form-control"
              value={tags}
              onChange={(event) => setTags(event.target.value)}
              aria-describedby="product-tags-help"
            />
            <div className="form-text" id="product-tags-help">
              {t('seller.productTagsHelp')}
            </div>
          </div>

          <h2 className="h5">{t('seller.variantsTitle')}</h2>
          <p className="text-muted small">{t('seller.variantsHelp')}</p>

          {variants.map((variant, index) => (
            <fieldset className="border rounded p-3 mb-3" key={index}>
              <legend className="h6">{t('seller.variantN', { n: index + 1 })}</legend>
              <div className="row g-2">
                <div className="col-md-4">
                  <label className="form-label" htmlFor={`variant-name-${index}`}>
                    {t('seller.variantName')}
                  </label>
                  <input
                    id={`variant-name-${index}`}
                    className="form-control"
                    value={variant.name}
                    onChange={(event) => updateVariant(index, { name: event.target.value })}
                    required
                  />
                </div>
                <div className="col-md-3">
                  <label className="form-label" htmlFor={`variant-price-${index}`}>
                    {t('seller.variantPrice')}
                  </label>
                  <input
                    id={`variant-price-${index}`}
                    className="form-control"
                    type="number"
                    min="1"
                    step="0.01"
                    value={variant.price}
                    onChange={(event) =>
                      updateVariant(index, { price: Number(event.target.value) })
                    }
                    required
                  />
                </div>
                <div className="col-md-2">
                  <label className="form-label" htmlFor={`variant-currency-${index}`}>
                    {t('seller.variantCurrency')}
                  </label>
                  <select
                    id={`variant-currency-${index}`}
                    className="form-select"
                    value={variant.currency}
                    onChange={(event) => updateVariant(index, { currency: event.target.value })}
                  >
                    <option value="INR">INR</option>
                    <option value="USD">USD</option>
                    <option value="EUR">EUR</option>
                    <option value="GBP">GBP</option>
                  </select>
                </div>
                <div className="col-md-3">
                  <label className="form-label" htmlFor={`variant-license-${index}`}>
                    {t('seller.variantLicense')}
                  </label>
                  <select
                    id={`variant-license-${index}`}
                    className="form-select"
                    value={variant.licenseType}
                    onChange={(event) => updateVariant(index, { licenseType: event.target.value })}
                  >
                    <option value="Personal">{t('seller.licensePersonal')}</option>
                    <option value="Commercial">{t('seller.licenseCommercial')}</option>
                    <option value="Extended">{t('seller.licenseExtended')}</option>
                  </select>
                </div>
              </div>
            </fieldset>
          ))}

          <button
            className="btn btn-outline-secondary btn-sm mb-4"
            type="button"
            onClick={() => setVariants((current) => [...current, { ...emptyVariant }])}
          >
            {t('seller.addVariant')}
          </button>

          <div>
            <button className="btn btn-primary" type="submit" disabled={mutation.isPending}>
              {t('seller.createDraft')}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
