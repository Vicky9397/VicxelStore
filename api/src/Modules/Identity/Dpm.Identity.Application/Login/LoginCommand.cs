using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;
using Dpm.Identity.Application.Abstractions;
using Dpm.Identity.Application.Contracts;
using Dpm.Identity.Domain;
using FluentValidation;

namespace Dpm.Identity.Application.Login;

public sealed record LoginCommand(string Email, string Password) : ICommand<AuthTokensDto>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
        RuleFor(c => c.Password).NotEmpty();
    }
}

public sealed class LoginCommandHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IIdentityUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IClock clock)
    : ICommandHandler<LoginCommand, AuthTokensDto>
{
    public async Task<Result<AuthTokensDto>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(UserAccount.NormalizeEmail(command.Email), cancellationToken);
        if (user is null || user.PasswordHash is null || user.Status == UserStatus.Deleted)
        {
            return Result.Failure<AuthTokensDto>(IdentityErrors.InvalidCredentials);
        }

        if (user.IsLockedOut(clock) || user.Status == UserStatus.Suspended)
        {
            return Result.Failure<AuthTokensDto>(IdentityErrors.AccountLocked);
        }

        if (!passwordHasher.Verify(command.Password, user.PasswordHash))
        {
            user.RecordFailedLogin(clock);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<AuthTokensDto>(IdentityErrors.InvalidCredentials);
        }

        user.RecordSuccessfulLogin(clock);

        var (refreshToken, refreshHash) = tokenService.GenerateOpaqueToken();
        refreshTokens.Add(RefreshToken.Issue(
            user.Id,
            familyId: Guid.NewGuid(),
            refreshHash,
            clock.UtcNow.Add(tokenService.RefreshTokenLifetime),
            clock));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var access = tokenService.IssueAccessToken(user);
        return Result.Success(new AuthTokensDto(
            access.Token,
            access.ExpiresInSeconds,
            refreshToken,
            user.ToDto()));
    }
}
