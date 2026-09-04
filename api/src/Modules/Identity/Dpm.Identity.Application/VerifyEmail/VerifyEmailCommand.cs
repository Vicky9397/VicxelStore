using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;
using Dpm.Identity.Application.Abstractions;
using Dpm.Identity.Domain;
using FluentValidation;

namespace Dpm.Identity.Application.VerifyEmail;

public sealed record VerifyEmailCommand(string Token) : ICommand;

public sealed class VerifyEmailCommandValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailCommandValidator()
    {
        RuleFor(c => c.Token).NotEmpty();
    }
}

public sealed class VerifyEmailCommandHandler(
    IUserRepository users,
    IUserTokenRepository userTokens,
    IIdentityUnitOfWork unitOfWork,
    ITokenService tokenService,
    IClock clock)
    : ICommandHandler<VerifyEmailCommand>
{
    public async Task<Result> Handle(VerifyEmailCommand command, CancellationToken cancellationToken)
    {
        var hash = tokenService.HashOpaqueToken(command.Token);
        var token = await userTokens.FindUsableByHashAsync(hash, TokenPurpose.EmailVerification, cancellationToken);
        if (token is null || !token.IsUsable(clock))
        {
            return Result.Failure(IdentityErrors.InvalidToken);
        }

        var user = await users.FindByIdAsync(token.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(IdentityErrors.InvalidToken);
        }

        token.Consume(clock);
        user.VerifyEmail(clock);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
