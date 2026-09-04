using Dpm.BuildingBlocks.Application;

namespace Dpm.Identity.Application;

public static class IdentityErrors
{
    /// <summary>
    /// Generic credential failure: never disclose whether the email exists
    /// (spec 02 section 2.4 auth error rules).
    /// </summary>
    public static readonly Error InvalidCredentials =
        Error.Unauthorized("UNAUTHENTICATED", "Invalid email or password.");

    public static readonly Error AccountLocked =
        Error.Locked("ACCOUNT_LOCKED", "Account is temporarily locked. Try again later.");

    public static readonly Error InvalidToken =
        Error.Unauthorized("UNAUTHENTICATED", "The token is invalid or has expired.");

    public static readonly Error EmailNotVerified =
        Error.Forbidden("EMAIL_NOT_VERIFIED", "Verify your email to continue.");

    public static readonly Error UserNotFound =
        Error.NotFound("NOT_FOUND", "User not found.");

    public static readonly Error ResendRateLimited =
        Error.RateLimited("RATE_LIMITED", "Too many verification emails requested. Try again later.");
}
