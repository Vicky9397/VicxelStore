/**
 * Hand-written until the API's openapi.json is generated into this folder; the
 * shapes mirror the Identity module contracts (spec 06 section 6.5).
 */

export type Role = 'Buyer' | 'Seller' | 'Admin' | 'Moderator' | 'Support';

export interface User {
  id: string;
  email: string;
  emailVerified: boolean;
  displayName: string;
  roles: Role[];
}

export interface AuthResponse {
  accessToken: string;
  expiresInSeconds: number;
  user: User;
}

export interface FieldError {
  field: string;
  message: string;
}

/** Normalized shape of an RFC 9457 problem+json response (spec 06 section 6.4). */
export interface ApiError {
  code: string;
  title: string;
  status: number;
  fieldErrors: FieldError[];
}

export interface Money {
  amount: number;
  currency: string;
}

export interface Category {
  id: number;
  slug: string;
  name: string;
  parentId: number | null;
}

export interface ProductSummary {
  id: string;
  slug: string;
  title: string;
  storeSlug: string;
  storeName: string;
  categorySlug: string;
  fromPrice: Money;
  ratingAvg: number;
  ratingCount: number;
  publishedAtUtc: string | null;
}

export interface Variant {
  id: string;
  name: string;
  price: Money;
  licenseType: string;
  downloadLimit: number;
  isActive: boolean;
  hasCleanFile: boolean;
}

export interface ProductVersion {
  versionNumber: string;
  changelog: string | null;
  releasedAtUtc: string;
}

export interface ProductDetail {
  id: string;
  slug: string;
  title: string;
  description: string | null;
  storeSlug: string;
  storeName: string;
  categorySlug: string;
  ratingAvg: number;
  ratingCount: number;
  publishedAtUtc: string | null;
  variants: Variant[];
  versions: ProductVersion[];
  tags: string[];
}

export interface PageInfo {
  page: number;
  pageSize: number;
  total: number;
}

export interface ProductPage {
  data: ProductSummary[];
  page: PageInfo;
}

export interface SellerProduct {
  id: string;
  slug: string;
  title: string;
  status: string;
  variantCount: number;
  isPublishReady: boolean;
  updatedAtUtc: string;
}

export interface Store {
  id: string;
  slug: string;
  name: string;
  about: string | null;
  logoUrl: string | null;
  bannerUrl: string | null;
  status: string;
}

export interface SellerProfile {
  kycStatus: string;
  legalName: string | null;
  taxIdType: string | null;
  taxId: string | null;
  bankVerified: boolean;
  isPublishReady: boolean;
}

export interface MyStore {
  store: Store;
  profile: SellerProfile;
}

export interface UploadSession {
  uploadId: string;
  partSizeBytes: number;
  totalParts: number;
  missingParts: number[];
}

export interface ProductFile {
  id: string;
  fileName: string;
  sizeBytes: number;
  scanStatus: string;
  isDownloadable: boolean;
}

export interface SellerProductDetail {
  id: string;
  slug: string;
  title: string;
  description: string | null;
  categorySlug: string;
  status: string;
  isPublishReady: boolean;
  variants: Variant[];
  versions: ProductVersion[];
}

export interface CartItem {
  variantId: string;
  productSlug: string;
  productTitle: string;
  variantName: string;
  price: Money;
  savedForLater: boolean;
  isAvailable: boolean;
}

export interface Cart {
  id: string;
  items: CartItem[];
  subtotal: Money;
  itemCount: number;
}

export interface Totals {
  subtotal: number;
  discount: number;
  tax: number;
  grandTotal: number;
  currency: string;
}

export interface Quote {
  totals: Totals;
  items: CartItem[];
}

export interface PaymentIntent {
  provider: string;
  clientSecret: string;
  instructions: string | null;
}

export interface CheckoutResult {
  orderPublicId: string;
  paymentIntent: PaymentIntent;
  totals: Totals;
}

export interface LicenseFile {
  id: string;
  fileName: string;
  sizeBytes: number;
}

export interface License {
  id: string;
  productTitle: string;
  variantName: string;
  downloadLimit: number;
  downloadsUsed: number;
  issuedAtUtc: string;
  files: LicenseFile[];
}

export interface OrderLine {
  productTitle: string;
  variantName: string;
  unitPrice: Money;
}

export interface Order {
  id: string;
  status: string;
  invoiceNo: string | null;
  totals: Totals;
  placedAtUtc: string;
  lines: OrderLine[];
  licenses: License[];
}

export interface DownloadUrl {
  url: string;
  expiresInSeconds: number;
}

export interface Wallet {
  pending: number;
  available: number;
  inTransit: number;
  currency: string;
}
