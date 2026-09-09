import { useMutation, useQuery } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { FormError } from '@/components/forms/FormError';
import { Spinner } from '@/components/ui/Spinner';
import {
  confirmCheckout,
  confirmSandboxPayment,
  getQuote,
} from '@/features/cart/api/cartApi';
import type { CheckoutResult } from '@/types/api';

const COUNTRIES = ['IN', 'GB', 'DE', 'FR', 'US'] as const;

function formatAmount(amount: number, currency: string, locale: string): string {
  return new Intl.NumberFormat(locale, { style: 'currency', currency }).format(amount);
}

/**
 * Quote then confirm (spec 06 section 6.6). The buyer sees tax for their region
 * before committing, and the total they saw is sent back with the confirmation
 * so a price that moved underneath them stops the charge instead of surprising
 * them.
 */
export function CheckoutPage(): ReactElement {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();

  const [billingCountry, setBillingCountry] = useState('IN');
  const [paymentMethod, setPaymentMethod] = useState('sandbox');
  const [placed, setPlaced] = useState<CheckoutResult | null>(null);

  const quote = useQuery({
    queryKey: ['checkout-quote', billingCountry],
    queryFn: () => getQuote(billingCountry),
    retry: false,
  });

  const confirm = useMutation({
    mutationFn: () =>
      // One key per attempt, reused if the request is retried, so a dropped
      // response cannot become a second order.
      confirmCheckout(
        {
          billingCountry,
          paymentMethod,
          returnUrl: `${window.location.origin}/checkout/return`,
          expectedGrandTotal: quote.data?.totals.grandTotal,
        },
        crypto.randomUUID(),
      ),
    onSuccess: setPlaced,
  });

  const settle = useMutation({
    mutationFn: (result: CheckoutResult) =>
      confirmSandboxPayment(
        result.paymentIntent.clientSecret.split('_secret_')[0] ?? '',
        result.totals.grandTotal,
        result.totals.currency,
      ),
    onSuccess: () => navigate('/checkout/return', { replace: true }),
  });

  if (quote.isPending) {
    return <Spinner />;
  }

  if (quote.isError) {
    return (
      <div>
        <FormError error={quote.error} />
        <button type="button" className="btn btn-outline-secondary" onClick={() => void quote.refetch()}>
          {t('common.retry')}
        </button>
      </div>
    );
  }

  const totals = quote.data.totals;

  if (placed !== null) {
    return (
      <div className="row justify-content-center">
        <div className="col-lg-6">
          <h1 className="h3">{t('checkout.payTitle')}</h1>
          <div className="alert alert-info" role="status">
            {placed.paymentIntent.instructions ?? t('checkout.completeAtProvider')}
          </div>
          <FormError error={settle.error} />
          {placed.paymentIntent.provider === 'sandbox' ? (
            <button
              type="button"
              className="btn btn-primary"
              disabled={settle.isPending}
              onClick={() => settle.mutate(placed)}
            >
              {t('checkout.simulatePayment')}
            </button>
          ) : null}
        </div>
      </div>
    );
  }

  return (
    <div className="row g-4 justify-content-center">
      <div className="col-lg-5">
        <h1 className="h3">{t('checkout.title')}</h1>

        <FormError error={confirm.error} />

        <div className="mb-3">
          <label className="form-label" htmlFor="billing-country">
            {t('checkout.billingCountry')}
          </label>
          <select
            id="billing-country"
            className="form-select"
            value={billingCountry}
            onChange={(event) => setBillingCountry(event.target.value)}
          >
            {COUNTRIES.map((country) => (
              <option key={country} value={country}>
                {country}
              </option>
            ))}
          </select>
          <div className="form-text">{t('checkout.billingCountryHelp')}</div>
        </div>

        <div className="mb-4">
          <label className="form-label" htmlFor="payment-method">
            {t('checkout.paymentMethod')}
          </label>
          <select
            id="payment-method"
            className="form-select"
            value={paymentMethod}
            onChange={(event) => setPaymentMethod(event.target.value)}
          >
            <option value="sandbox">{t('checkout.methodSandbox')}</option>
            <option value="manual_bank">{t('checkout.methodManualBank')}</option>
          </select>
        </div>

        <button
          type="button"
          className="btn btn-primary w-100"
          disabled={confirm.isPending}
          onClick={() => confirm.mutate()}
        >
          {t('checkout.placeOrder')}
        </button>
      </div>

      <div className="col-lg-4">
        <div className="card">
          <div className="card-body">
            <h2 className="h6">{t('checkout.summary')}</h2>
            <dl className="row mb-0">
              <dt className="col-7 fw-normal">{t('cart.subtotal')}</dt>
              <dd className="col-5 text-end">
                {formatAmount(totals.subtotal, totals.currency, i18n.language)}
              </dd>
              {totals.discount > 0 ? (
                <>
                  <dt className="col-7 fw-normal">{t('checkout.discount')}</dt>
                  <dd className="col-5 text-end">
                    -{formatAmount(totals.discount, totals.currency, i18n.language)}
                  </dd>
                </>
              ) : null}
              <dt className="col-7 fw-normal">{t('checkout.tax')}</dt>
              <dd className="col-5 text-end">
                {formatAmount(totals.tax, totals.currency, i18n.language)}
              </dd>
              <dt className="col-7">{t('checkout.total')}</dt>
              <dd className="col-5 text-end fw-bold">
                {formatAmount(totals.grandTotal, totals.currency, i18n.language)}
              </dd>
            </dl>
          </div>
        </div>
      </div>
    </div>
  );
}
