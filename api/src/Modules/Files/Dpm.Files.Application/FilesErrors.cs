using Dpm.BuildingBlocks.Application;

namespace Dpm.Files.Application;

public static class FilesErrors
{
    public static readonly Error NotAuthenticated =
        Error.Unauthorized("UNAUTHENTICATED", "Sign in to continue.");

    public static readonly Error NoStore =
        Error.Forbidden("FORBIDDEN", "Open a store before uploading files.");

    public static readonly Error VariantNotFound =
        Error.NotFound("NOT_FOUND", "That product variant does not exist.");

    public static readonly Error NotVariantOwner =
        Error.Forbidden("FORBIDDEN", "You do not own the product this file belongs to.");

    public static readonly Error UploadNotFound =
        Error.NotFound("NOT_FOUND", "Upload session not found.");

    public static readonly Error FileNotFound =
        Error.NotFound("NOT_FOUND", "File not found.");

    public static readonly Error NoVersion =
        Error.Conflict("CONFLICT", "Release a product version before uploading files.");
}
