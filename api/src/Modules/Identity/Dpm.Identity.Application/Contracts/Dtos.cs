namespace Dpm.Identity.Application.Contracts;

public sealed record UserDto(
    Guid Id,
    string Email,
    bool EmailVerified,
    string DisplayName,
    IReadOnlyList<string> Roles);

public sealed record AuthTokensDto(
    string AccessToken,
    int ExpiresInSeconds,
    string RefreshToken,
    UserDto User);
