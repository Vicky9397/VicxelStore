using Dpm.Identity.Domain;
using Dpm.Identity.Domain.Events;
using FluentAssertions;

namespace Dpm.UnitTests.Domain;

public sealed class UserAccountTests
{
    private readonly TestClock _clock = new();

    [Fact]
    public void Register_creates_an_unverified_account_and_raises_registration_events()
    {
        var user = UserAccount.Register("Buyer@Example.com", "Buyer One", "hash", _clock);

        user.EmailVerified.Should().BeFalse();
        user.Status.Should().Be(UserStatus.Active);
        user.EmailNormalized.Should().Be("BUYER@EXAMPLE.COM");
        user.PublicId.Should().NotBe(Guid.Empty);
        user.DomainEvents.Should().HaveCount(2);
        user.DomainEvents.OfType<UserRegistered>().Should().ContainSingle();
        user.DomainEvents.OfType<EmailVerificationRequested>().Should().ContainSingle();
    }

    [Fact]
    public void VerifyEmail_marks_the_account_verified_and_raises_once()
    {
        var user = UserAccount.Register("buyer@example.com", "Buyer One", "hash", _clock);
        user.ClearDomainEvents();

        user.VerifyEmail(_clock);
        user.VerifyEmail(_clock);

        user.EmailVerified.Should().BeTrue();
        user.DomainEvents.OfType<EmailVerified>().Should().ContainSingle();
    }

    [Fact]
    public void Account_locks_after_the_failed_login_threshold()
    {
        var user = UserAccount.Register("buyer@example.com", "Buyer One", "hash", _clock);

        for (var attempt = 0; attempt < UserAccount.MaxFailedLogins - 1; attempt++)
        {
            user.RecordFailedLogin(_clock);
        }

        user.IsLockedOut(_clock).Should().BeFalse();

        user.RecordFailedLogin(_clock);

        user.IsLockedOut(_clock).Should().BeTrue();
        user.LockoutEndUtc.Should().Be(_clock.UtcNow.AddMinutes(15));
        user.DomainEvents.OfType<UserLockedOut>().Should().ContainSingle();
    }

    [Fact]
    public void Lockout_backoff_doubles_on_each_further_failure()
    {
        var user = UserAccount.Register("buyer@example.com", "Buyer One", "hash", _clock);

        for (var attempt = 0; attempt < UserAccount.MaxFailedLogins; attempt++)
        {
            user.RecordFailedLogin(_clock);
        }

        var firstLockout = user.LockoutEndUtc!.Value;
        user.RecordFailedLogin(_clock);

        (user.LockoutEndUtc!.Value - _clock.UtcNow)
            .Should().Be((firstLockout - _clock.UtcNow) * 2);
    }

    [Fact]
    public void Lockout_expires_once_the_window_passes()
    {
        var user = UserAccount.Register("buyer@example.com", "Buyer One", "hash", _clock);
        for (var attempt = 0; attempt < UserAccount.MaxFailedLogins; attempt++)
        {
            user.RecordFailedLogin(_clock);
        }

        _clock.Advance(TimeSpan.FromMinutes(16));

        user.IsLockedOut(_clock).Should().BeFalse();
    }

    [Fact]
    public void Successful_login_clears_the_failure_counter_and_lockout()
    {
        var user = UserAccount.Register("buyer@example.com", "Buyer One", "hash", _clock);
        for (var attempt = 0; attempt < UserAccount.MaxFailedLogins; attempt++)
        {
            user.RecordFailedLogin(_clock);
        }

        user.RecordSuccessfulLogin(_clock);

        user.FailedLogins.Should().Be(0);
        user.LockoutEndUtc.Should().BeNull();
        user.IsLockedOut(_clock).Should().BeFalse();
    }
}
