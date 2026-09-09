using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;
using Dpm.Catalog.Contracts;
using Dpm.Orders.Application.Abstractions;
using Dpm.Orders.Application.Contracts;
using Dpm.Orders.Domain;

namespace Dpm.Orders.Application.CartCommands;

public sealed record GetCartQuery : IQuery<CartDto>;

public sealed record AddCartItemCommand(Guid VariantId) : ICommand<CartDto>;

public sealed record RemoveCartItemCommand(Guid VariantId) : ICommand<CartDto>;

public sealed record SaveForLaterCommand(Guid VariantId, bool SavedForLater) : ICommand<CartDto>;

/// <summary>
/// Shared cart loading and projection. The cart is created on first use so a
/// buyer never has to be told they lack one.
/// </summary>
public sealed class CartService(
    ICartRepository carts,
    IOrdersUnitOfWork unitOfWork,
    ICatalogDirectory catalog,
    ICurrentBuyer buyer,
    IClock clock)
{
    public async Task<Result<Cart>> LoadOrCreateAsync(CancellationToken ct)
    {
        if (await buyer.ResolveUserIdAsync(ct) is not { } userId)
        {
            return Result.Failure<Cart>(OrderErrors.NotAuthenticated);
        }

        var cart = await carts.FindByUserIdAsync(userId, ct);
        if (cart is not null)
        {
            return Result.Success(cart);
        }

        cart = Cart.CreateFor(userId, clock);
        carts.Add(cart);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(cart);
    }

    /// <summary>
    /// Projects the cart with live catalog data. An item whose product is no
    /// longer purchasable is shown as unavailable rather than silently dropped,
    /// so the buyer can see why the total changed.
    /// </summary>
    public async Task<CartDto> ProjectAsync(Cart cart, CancellationToken ct)
    {
        var items = new List<CartItemDto>(cart.Items.Count);
        var currency = "INR";
        var subtotal = 0m;

        foreach (var item in cart.Items)
        {
            var sellable = await catalog.FindSellableVariantAsync(item.VariantId, ct);
            var available = sellable is not null;
            var price = sellable is null
                ? new MoneyDto(item.PriceSnapshotAmount, item.PriceSnapshotCurrency)
                : new MoneyDto(sellable.PriceAmount, sellable.PriceCurrency);

            if (available && !item.SavedForLater)
            {
                currency = price.Currency;
                subtotal += price.Amount;
            }

            items.Add(new CartItemDto(
                sellable?.VariantPublicId ?? Guid.Empty,
                sellable?.ProductSlug ?? string.Empty,
                sellable?.ProductTitle ?? "Unavailable item",
                sellable?.VariantName ?? string.Empty,
                price,
                item.SavedForLater,
                available));
        }

        return new CartDto(
            cart.PublicId,
            items,
            new MoneyDto(subtotal, currency),
            items.Count(i => !i.SavedForLater && i.IsAvailable));
    }
}

public sealed class GetCartQueryHandler(CartService cartService)
    : IQueryHandler<GetCartQuery, CartDto>
{
    public async Task<Result<CartDto>> Handle(GetCartQuery query, CancellationToken cancellationToken)
    {
        var cart = await cartService.LoadOrCreateAsync(cancellationToken);
        return cart.IsFailure
            ? Result.Failure<CartDto>(cart.Error)
            : Result.Success(await cartService.ProjectAsync(cart.Value, cancellationToken));
    }
}

public sealed class AddCartItemCommandHandler(
    CartService cartService,
    IOrdersUnitOfWork unitOfWork,
    ICatalogDirectory catalog,
    IClock clock)
    : ICommandHandler<AddCartItemCommand, CartDto>
{
    public async Task<Result<CartDto>> Handle(AddCartItemCommand command, CancellationToken cancellationToken)
    {
        var loaded = await cartService.LoadOrCreateAsync(cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<CartDto>(loaded.Error);
        }

        var sellable = await catalog.FindSellableVariantByPublicIdAsync(command.VariantId, cancellationToken);
        if (sellable is null)
        {
            return Result.Failure<CartDto>(OrderErrors.ProductUnavailable);
        }

        var price = Money.Create(sellable.PriceAmount, sellable.PriceCurrency);
        if (price.IsFailure)
        {
            return Result.Failure<CartDto>(price.Error);
        }

        var cart = loaded.Value;
        var add = cart.AddItem(sellable.ProductId, sellable.VariantId, price.Value, clock);
        if (add.IsFailure)
        {
            return Result.Failure<CartDto>(add.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(await cartService.ProjectAsync(cart, cancellationToken));
    }
}

public sealed class RemoveCartItemCommandHandler(
    CartService cartService,
    IOrdersUnitOfWork unitOfWork,
    ICatalogDirectory catalog,
    IClock clock)
    : ICommandHandler<RemoveCartItemCommand, CartDto>
{
    public async Task<Result<CartDto>> Handle(RemoveCartItemCommand command, CancellationToken cancellationToken)
    {
        var loaded = await cartService.LoadOrCreateAsync(cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<CartDto>(loaded.Error);
        }

        var sellable = await catalog.FindSellableVariantByPublicIdAsync(command.VariantId, cancellationToken);
        if (sellable is null)
        {
            return Result.Failure<CartDto>(OrderErrors.ProductUnavailable);
        }

        var cart = loaded.Value;
        var remove = cart.RemoveItem(sellable.VariantId, clock);
        if (remove.IsFailure)
        {
            return Result.Failure<CartDto>(remove.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(await cartService.ProjectAsync(cart, cancellationToken));
    }
}

public sealed class SaveForLaterCommandHandler(
    CartService cartService,
    IOrdersUnitOfWork unitOfWork,
    ICatalogDirectory catalog,
    IClock clock)
    : ICommandHandler<SaveForLaterCommand, CartDto>
{
    public async Task<Result<CartDto>> Handle(SaveForLaterCommand command, CancellationToken cancellationToken)
    {
        var loaded = await cartService.LoadOrCreateAsync(cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<CartDto>(loaded.Error);
        }

        var sellable = await catalog.FindSellableVariantByPublicIdAsync(command.VariantId, cancellationToken);
        if (sellable is null)
        {
            return Result.Failure<CartDto>(OrderErrors.ProductUnavailable);
        }

        var cart = loaded.Value;
        var update = cart.SaveForLater(sellable.VariantId, command.SavedForLater, clock);
        if (update.IsFailure)
        {
            return Result.Failure<CartDto>(update.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(await cartService.ProjectAsync(cart, cancellationToken));
    }
}
