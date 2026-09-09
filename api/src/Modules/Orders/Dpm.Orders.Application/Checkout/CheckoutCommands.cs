using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;
using Dpm.Orders.Application.Abstractions;
using Dpm.Orders.Application.CartCommands;
using Dpm.Orders.Application.Contracts;
using Dpm.Orders.Domain;
using Dpm.Payments.Contracts;
using FluentValidation;

namespace Dpm.Orders.Application.Checkout;

public sealed record CheckoutQuoteQuery(string BillingCountry) : IQuery<QuoteDto>;

public sealed class CheckoutQuoteQueryValidator : AbstractValidator<CheckoutQuoteQuery>
{
    public CheckoutQuoteQueryValidator()
    {
        RuleFor(q => q.BillingCountry).NotEmpty().Length(2)
            .WithMessage("Billing country must be a 2-letter ISO code.");
    }
}

/// <summary>
/// Shows the buyer what they will be charged, including tax for their region.
/// It changes nothing, so it can be called as often as the cart changes.
/// </summary>
public sealed class CheckoutQuoteQueryHandler(
    CartService cartService,
    CheckoutPricing pricing)
    : IQueryHandler<CheckoutQuoteQuery, QuoteDto>
{
    public async Task<Result<QuoteDto>> Handle(CheckoutQuoteQuery query, CancellationToken cancellationToken)
    {
        var loaded = await cartService.LoadOrCreateAsync(cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<QuoteDto>(loaded.Error);
        }

        var priced = await pricing.PriceAsync(
            loaded.Value, query.BillingCountry.ToUpperInvariant(), cancellationToken);
        if (priced.IsFailure)
        {
            return Result.Failure<QuoteDto>(priced.Error);
        }

        var cartDto = await cartService.ProjectAsync(loaded.Value, cancellationToken);
        return Result.Success(new QuoteDto(ToTotals(priced.Value), cartDto.Items));
    }

    internal static TotalsDto ToTotals(PricedCart priced) => new(
        priced.Subtotal.Amount,
        priced.Discount.Amount,
        priced.Tax.Amount,
        priced.GrandTotal.Amount,
        priced.GrandTotal.Currency);
}

public sealed record ConfirmCheckoutCommand(
    string BillingCountry,
    string PaymentMethod,
    string? ReturnUrl,
    decimal? ExpectedGrandTotal) : ICommand<CheckoutResultDto>;

public sealed class ConfirmCheckoutCommandValidator : AbstractValidator<ConfirmCheckoutCommand>
{
    public ConfirmCheckoutCommandValidator()
    {
        RuleFor(c => c.BillingCountry).NotEmpty().Length(2);
        RuleFor(c => c.PaymentMethod).NotEmpty().MaximumLength(20);
    }
}

/// <summary>
/// Turns a priced cart into a Pending order and a provider payment intent.
///
/// It does not capture money and does not issue licenses: both follow the
/// provider's verified webhook. That ordering is what makes a failed payment
/// leave no order behind to reconcile and a duplicate submit harmless
/// (spec 02 ORD-01).
///
/// The endpoint is idempotent through the Idempotency-Key header, handled in the
/// Api layer so a replay returns the first response rather than charging twice.
/// </summary>
public sealed class ConfirmCheckoutCommandHandler(
    CartService cartService,
    CheckoutPricing pricing,
    IOrderRepository orders,
    IOrdersUnitOfWork unitOfWork,
    IPaymentIntentService paymentIntents,
    ICurrentBuyer buyer,
    IClock clock)
    : ICommandHandler<ConfirmCheckoutCommand, CheckoutResultDto>
{
    public async Task<Result<CheckoutResultDto>> Handle(
        ConfirmCheckoutCommand command,
        CancellationToken cancellationToken)
    {
        if (buyer.UserPublicId is not { } buyerPublicId
            || await buyer.ResolveUserIdAsync(cancellationToken) is not { } userId)
        {
            return Result.Failure<CheckoutResultDto>(OrderErrors.NotAuthenticated);
        }

        var loaded = await cartService.LoadOrCreateAsync(cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<CheckoutResultDto>(loaded.Error);
        }

        var cart = loaded.Value;
        var billingCountry = command.BillingCountry.ToUpperInvariant();
        var priced = await pricing.PriceAsync(cart, billingCountry, cancellationToken);
        if (priced.IsFailure)
        {
            return Result.Failure<CheckoutResultDto>(priced.Error);
        }

        // The buyer confirms a total they were shown. If it moved underneath
        // them, stop and make them look again rather than charging the new one.
        if (command.ExpectedGrandTotal is { } expected
            && expected != priced.Value.GrandTotal.Amount)
        {
            return Result.Failure<CheckoutResultDto>(OrderErrors.PriceChanged);
        }

        var placed = Order.Place(
            userId,
            buyerPublicId,
            billingCountry,
            priced.Value.Lines,
            priced.Value.Tax,
            priced.Value.Discount,
            clock);
        if (placed.IsFailure)
        {
            return Result.Failure<CheckoutResultDto>(placed.Error);
        }

        var order = placed.Value;
        orders.Add(order);
        cart.Clear(clock);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var intent = await paymentIntents.CreateForOrderAsync(
            new CreatePaymentIntent(
                order.Id,
                order.PublicId,
                order.GrandTotal,
                order.Currency,
                command.PaymentMethod,
                command.ReturnUrl),
            cancellationToken);

        return Result.Success(new CheckoutResultDto(
            order.PublicId,
            new PaymentIntentDto(intent.Provider, intent.ClientSecret, intent.Instructions),
            CheckoutQuoteQueryHandler.ToTotals(priced.Value)));
    }
}
