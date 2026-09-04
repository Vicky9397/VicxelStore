using Dpm.BuildingBlocks.Application;
using Dpm.Identity.Application.Abstractions;
using Dpm.Identity.Application.Contracts;

namespace Dpm.Identity.Application.Me;

public sealed record GetMeQuery : IQuery<UserDto>;

public sealed class GetMeQueryHandler(
    IUserRepository users,
    ICurrentUser currentUser)
    : IQueryHandler<GetMeQuery, UserDto>
{
    public async Task<Result<UserDto>> Handle(GetMeQuery query, CancellationToken cancellationToken)
    {
        if (currentUser.PublicId is not { } publicId)
        {
            return Result.Failure<UserDto>(IdentityErrors.InvalidToken);
        }

        var user = await users.FindByPublicIdAsync(publicId, cancellationToken);
        return user is null
            ? Result.Failure<UserDto>(IdentityErrors.UserNotFound)
            : Result.Success(user.ToDto());
    }
}
