using Dpm.BuildingBlocks.Application;

namespace Dpm.Catalog.Application;

public static class CatalogErrors
{
    public static readonly Error NotAuthenticated =
        Error.Unauthorized("UNAUTHENTICATED", "Sign in to continue.");

    public static readonly Error NoStore =
        Error.Forbidden("FORBIDDEN", "Open a store before managing products.");

    public static readonly Error NotProductOwner =
        Error.Forbidden("FORBIDDEN", "You do not own this product.");

    public static readonly Error ProductNotFound =
        Error.NotFound("NOT_FOUND", "Product not found.");

    public static readonly Error CategoryNotFound =
        Error.Validation("VALIDATION_ERROR", "That category does not exist.");

    public static readonly Error SlugTaken =
        Error.Conflict("CONFLICT", "That product address is already taken.");

    public static readonly Error StoreSuspended =
        Error.Forbidden("FORBIDDEN", "This store is suspended.");
}
