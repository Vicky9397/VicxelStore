using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;
using Dpm.Identity.Application.Abstractions;
using Dpm.Identity.Domain;
using FluentValidation;

namespace Dpm.Identity.Application.Register;

public sealed record RegisterUserCommand(string Email, string Password, string DisplayName) : ICommand;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(c => c.DisplayName).NotEmpty().MinimumLength(2).MaximumLength(120);
        RuleFor(c => c.Password)
            .NotEmpty()
            .MinimumLength(12)
            .WithErrorCode("WEAK_PASSWORD")
            .WithMessage("Password must be at least 12 characters.");
        RuleFor(c => c)
            .Must(c => !string.Equals(
                c.Password,
                c.Email.Split('@')[0],
                StringComparison.OrdinalIgnoreCase))
            .WithErrorCode("WEAK_PASSWORD")
            .WithMessage("Password cannot equal the email local part.")
            .OverridePropertyName("password");
    }
}

/// <summary>
/// AUTH-03: creates an unverified account and queues a verification email. On a
/// duplicate email the handler still succeeds (no account-existence disclosure)
/// and silently does nothing.
/// </summary>
public sealed class RegisterUserCommandHandler(
    IUserRepository users,
    IUserTokenRepository userTokens,
    IIdentityUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IVerificationEmailComposer emailComposer,
    IClock clock)
    : ICommandHandler<RegisterUserCommand>
{
    public async Task<Result> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        var normalized = UserAccount.NormalizeEmail(command.Email);
        if (await users.EmailExistsAsync(normalized, cancellationToken))
        {
            return Result.Success();
        }

        var user = UserAccount.Register(
            command.Email,
            command.DisplayName,
            passwordHasher.Hash(command.Password),
            clock);

        var buyerRole = await users.FindRoleAsync(Role.Buyer, cancellationToken);
        if (buyerRole is not null)
        {
            user.AddRole(buyerRole);
        }

        users.Add(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

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
