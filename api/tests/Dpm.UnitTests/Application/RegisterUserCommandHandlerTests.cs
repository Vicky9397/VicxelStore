using Dpm.Identity.Application.Register;
using Dpm.Identity.Domain;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace Dpm.UnitTests.Application;

public sealed class RegisterUserCommandHandlerTests
{
    private readonly TestClock _clock = new();
    private readonly FakeIdentityStore _store = new();
    private readonly RecordingEmailComposer _emails = new();
    private readonly FakeTokenService _tokens = new();

    private RegisterUserCommandHandler CreateHandler() =>
        new(_store, _store, _store, new FakePasswordHasher(), _tokens, _emails, _clock);

    [Fact]
    public async Task Registration_creates_an_unverified_account_and_sends_a_verification_email()
    {
        var result = await CreateHandler().Handle(
            new RegisterUserCommand("buyer@example.com", "a-long-enough-password", "Buyer One"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _store.Users.Should().ContainSingle();
        _store.Users[0].EmailVerified.Should().BeFalse();
        _store.UserTokens.Should().ContainSingle()
            .Which.Purpose.Should().Be(TokenPurpose.EmailVerification);
        _emails.Sent.Should().ContainSingle()
            .Which.Email.Should().Be("buyer@example.com");
    }

    [Fact]
    public async Task The_verification_token_is_stored_hashed_never_raw()
    {
        await CreateHandler().Handle(
            new RegisterUserCommand("buyer@example.com", "a-long-enough-password", "Buyer One"),
            CancellationToken.None);

        var sentToken = _emails.Sent[0].Token;
        _store.UserTokens[0].TokenHash.Should().NotBe(sentToken);
        _store.UserTokens[0].TokenHash.Should().Be(_tokens.HashOpaqueToken(sentToken));
    }

    [Fact]
    public async Task The_verification_token_expires_in_24_hours()
    {
        await CreateHandler().Handle(
            new RegisterUserCommand("buyer@example.com", "a-long-enough-password", "Buyer One"),
            CancellationToken.None);

        _store.UserTokens[0].ExpiresAtUtc.Should().Be(_clock.UtcNow.AddHours(24));
    }

    [Fact]
    public async Task Registering_an_existing_email_succeeds_without_disclosing_or_duplicating()
    {
        var command = new RegisterUserCommand("buyer@example.com", "a-long-enough-password", "Buyer One");
        var handler = CreateHandler();
        await handler.Handle(command, CancellationToken.None);

        var result = await handler.Handle(
            new RegisterUserCommand("BUYER@example.com", "another-long-password", "Impostor"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _store.Users.Should().ContainSingle();
        _emails.Sent.Should().ContainSingle();
    }
}

public sealed class RegisterUserCommandValidatorTests
{
    private readonly RegisterUserCommandValidator _validator = new();

    [Fact]
    public void A_password_under_twelve_characters_is_rejected_as_weak()
    {
        var result = _validator.TestValidate(
            new RegisterUserCommand("buyer@example.com", "short", "Buyer One"));

        result.ShouldHaveValidationErrorFor(c => c.Password)
            .WithErrorCode("WEAK_PASSWORD");
    }

    [Fact]
    public void A_password_equal_to_the_email_local_part_is_rejected()
    {
        var result = _validator.TestValidate(
            new RegisterUserCommand("averylongbuyername@example.com", "averylongbuyername", "Buyer One"));

        result.ShouldHaveValidationErrorFor("password");
    }

    [Fact]
    public void A_malformed_email_is_rejected()
    {
        var result = _validator.TestValidate(
            new RegisterUserCommand("not-an-email", "a-long-enough-password", "Buyer One"));

        result.ShouldHaveValidationErrorFor(c => c.Email);
    }

    [Fact]
    public void A_compliant_registration_passes_validation()
    {
        var result = _validator.TestValidate(
            new RegisterUserCommand("buyer@example.com", "a-long-enough-password", "Buyer One"));

        result.ShouldNotHaveAnyValidationErrors();
    }
}
