using Dpm.Identity.Application.Login;
using Dpm.Identity.Application.Logout;
using Dpm.Identity.Application.Refresh;
using Dpm.Identity.Application.Register;
using Dpm.Identity.Application.VerifyEmail;
using Dpm.Identity.Domain;
using FluentAssertions;

namespace Dpm.UnitTests.Application;

public sealed class AuthFlowHandlerTests
{
    private const string Email = "buyer@example.com";
    private const string Password = "a-long-enough-password";

    private readonly TestClock _clock = new();
    private readonly FakeIdentityStore _store = new();
    private readonly FakeTokenService _tokens = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly RecordingEmailComposer _emails = new();

    private async Task<UserAccount> RegisterAsync()
    {
        var handler = new RegisterUserCommandHandler(
            _store, _store, _store, _hasher, _tokens, _emails, _clock);
        await handler.Handle(new RegisterUserCommand(Email, Password, "Buyer One"), CancellationToken.None);
        return _store.Users[0];
    }

    private LoginCommandHandler LoginHandler() =>
        new(_store, _store, _store, _hasher, _tokens, _clock);

    private RefreshTokenCommandHandler RefreshHandler() =>
        new(_store, _store, _store, _tokens, _clock);

    [Fact]
    public async Task Login_with_correct_credentials_issues_an_access_and_refresh_token()
    {
        await RegisterAsync();

        var result = await LoginHandler().Handle(new LoginCommand(Email, Password), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().NotBeNullOrEmpty();
        result.Value.RefreshToken.Should().NotBeNullOrEmpty();
        result.Value.User.Email.Should().Be(Email);
        _store.RefreshTokens.Should().ContainSingle();
    }

    [Fact]
    public async Task The_refresh_token_is_persisted_only_as_a_hash()
    {
        await RegisterAsync();

        var result = await LoginHandler().Handle(new LoginCommand(Email, Password), CancellationToken.None);

        _store.RefreshTokens[0].TokenHash
            .Should().Be(_tokens.HashOpaqueToken(result.Value.RefreshToken));
        _store.RefreshTokens[0].TokenHash.Should().NotBe(result.Value.RefreshToken);
    }

    [Fact]
    public async Task Login_with_a_wrong_password_returns_the_generic_credential_error()
    {
        await RegisterAsync();

        var result = await LoginHandler().Handle(
            new LoginCommand(Email, "wrong-password-here"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("UNAUTHENTICATED");
        _store.Users[0].FailedLogins.Should().Be(1);
    }

    [Fact]
    public async Task Login_with_an_unknown_email_returns_the_same_generic_error()
    {
        var result = await LoginHandler().Handle(
            new LoginCommand("nobody@example.com", Password), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("UNAUTHENTICATED");
    }

    [Fact]
    public async Task Login_is_refused_while_the_account_is_locked_out()
    {
        await RegisterAsync();
        var handler = LoginHandler();
        for (var attempt = 0; attempt < UserAccount.MaxFailedLogins; attempt++)
        {
            await handler.Handle(new LoginCommand(Email, "wrong-password-here"), CancellationToken.None);
        }

        var result = await handler.Handle(new LoginCommand(Email, Password), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ACCOUNT_LOCKED");
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_keeps_the_family()
    {
        await RegisterAsync();
        var login = await LoginHandler().Handle(new LoginCommand(Email, Password), CancellationToken.None);

        var refreshed = await RefreshHandler().Handle(
            new RefreshTokenCommand(login.Value.RefreshToken), CancellationToken.None);

        refreshed.IsSuccess.Should().BeTrue();
        refreshed.Value.RefreshToken.Should().NotBe(login.Value.RefreshToken);
        _store.RefreshTokens.Should().HaveCount(2);
        _store.RefreshTokens.Select(t => t.FamilyId).Distinct().Should().ContainSingle();
    }

    [Fact]
    public async Task Reusing_a_rotated_refresh_token_revokes_the_entire_family()
    {
        await RegisterAsync();
        var login = await LoginHandler().Handle(new LoginCommand(Email, Password), CancellationToken.None);
        var handler = RefreshHandler();
        var firstRefresh = await handler.Handle(
            new RefreshTokenCommand(login.Value.RefreshToken), CancellationToken.None);

        var reuse = await handler.Handle(
            new RefreshTokenCommand(login.Value.RefreshToken), CancellationToken.None);

        reuse.IsFailure.Should().BeTrue();
        reuse.Error.Code.Should().Be("UNAUTHENTICATED");
        _store.RefreshTokens.Should().OnlyContain(t => t.RevokedAtUtc != null);

        var afterTheft = await handler.Handle(
            new RefreshTokenCommand(firstRefresh.Value.RefreshToken), CancellationToken.None);
        afterTheft.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Refresh_with_an_unknown_token_fails()
    {
        var result = await RefreshHandler().Handle(
            new RefreshTokenCommand("never-issued"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("UNAUTHENTICATED");
    }

    [Fact]
    public async Task Refresh_fails_once_the_token_has_expired()
    {
        await RegisterAsync();
        var login = await LoginHandler().Handle(new LoginCommand(Email, Password), CancellationToken.None);

        _clock.Advance(TimeSpan.FromDays(15));

        var result = await RefreshHandler().Handle(
            new RefreshTokenCommand(login.Value.RefreshToken), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Logout_revokes_the_session_family_so_refresh_stops_working()
    {
        await RegisterAsync();
        var login = await LoginHandler().Handle(new LoginCommand(Email, Password), CancellationToken.None);

        var logout = await new LogoutCommandHandler(_store, _store, _tokens, _clock)
            .Handle(new LogoutCommand(login.Value.RefreshToken), CancellationToken.None);

        logout.IsSuccess.Should().BeTrue();
        var refreshed = await RefreshHandler().Handle(
            new RefreshTokenCommand(login.Value.RefreshToken), CancellationToken.None);
        refreshed.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Email_verification_consumes_the_token_and_marks_the_account_verified()
    {
        await RegisterAsync();
        var handler = new VerifyEmailCommandHandler(_store, _store, _store, _tokens, _clock);

        var result = await handler.Handle(
            new VerifyEmailCommand(_emails.Sent[0].Token), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _store.Users[0].EmailVerified.Should().BeTrue();
        _store.UserTokens[0].ConsumedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task A_verification_token_cannot_be_used_twice()
    {
        await RegisterAsync();
        var handler = new VerifyEmailCommandHandler(_store, _store, _store, _tokens, _clock);
        await handler.Handle(new VerifyEmailCommand(_emails.Sent[0].Token), CancellationToken.None);

        var replay = await handler.Handle(
            new VerifyEmailCommand(_emails.Sent[0].Token), CancellationToken.None);

        replay.IsFailure.Should().BeTrue();
        replay.Error.Code.Should().Be("UNAUTHENTICATED");
    }

    [Fact]
    public async Task An_expired_verification_token_is_rejected()
    {
        await RegisterAsync();
        _clock.Advance(TimeSpan.FromHours(25));

        var result = await new VerifyEmailCommandHandler(_store, _store, _store, _tokens, _clock)
            .Handle(new VerifyEmailCommand(_emails.Sent[0].Token), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Verification_resend_is_rate_limited_to_three_per_hour()
    {
        await RegisterAsync();
        var handler = new ResendVerificationCommandHandler(
            _store, _store, _store, _tokens, _emails, _clock);

        // Registration already issued one token; two more reach the cap of three.
        await handler.Handle(new ResendVerificationCommand(Email), CancellationToken.None);
        await handler.Handle(new ResendVerificationCommand(Email), CancellationToken.None);
        var fourth = await handler.Handle(new ResendVerificationCommand(Email), CancellationToken.None);

        fourth.IsFailure.Should().BeTrue();
        fourth.Error.Code.Should().Be("RATE_LIMITED");

        _clock.Advance(TimeSpan.FromHours(1.1));
        var afterWindow = await handler.Handle(new ResendVerificationCommand(Email), CancellationToken.None);
        afterWindow.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Resending_to_an_unknown_email_succeeds_silently()
    {
        var result = await new ResendVerificationCommandHandler(
                _store, _store, _store, _tokens, _emails, _clock)
            .Handle(new ResendVerificationCommand("nobody@example.com"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _emails.Sent.Should().BeEmpty();
    }
}
