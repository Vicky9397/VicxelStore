import { useQuery } from '@tanstack/react-query';
import type { ReactElement } from 'react';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router-dom';
import { Spinner } from '@/components/ui/Spinner';
import { listCategories, searchProducts } from '@/features/catalog/api/catalogApi';
import { ProductCard } from '@/features/catalog/components/ProductCard';

/**
 * Filters and sort live in the query string so a result set is linkable and
 * survives a reload (spec 07 section 7.2).
 */
export function SearchPage(): ReactElement {
  const { t } = useTranslation();
  const [searchParams, setSearchParams] = useSearchParams();

  const query = searchParams.get('q') ?? '';
  const category = searchParams.get('category') ?? '';
  const sort = searchParams.get('sort') ?? '';
  const page = Number(searchParams.get('page') ?? '1');

  const categories = useQuery({ queryKey: ['categories'], queryFn: listCategories });

  const products = useQuery({
    queryKey: ['products', { query, category, sort, page }],
    queryFn: () =>
      searchProducts({
        q: query || undefined,
        category: category || undefined,
        sort: sort || undefined,
        page,
      }),
  });

  const setParam = (key: string, value: string): void => {
    const next = new URLSearchParams(searchParams);
    if (value === '') {
      next.delete(key);
    } else {
      next.set(key, value);
    }
    next.delete('page');
    setSearchParams(next);
  };

  return (
    <div>
      <h1 className="h3 mb-3">{t('catalog.browseTitle')}</h1>

      <div className="row g-2 mb-4">
        <div className="col-md-5">
          <label className="form-label" htmlFor="search-q">
            {t('catalog.searchLabel')}
          </label>
          <input
            id="search-q"
            className="form-control"
            type="search"
            defaultValue={query}
            onBlur={(event) => setParam('q', event.target.value.trim())}
          />
        </div>
        <div className="col-md-4">
          <label className="form-label" htmlFor="search-category">
            {t('catalog.categoryLabel')}
          </label>
          <select
            id="search-category"
            className="form-select"
            value={category}
            onChange={(event) => setParam('category', event.target.value)}
          >
            <option value="">{t('catalog.allCategories')}</option>
            {(categories.data ?? []).map((c) => (
              <option key={c.id} value={c.slug}>
                {c.name}
              </option>
            ))}
          </select>
        </div>
        <div className="col-md-3">
          <label className="form-label" htmlFor="search-sort">
            {t('catalog.sortLabel')}
          </label>
          <select
            id="search-sort"
            className="form-select"
            value={sort}
            onChange={(event) => setParam('sort', event.target.value)}
          >
            <option value="">{t('catalog.sortNewest')}</option>
            <option value="price">{t('catalog.sortPriceAsc')}</option>
            <option value="-price">{t('catalog.sortPriceDesc')}</option>
            <option value="rating">{t('catalog.sortRating')}</option>
          </select>
        </div>
      </div>

      {products.isPending ? <Spinner /> : null}

      {products.isError ? (
        <div className="alert alert-danger" role="alert">
          {t('errors.generic')}
        </div>
      ) : null}

      {products.data?.data.length === 0 ? (
        <div className="alert alert-info" role="status">
          {t('catalog.empty')}
        </div>
      ) : null}

      <div className="row row-cols-1 row-cols-md-3 g-3">
        {(products.data?.data ?? []).map((product) => (
          <div className="col" key={product.id}>
            <ProductCard product={product} />
          </div>
        ))}
      </div>

      {products.data !== undefined && products.data.page.total > products.data.page.pageSize ? (
        <nav className="mt-4" aria-label={t('catalog.pagination')}>
          <div className="d-flex gap-2 align-items-center">
            <button
              type="button"
              className="btn btn-outline-secondary btn-sm"
              disabled={page <= 1}
              onClick={() => setParam('page', String(page - 1))}
            >
              {t('catalog.previous')}
            </button>
            <span className="small text-muted">
              {t('catalog.pageOf', {
                page: products.data.page.page,
                total: Math.ceil(products.data.page.total / products.data.page.pageSize),
              })}
            </span>
            <button
              type="button"
              className="btn btn-outline-secondary btn-sm"
              disabled={page * products.data.page.pageSize >= products.data.page.total}
              onClick={() => setParam('page', String(page + 1))}
            >
              {t('catalog.next')}
            </button>
          </div>
        </nav>
      ) : null}
    </div>
  );
}
