using Dpm.Orders.Application.Abstractions;
using Dpm.Orders.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dpm.Orders.Infrastructure.Persistence;

public sealed class CartRepository(OrdersDbContext dbContext) : ICartRepository
{
    public Task<Cart?> FindByUserIdAsync(long userId, CancellationToken ct) =>
        dbContext.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == userId, ct);

    public void Add(Cart cart) => dbContext.Carts.Add(cart);
}

public sealed class OrderRepository(OrdersDbContext dbContext) : IOrderRepository
{
    private IQueryable<Order> WithAggregate() =>
        dbContext.Orders.Include(o => o.Lines).Include(o => o.Licenses);

    public Task<Order?> FindByPublicIdAsync(Guid publicId, CancellationToken ct) =>
        WithAggregate().FirstOrDefaultAsync(o => o.PublicId == publicId, ct);

    public Task<Order?> FindByIdAsync(long orderId, CancellationToken ct) =>
        WithAggregate().FirstOrDefaultAsync(o => o.Id == orderId, ct);

    public Task<Order?> FindByLicensePublicIdAsync(Guid licensePublicId, CancellationToken ct) =>
        WithAggregate().FirstOrDefaultAsync(o => o.Licenses.Any(l => l.PublicId == licensePublicId), ct);

    public async Task<IReadOnlyList<Order>> ListForBuyerAsync(long buyerUserId, CancellationToken ct) =>
        await WithAggregate()
            .Where(o => o.BuyerUserId == buyerUserId)
            .OrderByDescending(o => o.PlacedAtUtc)
            .ToListAsync(ct);

    public void Add(Order order) => dbContext.Orders.Add(order);
}

public sealed class OrdersUnitOfWork(OrdersDbContext dbContext) : IOrdersUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}

/// <summary>
/// Allocates invoice numbers from the database sequence (spec 05 section 5.7),
/// which gives contiguous-ish numbering without a contended counter row.
/// </summary>
public sealed class InvoiceNumbers(OrdersDbContext dbContext) : IInvoiceNumbers
{
    public async Task<string> NextAsync(CancellationToken ct)
    {
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT NEXT VALUE FOR orders.InvoiceNumbers;";

        await dbContext.Database.OpenConnectionAsync(ct);
        try
        {
            var next = await command.ExecuteScalarAsync(ct);
            var sequential = Convert.ToInt64(next, System.Globalization.CultureInfo.InvariantCulture);
            return $"INV-{DateTime.UtcNow:yyyy}-{sequential:D8}";
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }
}
