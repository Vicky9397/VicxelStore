using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;
using Dpm.Identity.Application.Abstractions;
using Dpm.Identity.Domain;
using FluentValidation;

namespace Dpm.Identity.Application.VerifyEmail;

public sealed record ResendVerificationCommand(string Email) : ICommand;

public sealed class ResendVerificationCommandValidator : AbstractValidator<ResendVerificationCommand>
{
    public ResendVerificationCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
    }
}

/// <summary>
/// Re-sends the verification email. Rate-limited to 3 per hour per account
/// (AUTH-03 business rule); unknown emails succeed silently (no disclosure).
/// </summary>
public sealed class ResendVerificationCommandHandler(
    IUserRepository users,
    IUserTokenRepository userTokens,
    IIdentityUnitOfWork unitOfWork,
    ITokenService tokenService,
    IVerificationEmailComposer emailComposer,
    IClock clock)
    : ICommandHandler<ResendVerificationCommand>
{
    public async Task<Result> Handle(ResendVerificationCommand command, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(UserAccount.NormalizeEmail(command.Email), cancellationToken);
        if (user is null || user.EmailVerified)
        {
            return Result.Success();
        }

        var issuedLastHour = await userTokens.CountIssuedSinceAsync(
            user.Id,
            TokenPurpose.EmailVerification,
            clock.UtcNow.AddHours(-1),
            cancellationToken);
        if (issuedLastHour >= 3)
        {
            return Result.Failure(IdentityErrors.ResendRateLimited);
        }

        var (token, hash) = tokenService.GenerateOpaqueToken();
        userTokens.Add(UserToken.Issue(
            user.Id,
            TokenPurpose.EmailVerification,
            hash,
            TimeSpan.FromHours(24),
            clock));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await emailComposer.SendVerificationAsync(user.Email, user.DisplayName, token, cancellationToken);
        return Result.Success();
    }
}
