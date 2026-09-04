using Dpm.Identity.Domain;

namespace Dpm.Identity.Application.Contracts;

public static class UserDtoMapping
{
    public static UserDto ToDto(this UserAccount user) => new(
        user.PublicId,
        user.Email,
        user.EmailVerified,
        user.DisplayName,
        user.Roles.Select(r => r.Name).ToList());
}
