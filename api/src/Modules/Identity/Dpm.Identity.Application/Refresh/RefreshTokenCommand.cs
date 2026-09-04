using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;
using Dpm.Identity.Application.Abstractions;
using Dpm.Identity.Application.Contracts;
using Dpm.Identity.Domain;
using FluentValidation;

namespace Dpm.Identity.Application.Refresh;

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<AuthTokensDto>;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(c => c.RefreshToken).NotEmpty();
    }
}

/// <summary>
/// Rotates the refresh token: the presented token is retired and a new one is
/// issued in the same family. Presenting an already-rotated token is treated as
/// theft and revokes the entire family (spec 08 section 8.2).
/// </summary>
public sealed class RefreshTokenCommandHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IIdentityUnitOfWork unitOfWork,
    ITokenService tokenService,
    IClock clock)
    : ICommandHandler<RefreshTokenCommand, AuthTokensDto>
{
    public async Task<Result<AuthTokensDto>> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var presentedHash = tokenService.HashOpaqueToken(command.RefreshToken);
        var token = await refreshTokens.FindByHashAsync(presentedHash, cancellationToken);
        if (token is null)
        {
            return Result.Failure<AuthTokensDto>(IdentityErrors.InvalidToken);
        }

        if (token.IsReused(clock))
        {
            var family = await refreshTokens.FindFamilyAsync(token.FamilyId, cancellationToken);
            foreach (var member in family)
            {
                member.Revoke(clock);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<AuthTokensDto>(IdentityErrors.InvalidToken);
        }

        if (!token.IsActive(clock))
        {
            return Result.Failure<AuthTokensDto>(IdentityErrors.InvalidToken);
        }

        var user = await users.FindByIdAsync(token.UserId, cancellationToken);
        if (user is null || user.Status is UserStatus.Deleted or UserStatus.Suspended)
        {
            return Result.Failure<AuthTokensDto>(IdentityErrors.InvalidToken);
        }

        var (newToken, newHash) = tokenService.GenerateOpaqueToken();
        token.MarkRotated(newHash, clock);
        refreshTokens.Add(RefreshToken.Issue(
            user.Id,
            token.FamilyId,
            newHash,
            clock.UtcNow.Add(tokenService.RefreshTokenLifetime),
            clock));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var access = tokenService.IssueAccessToken(user);
        return Result.Success(new AuthTokensDto(
            access.Token,
            access.ExpiresInSeconds,
            newToken,
            user.ToDto()));
    }
}
