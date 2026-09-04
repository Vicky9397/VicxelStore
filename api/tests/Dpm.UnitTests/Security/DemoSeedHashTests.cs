using Dpm.Identity.Infrastructure.Security;
using FluentAssertions;

namespace Dpm.UnitTests.Security;

public sealed class DemoSeedHashTests
{
    /// <summary>
    /// The demo seed (db/seed/demo) stores a precomputed hash so the dataset is
    /// deterministic. This pins it to the hasher the API actually uses, so a
    /// change to the hashing parameters cannot silently strand the demo logins.
    /// </summary>
    private const string SeededDemoPasswordHash =
        "pbkdf2-sha256.210000.NH3eeLY8GdLiUMgLNZwv5w==.WEY6pYGm3du70l/QUgTS5br8Ijb9eGndf0xxtO26Ogw=";

    [Fact]
    public void The_seeded_demo_password_hash_verifies_against_the_documented_password()
    {
        new Pbkdf2PasswordHasher()
            .Verify("DemoPassword123!", SeededDemoPasswordHash)
            .Should().BeTrue();
    }
}
