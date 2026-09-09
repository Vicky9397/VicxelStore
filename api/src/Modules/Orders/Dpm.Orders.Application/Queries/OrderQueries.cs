using Dpm.BuildingBlocks.Application;
using Dpm.Catalog.Contracts;
using Dpm.Orders.Application.Abstractions;
using Dpm.Orders.Application.Contracts;
using Dpm.Orders.Domain;

namespace Dpm.Orders.Application.Queries;

public sealed record ListMyOrdersQuery : IQuery<IReadOnlyList<OrderDto>>;

public sealed record GetOrderQuery(Guid OrderId) : IQuery<OrderDto>;

public sealed record ListMyLicensesQuery : IQuery<IReadOnlyList<LicenseDto>>;

/// <summary>Projects orders with the catalog titles a buyer expects to see.</summary>
public sealed class OrderProjector(ICatalogDirectory catalog)
{
    public async Task<OrderDto> ProjectAsync(Order order, CancellationToken ct)
    {
        var lines = new List<OrderLineDto>(order.Lines.Count);
        var licenses = new List<LicenseDto>(order.Licenses.Count);

        foreach (var line in order.Lines)
        {
            var variant = await catalog.FindVariantByIdAsync(line.VariantId, ct);
            var names = await ResolveNamesAsync(line.VariantId, ct);
            lines.Add(new OrderLineDto(
                variant?.ProductTitle ?? names.ProductTitle,
                names.VariantName,
                new MoneyDto(line.UnitAmount, line.Currency)));
        }

        foreach (var license in order.Licenses)
        {
            var names = await ResolveNamesAsync(license.VariantId, ct);
            licenses.Add(new LicenseDto(
                license.PublicId,
                names.ProductTitle,
                names.VariantName,
                license.DownloadLimit,
                license.DownloadsUsed,
                license.IssuedAtUtc));
        }

        return new OrderDto(
            order.PublicId,
            order.Status.ToString(),
            order.InvoiceNo,
            new TotalsDto(
                order.Subtotal, order.DiscountTotal, order.TaxTotal, order.GrandTotal, order.Currency),
            order.PlacedAtUtc,
            lines,
            licenses);
    }

    public async Task<IReadOnlyList<LicenseDto>> ProjectLicensesAsync(
        IReadOnlyList<Order> orders,
        CancellationToken ct)
    {
        var licenses = new List<LicenseDto>();
        foreach (var order in orders.Where(o => o.IsPaid))
        {
            foreach (var license in order.Licenses)
            {
                var names = await ResolveNamesAsync(license.VariantId, ct);
                licenses.Add(new LicenseDto(
                    license.PublicId,
                    names.ProductTitle,
                    names.VariantName,
                    license.DownloadLimit,
                    license.DownloadsUsed,
                    license.IssuedAtUtc));
            }
        }

        return licenses;
    }

    /// <summary>
    /// A purchased product may since have been unpublished, so the sellable
    /// lookup can come back empty. The order still has to render, so fall back
    /// to the variant reference rather than failing the buyer's history.
    /// </summary>
    private async Task<(string ProductTitle, string VariantName)> ResolveNamesAsync(
        long variantId,
        CancellationToken ct)
    {
        var sellable = await catalog.FindSellableVariantAsync(variantId, ct);
        if (sellable is not null)
        {
            return (sellable.ProductTitle, sellable.VariantName);
        }

        var variant = await catalog.FindVariantByIdAsync(variantId, ct);
        return (variant?.ProductTitle ?? "Unavailable product", string.Empty);
    }
}

public sealed class ListMyOrdersQueryHandler(
    IOrderRepository orders,
    OrderProjector projector,
    ICurrentBuyer buyer)
    : IQueryHandler<ListMyOrdersQuery, IReadOnlyList<OrderDto>>
{
    public async Task<Result<IReadOnlyList<OrderDto>>> Handle(
        ListMyOrdersQuery query,
        CancellationToken cancellationToken)
    {
        if (await buyer.ResolveUserIdAsync(cancellationToken) is not { } userId)
        {
            return Result.Failure<IReadOnlyList<OrderDto>>(OrderErrors.NotAuthenticated);
        }

        var found = await orders.ListForBuyerAsync(userId, cancellationToken);
        var projected = new List<OrderDto>(found.Count);
        foreach (var order in found)
        {
            projected.Add(await projector.ProjectAsync(order, cancellationToken));
        }

        return Result.Success<IReadOnlyList<OrderDto>>(projected);
    }
}

public sealed class GetOrderQueryHandler(
    IOrderRepository orders,
    OrderProjector projector,
    ICurrentBuyer buyer)
    : IQueryHandler<GetOrderQuery, OrderDto>
{
    public async Task<Result<OrderDto>> Handle(GetOrderQuery query, CancellationToken cancellationToken)
    {
        if (await buyer.ResolveUserIdAsync(cancellationToken) is not { } userId)
        {
            return Result.Failure<OrderDto>(OrderErrors.NotAuthenticated);
        }

        var order = await orders.FindByPublicIdAsync(query.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure<OrderDto>(OrderErrors.OrderNotFound);
        }

        return order.BuyerUserId == userId
            ? Result.Success(await projector.ProjectAsync(order, cancellationToken))
            : Result.Failure<OrderDto>(OrderErrors.NotOrderOwner);
    }
}

public sealed class ListMyLicensesQueryHandler(
    IOrderRepository orders,
    OrderProjector projector,
    ICurrentBuyer buyer)
    : IQueryHandler<ListMyLicensesQuery, IReadOnlyList<LicenseDto>>
{
    public async Task<Result<IReadOnlyList<LicenseDto>>> Handle(
        ListMyLicensesQuery query,
        CancellationToken cancellationToken)
    {
        if (await buyer.ResolveUserIdAsync(cancellationToken) is not { } userId)
        {
            return Result.Failure<IReadOnlyList<LicenseDto>>(OrderErrors.NotAuthenticated);
        }

        var found = await orders.ListForBuyerAsync(userId, cancellationToken);
        return Result.Success(await projector.ProjectLicensesAsync(found, cancellationToken));
    }
}
