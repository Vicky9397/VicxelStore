using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;

namespace Dpm.Orders.Domain;

/// <summary>
/// The buyer's cart (orders.Carts). Prices held here are snapshots for display;
/// checkout re-prices against the catalog before charging, so a stale cart can
/// never lock in an old price (spec 02 ORD-01 validation rules).
/// </summary>
public sealed class Cart : AggregateRoot
{
    private readonly List<CartItem> _items = [];

    public long UserId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public IReadOnlyList<CartItem> Items => _items.AsReadOnly();

    private Cart()
    {
    }

    public static Cart CreateFor(long userId, IClock clock) => new()
    {
        PublicId = Guid.NewGuid(),
        UserId = userId,
        CreatedAtUtc = clock.UtcNow,
        UpdatedAtUtc = clock.UtcNow,
    };

    /// <summary>
    /// A digital product is bought once per license, so adding the same variant
    /// twice is idempotent rather than an error or a quantity bump.
    /// </summary>
    public Result AddItem(long productId, long variantId, Money price, IClock clock)
    {
        if (_items.Count > 0 && _items[0].PriceSnapshotCurrency != price.Currency)
        {
            return Result.Failure(Error.Conflict(
                "CURRENCY_MISMATCH",
                "All items in one order must share a single currency."));
        }

        var existing = _items.FirstOrDefault(i => i.VariantId == variantId);
        if (existing is not null)
        {
            existing.Reprice(price);
            existing.MoveToCart();
            UpdatedAtUtc = clock.UtcNow;
            return Result.Success();
        }

        _items.Add(CartItem.Create(productId, variantId, price, clock));
        UpdatedAtUtc = clock.UtcNow;
        return Result.Success();
    }

    public Result RemoveItem(long variantId, IClock clock)
    {
        var removed = _items.RemoveAll(i => i.VariantId == variantId);
        if (removed == 0)
        {
            return Result.Failure(Error.NotFound("NOT_FOUND", "That item is not in the cart."));
        }

        UpdatedAtUtc = clock.UtcNow;
        return Result.Success();
    }

    public Result SaveForLater(long variantId, bool saved, IClock clock)
    {
        var item = _items.FirstOrDefault(i => i.VariantId == variantId);
        if (item is null)
        {
            return Result.Failure(Error.NotFound("NOT_FOUND", "That item is not in the cart."));
        }

        if (saved)
        {
            item.SaveForLater();
        }
        else
        {
            item.MoveToCart();
        }

        UpdatedAtUtc = clock.UtcNow;
        return Result.Success();
    }

    public void Clear(IClock clock)
    {
        _items.RemoveAll(i => !i.SavedForLater);
        UpdatedAtUtc = clock.UtcNow;
    }

    /// <summary>Items that will actually be charged; saved-for-later is excluded.</summary>
    public IReadOnlyList<CartItem> PayableItems => _items.Where(i => !i.SavedForLater).ToList();

    public void RepriceItem(long variantId, Money price, IClock clock)
    {
        var item = _items.FirstOrDefault(i => i.VariantId == variantId);
        if (item is null)
        {
            return;
        }

        item.Reprice(price);
        UpdatedAtUtc = clock.UtcNow;
    }
}

public sealed class CartItem : Entity
{
    public long CartId { get; private set; }

    public long ProductId { get; private set; }

    public long VariantId { get; private set; }

    public decimal PriceSnapshotAmount { get; private set; }

    public string PriceSnapshotCurrency { get; private set; } = string.Empty;

    public bool SavedForLater { get; private set; }

    public DateTime AddedAtUtc { get; private set; }

    private CartItem()
    {
    }

    internal static CartItem Create(long productId, long variantId, Money price, IClock clock) => new()
    {
        ProductId = productId,
        VariantId = variantId,
        PriceSnapshotAmount = price.Amount,
        PriceSnapshotCurrency = price.Currency,
        AddedAtUtc = clock.UtcNow,
    };

    internal void Reprice(Money price)
    {
        PriceSnapshotAmount = price.Amount;
        PriceSnapshotCurrency = price.Currency;
    }

    internal void SaveForLater() => SavedForLater = true;

    internal void MoveToCart() => SavedForLater = false;

    public Money Price => Money.Create(PriceSnapshotAmount, PriceSnapshotCurrency).Value;
}
