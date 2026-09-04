using Dpm.Identity.Domain;
using FluentAssertions;

namespace Dpm.UnitTests.Domain;

public sealed class RefreshTokenTests
{
    private readonly TestClock _clock = new();

    private RefreshToken IssueToken(string hash = "hash-a") =>
        RefreshToken.Issue(1, Guid.NewGuid(), hash, _clock.UtcNow.AddDays(14), _clock);

    [Fact]
    public void A_freshly_issued_token_is_active_and_not_reused()
    {
        var token = IssueToken();

        token.IsActive(_clock).Should().BeTrue();
        token.IsReused(_clock).Should().BeFalse();
    }

    [Fact]
    public void A_rotated_token_stops_being_active_and_counts_as_reuse_when_presented_again()
    {
        var token = IssueToken();

        token.MarkRotated("hash-b", _clock);

        token.IsActive(_clock).Should().BeFalse();
        token.IsReused(_clock).Should().BeTrue();
        token.ReplacedByHash.Should().Be("hash-b");
    }

    [Fact]
    public void A_revoked_token_is_not_active()
    {
        var token = IssueToken();

        token.Revoke(_clock);

        token.IsActive(_clock).Should().BeFalse();
        token.RevokedAtUtc.Should().Be(_clock.UtcNow);
    }

    [Fact]
    public void Revoke_keeps_the_original_revocation_timestamp()
    {
        var token = IssueToken();
        token.Revoke(_clock);
        var firstRevocation = token.RevokedAtUtc;

        _clock.Advance(TimeSpan.FromMinutes(5));
        token.Revoke(_clock);

        token.RevokedAtUtc.Should().Be(firstRevocation);
    }

    [Fact]
    public void An_expired_token_is_neither_active_nor_reusable_evidence()
    {
        var token = IssueToken();
        token.MarkRotated("hash-b", _clock);

        _clock.Advance(TimeSpan.FromDays(15));

        token.IsActive(_clock).Should().BeFalse();
        token.IsReused(_clock).Should().BeFalse();
    }
}
