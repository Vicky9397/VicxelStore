namespace Dpm.SharedApi;

/// <summary>
/// Named policies used by module controllers. Registered once in the
/// composition root so a module cannot widen its own access.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Any platform staff role: Admin, Moderator or Support.</summary>
    public const string StaffOnly = "staff-only";

    /// <summary>Content moderation decisions: Moderator or Admin.</summary>
    public const string ModeratorOnly = "moderator-only";

    /// <summary>Actions that require a verified email (spec 02 AUTH-03).</summary>
    public const string EmailVerified = "email-verified";
}
