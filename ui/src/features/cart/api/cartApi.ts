import { apiRequest } from '@/lib/apiClient';
import type { Cart, CheckoutResult, DownloadUrl, Order, Quote } from '@/types/api';

export function getCart(): Promise<Cart> {
  return apiRequest('/cart');
}

export function addToCart(variantId: string): Promise<Cart> {
  return apiRequest('/cart/items', { method: 'POST', body: { variantId } });
}

export function removeFromCart(variantId: string): Promise<Cart> {
  return apiRequest(`/cart/items/${variantId}`, { method: 'DELETE' });
}

export function setSavedForLater(variantId: string, savedForLater: boolean): Promise<Cart> {
  return apiRequest(`/cart/items/${variantId}/save-for-later`, {
    method: 'POST',
    body: { savedForLater },
  });
}

export function getQuote(billingCountry: string): Promise<Quote> {
  return apiRequest('/checkout/quote', { method: 'POST', body: { billingCountry } });
}

export interface ConfirmPayload {
  billingCountry: string;
  paymentMethod: string;
  returnUrl?: string | undefined;
  expectedGrandTotal?: number | undefined;
}

/**
 * Confirms checkout. The idempotency key is generated once per attempt and
 * reused across retries, so a dropped response cannot become a second order.
 */
export function confirmCheckout(
  payload: ConfirmPayload,
  idempotencyKey: string,
): Promise<CheckoutResult> {
  return apiRequest('/checkout/confirm', {
    method: 'POST',
    body: payload,
    idempotencyKey,
  });
}

/** Development-only stand-in for completing the charge at a hosted gateway. */
export function confirmSandboxPayment(
  intentId: string,
  amount: number,
  currency: string,
): Promise<void> {
  return apiRequest('/dev/payments/confirm', {
    method: 'POST',
    body: { intentId, amount, currency },
  });
}

export function listOrders(): Promise<Order[]> {
  return apiRequest('/orders');
}

export function getOrder(orderId: string): Promise<Order> {
  return apiRequest(`/orders/${orderId}`);
}

export function requestDownload(licenseId: string, fileId: string): Promise<DownloadUrl> {
  return apiRequest(`/licenses/${licenseId}/download?fileId=${encodeURIComponent(fileId)}`);
}
