using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;
using Dpm.Catalog.Contracts;
using Dpm.Orders.Application.Abstractions;
using Dpm.Orders.Domain;

namespace Dpm.Orders.Application.Checkout;

/// <summary>
/// Re-prices a cart against the live catalog and resolves each line's commission.
/// Checkout never trusts the cart's price snapshot: a product may have been
/// unpublished or repriced since it was added (spec 02 ORD-01 validation).
/// </summary>
public sealed class CheckoutPricing(
    ICatalogDirectory catalog,
    ICommissionPolicy commissionPolicy,
    ITaxCalculator taxCalculator)
{
    public async Task<Result<PricedCart>> PriceAsync(
        Cart cart,
        string billingCountry,
        CancellationToken ct)
    {
        var payable = cart.PayableItems;
        if (payable.Count == 0)
        {
            return Result.Failure<PricedCart>(OrderErrors.CartEmpty);
        }

        var drafts = new List<OrderLineDraft>(payable.Count);
        var repriced = new List<RepricedItem>(payable.Count);

        foreach (var item in payable)
        {
            var sellable = await catalog.FindSellableVariantAsync(item.VariantId, ct);
            if (sellable is null)
            {
                return Result.Failure<PricedCart>(OrderErrors.ProductUnavailable);
            }

            var price = Money.Create(sellable.PriceAmount, sellable.PriceCurrency);
            if (price.IsFailure)
            {
                return Result.Failure<PricedCart>(price.Error);
            }

            var commissionPct = await commissionPolicy.ResolveRateAsync(
                sellable.StoreId, sellable.CategoryId, ct);

            drafts.Add(new OrderLineDraft(
                sellable.ProductId,
                sellable.VariantId,
                sellable.VersionId,
                sellable.StoreId,
                price.Value,
                commissionPct,
                sellable.LicenseType,
                sellable.DownloadLimit));

            repriced.Add(new RepricedItem(
                item.VariantId,
                sellable.ProductSlug,
                sellable.ProductTitle,
                sellable.VariantName,
                price.Value,
                PriceChanged: item.PriceSnapshotAmount != sellable.PriceAmount
                    || item.PriceSnapshotCurrency != sellable.PriceCurrency));
        }

        var currency = drafts[0].UnitPrice.Currency;
        if (drafts.Any(d => d.UnitPrice.Currency != currency))
        {
            return Result.Failure<PricedCart>(Error.Conflict(
                "CURRENCY_MISMATCH",
                "All items in one order must share a single currency."));
        }

        var subtotal = drafts.Aggregate(
            Money.Zero(currency),
            (running, draft) => running.Add(draft.UnitPrice));

        // Coupons arrive in a later phase; the discount is carried through the
        // whole calculation now so adding them does not reshape the totals.
        var discount = Money.Zero(currency);
        var taxable = subtotal.Subtract(discount);
        var tax = await taxCalculator.CalculateAsync(taxable, billingCountry, ct);
        var grandTotal = taxable.Add(tax);

        return Result.Success(new PricedCart(drafts, repriced, subtotal, discount, tax, grandTotal));
    }
}

public sealed record RepricedItem(
    long VariantId,
    string ProductSlug,
    string ProductTitle,
    string VariantName,
    Money Price,
    bool PriceChanged);

public sealed record PricedCart(
    IReadOnlyList<OrderLineDraft> Lines,
    IReadOnlyList<RepricedItem> Items,
    Money Subtotal,
    Money Discount,
    Money Tax,
    Money GrandTotal)
{
    public bool AnyPriceChanged => Items.Any(i => i.PriceChanged);
}
