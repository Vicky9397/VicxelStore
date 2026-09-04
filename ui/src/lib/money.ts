import type { Money } from '@/types/api';

/** Formats money in the active locale using Intl, per spec 07 section 7.10. */
export function formatMoney(money: Money, locale: string): string {
  return new Intl.NumberFormat(locale, {
    style: 'currency',
    currency: money.currency,
  }).format(money.amount);
}
