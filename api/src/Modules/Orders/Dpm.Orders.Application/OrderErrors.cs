using Dpm.BuildingBlocks.Application;

namespace Dpm.Orders.Application;

public static class OrderErrors
{
    public static readonly Error NotAuthenticated =
        Error.Unauthorized("UNAUTHENTICATED", "Sign in to continue.");

    public static readonly Error CartEmpty =
        Error.Conflict("CART_EMPTY", "Your cart is empty.");

    public static readonly Error ProductUnavailable =
        Error.Conflict("PRODUCT_UNAVAILABLE", "An item in your cart is no longer available.");

    public static readonly Error PriceChanged =
        Error.Conflict("PRICE_CHANGED", "A price changed while you were checking out. Review the new total.");

    public static readonly Error OrderNotFound =
        Error.NotFound("NOT_FOUND", "Order not found.");

    public static readonly Error NotOrderOwner =
        Error.Forbidden("FORBIDDEN", "This order belongs to another account.");

    public static readonly Error EmailNotVerified =
        Error.Forbidden("EMAIL_NOT_VERIFIED", "Verify your email before buying.");

    public static readonly Error IdempotencyConflict =
        Error.Conflict("IDEMPOTENCY_CONFLICT", "This request was already submitted with different details.");
}
