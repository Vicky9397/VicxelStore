using Dpm.BuildingBlocks.Application;

namespace Dpm.Marketplace.Application;

public static class MarketplaceErrors
{
    public static readonly Error NotAuthenticated =
        Error.Unauthorized("UNAUTHENTICATED", "Sign in to continue.");

    public static readonly Error StoreNotFound =
        Error.NotFound("NOT_FOUND", "Store not found.");

    public static readonly Error SlugTaken =
        Error.Conflict("CONFLICT", "That store address is already taken.");

    public static readonly Error AlreadyOwnsStore =
        Error.Conflict("CONFLICT", "This account already owns a store.");

    public static readonly Error NotStoreOwner =
        Error.Forbidden("FORBIDDEN", "You do not own this store.");

    public static readonly Error EmailNotVerified =
        Error.Forbidden("EMAIL_NOT_VERIFIED", "Verify your email before opening a store.");
}
