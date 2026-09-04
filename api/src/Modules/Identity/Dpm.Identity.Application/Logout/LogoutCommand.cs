using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;
using Dpm.Identity.Application.Abstractions;

namespace Dpm.Identity.Application.Logout;

public sealed record LogoutCommand(string RefreshToken) : ICommand;

/// <summary>Revokes the presented refresh token's whole family (ends the session).</summary>
public sealed class LogoutCommandHandler(
    IRefreshTokenRepository refreshTokens,
    IIdentityUnitOfWork unitOfWork,
    ITokenService tokenService,
    IClock clock)
    : ICommandHandler<LogoutCommand>
{
    public async Task<Result> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return Result.Success();
        }

        var hash = tokenService.HashOpaqueToken(command.RefreshToken);
        var token = await refreshTokens.FindByHashAsync(hash, cancellationToken);
        if (token is null)
        {
            return Result.Success();
        }

        var family = await refreshTokens.FindFamilyAsync(token.FamilyId, cancellationToken);
        foreach (var member in family)
        {
            member.Revoke(clock);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
