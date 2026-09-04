using Dpm.Identity.Application.Me;
using Dpm.Identity.Application.Register;
using FluentAssertions;

namespace Dpm.UnitTests.Application;

public sealed class GetMeQueryHandlerTests
{
    private readonly TestClock _clock = new();
    private readonly FakeIdentityStore _store = new();

    private async Task<Guid> RegisterAsync()
    {
        await new RegisterUserCommandHandler(
                _store, _store, _store, new FakePasswordHasher(), new FakeTokenService(),
                new RecordingEmailComposer(), _clock)
            .Handle(
                new RegisterUserCommand("buyer@example.com", "a-long-enough-password", "Buyer One"),
                CancellationToken.None);
        return _store.Users[0].PublicId;
    }

    [Fact]
    public async Task Returns_the_authenticated_user_profile()
    {
        var publicId = await RegisterAsync();

        var result = await new GetMeQueryHandler(_store, new StubCurrentUser(publicId))
            .Handle(new GetMeQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(publicId);
        result.Value.Email.Should().Be("buyer@example.com");
        result.Value.EmailVerified.Should().BeFalse();
    }

    [Fact]
    public async Task Fails_when_the_request_carries_no_identity()
    {
        await RegisterAsync();

        var result = await new GetMeQueryHandler(_store, new StubCurrentUser(null))
            .Handle(new GetMeQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("UNAUTHENTICATED");
    }

    [Fact]
    public async Task Fails_when_the_token_names_a_user_that_no_longer_exists()
    {
        var result = await new GetMeQueryHandler(_store, new StubCurrentUser(Guid.NewGuid()))
            .Handle(new GetMeQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("NOT_FOUND");
    }
}
