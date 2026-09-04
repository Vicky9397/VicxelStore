import { apiRequest } from '@/lib/apiClient';
import type {
  MyStore,
  ProductFile,
  SellerProduct,
  SellerProductDetail,
  SellerProfile,
  Store,
  UploadSession,
} from '@/types/api';

export interface CreateStorePayload {
  slug: string;
  name: string;
  about?: string | undefined;
}

export interface VariantPayload {
  name: string;
  price: number;
  currency: string;
  licenseType: string;
  downloadLimit: number;
}

export interface CreateProductPayload {
  title: string;
  categorySlug: string;
  description?: string | undefined;
  tags?: string[] | undefined;
  variants: VariantPayload[];
}

export function getMyStore(): Promise<MyStore> {
  return apiRequest('/stores/mine');
}

export function createStore(payload: CreateStorePayload): Promise<Store> {
  return apiRequest('/stores', { method: 'POST', body: payload });
}

export function submitKyc(storeId: string, legalName: string): Promise<SellerProfile> {
  return apiRequest(`/stores/${storeId}/kyc`, { method: 'POST', body: { legalName } });
}

export function updateTaxInfo(
  storeId: string,
  taxIdType: string,
  taxId: string,
): Promise<SellerProfile> {
  return apiRequest(`/stores/${storeId}/tax-info`, { method: 'PUT', body: { taxIdType, taxId } });
}

export function updateBankInfo(storeId: string, bankRef: string): Promise<SellerProfile> {
  return apiRequest(`/stores/${storeId}/bank-info`, { method: 'PUT', body: { bankRef } });
}

export function listMyProducts(): Promise<SellerProduct[]> {
  return apiRequest('/products/mine');
}

/** The owner's view of their own product, available in any status. */
export function getMyProduct(productId: string): Promise<SellerProductDetail> {
  return apiRequest(`/products/${productId}/manage`);
}

export function createProduct(payload: CreateProductPayload): Promise<SellerProduct> {
  return apiRequest('/products', { method: 'POST', body: payload });
}

export function submitProduct(productId: string): Promise<SellerProduct> {
  return apiRequest(`/products/${productId}/submit`, { method: 'POST' });
}

export function publishProduct(productId: string): Promise<SellerProduct> {
  return apiRequest(`/products/${productId}/publish`, { method: 'POST' });
}

export function unpublishProduct(productId: string): Promise<SellerProduct> {
  return apiRequest(`/products/${productId}/unpublish`, { method: 'POST' });
}

export function initUpload(
  variantId: string,
  fileName: string,
  sizeBytes: number,
  checksum: string,
): Promise<UploadSession> {
  return apiRequest('/files/init', {
    method: 'POST',
    body: { variantId, fileName, sizeBytes, checksum },
  });
}

export function completeUpload(uploadId: string): Promise<ProductFile> {
  return apiRequest(`/files/${uploadId}/complete`, { method: 'POST' });
}

export function getScanStatus(fileId: string): Promise<ProductFile> {
  return apiRequest(`/files/${fileId}/scan-status`);
}
