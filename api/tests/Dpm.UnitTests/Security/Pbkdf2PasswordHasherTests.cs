using Dpm.Identity.Infrastructure.Security;
using FluentAssertions;

namespace Dpm.UnitTests.Security;

public sealed class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void A_hashed_password_verifies_against_itself()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        _hasher.Verify("correct horse battery staple", hash).Should().BeTrue();
    }

    [Fact]
    public void A_different_password_does_not_verify()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        _hasher.Verify("Correct horse battery staple", hash).Should().BeFalse();
    }

    [Fact]
    public void The_same_password_hashes_differently_each_time()
    {
        _hasher.Hash("correct horse battery staple")
            .Should().NotBe(_hasher.Hash("correct horse battery staple"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("pbkdf2-sha256.210000.notbase64!.notbase64!")]
    public void A_malformed_stored_hash_fails_verification_without_throwing(string storedHash)
    {
        _hasher.Verify("any password", storedHash).Should().BeFalse();
    }
}
