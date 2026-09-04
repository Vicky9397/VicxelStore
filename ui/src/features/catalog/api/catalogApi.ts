import { apiRequest } from '@/lib/apiClient';
import type { Category, ProductDetail, ProductPage } from '@/types/api';

export interface SearchParams {
  q?: string | undefined;
  category?: string | undefined;
  minPrice?: number | undefined;
  maxPrice?: number | undefined;
  licenseType?: string | undefined;
  sort?: string | undefined;
  page?: number | undefined;
}

/** Mirrors the API's filter syntax from spec 06 section 6.3. */
function toQueryString(params: SearchParams): string {
  const search = new URLSearchParams();
  if (params.q) search.set('q', params.q);
  if (params.category) search.set('filter[category]', params.category);
  if (params.minPrice !== undefined) search.set('filter[price][gte]', String(params.minPrice));
  if (params.maxPrice !== undefined) search.set('filter[price][lte]', String(params.maxPrice));
  if (params.licenseType) search.set('filter[license]', params.licenseType);
  if (params.sort) search.set('sort', params.sort);
  if (params.page !== undefined) search.set('page', String(params.page));
  const query = search.toString();
  return query === '' ? '' : `?${query}`;
}

export function searchProducts(params: SearchParams): Promise<ProductPage> {
  return apiRequest(`/products${toQueryString(params)}`);
}

export function getProduct(slug: string): Promise<ProductDetail> {
  return apiRequest(`/products/${encodeURIComponent(slug)}`);
}

export function listCategories(): Promise<Category[]> {
  return apiRequest('/categories');
}
