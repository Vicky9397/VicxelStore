using Dpm.BuildingBlocks.Domain;
using Dpm.Orders.Domain;

namespace Dpm.Orders.Application.Abstractions;

public interface ICartRepository
{
    Task<Cart?> FindByUserIdAsync(long userId, CancellationToken ct);

    void Add(Cart cart);
}

public interface IOrderRepository
{
    Task<Order?> FindByPublicIdAsync(Guid publicId, CancellationToken ct);

    Task<Order?> FindByIdAsync(long orderId, CancellationToken ct);

    Task<Order?> FindByLicensePublicIdAsync(Guid licensePublicId, CancellationToken ct);

    Task<IReadOnlyList<Order>> ListForBuyerAsync(long buyerUserId, CancellationToken ct);

    void Add(Order order);
}

public interface IOrdersUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);
}

public interface ICurrentBuyer
{
    Guid? UserPublicId { get; }

    Task<long?> ResolveUserIdAsync(CancellationToken ct);
}

/// <summary>
/// Computes tax at checkout by buyer region (spec 03 section 3.2). Pluggable so
/// GST, VAT and any future regime sit behind one call; rates are platform
/// configuration, not code.
/// </summary>
public interface ITaxCalculator
{
    Task<Money> CalculateAsync(Money taxableAmount, string billingCountry, CancellationToken ct);
}

/// <summary>
/// Resolves the effective commission rate: seller override, else the category
/// rate, else the platform default (spec 03 section 3.4).
/// </summary>
public interface ICommissionPolicy
{
    Task<decimal> ResolveRateAsync(long storeId, int categoryId, CancellationToken ct);
}

/// <summary>Allocates the next invoice number from the database sequence.</summary>
public interface IInvoiceNumbers
{
    Task<string> NextAsync(CancellationToken ct);
}

/// <summary>Platform settings that govern order behaviour, read from admin.Settings.</summary>
public interface IOrderPolicy
{
    Task<TimeSpan> GetPayoutHoldPeriodAsync(CancellationToken ct);
}
