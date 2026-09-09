using Dpm.Downloads.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Dpm.UnitTests.Downloads;

public sealed class SignedUrlFactoryTests
{
    private readonly TestClock _clock = new();

    private SignedUrlFactory CreateFactory() => new(
        Options.Create(new DownloadOptions
        {
            BaseUrl = "https://cdn.example/object",
            SigningKey = "download-test-key",
        }),
        _clock);

    private static long ExpiryOf(string url) =>
        long.Parse(
            System.Web.HttpUtility.ParseQueryString(new Uri(url).Query)["expires"]!,
            System.Globalization.CultureInfo.InvariantCulture);

    private static string SignatureOf(string url) =>
        System.Web.HttpUtility.ParseQueryString(new Uri(url).Query)["sig"]!;

    [Fact]
    public void A_freshly_minted_url_verifies()
    {
        var factory = CreateFactory();

        var signed = factory.Create("clean/abc", "kit.zip", TimeSpan.FromMinutes(15));

        factory.Verify("clean/abc", ExpiryOf(signed.Url), SignatureOf(signed.Url))
            .Should().BeTrue();
    }

    [Fact]
    public void A_url_edited_to_reach_another_object_fails_verification()
    {
        var factory = CreateFactory();
        var signed = factory.Create("clean/abc", "kit.zip", TimeSpan.FromMinutes(15));

        factory.Verify("clean/someone-elses-file", ExpiryOf(signed.Url), SignatureOf(signed.Url))
            .Should().BeFalse();
    }

    [Fact]
    public void A_url_edited_to_extend_its_life_fails_verification()
    {
        var factory = CreateFactory();
        var signed = factory.Create("clean/abc", "kit.zip", TimeSpan.FromMinutes(15));

        factory.Verify("clean/abc", ExpiryOf(signed.Url) + 86_400, SignatureOf(signed.Url))
            .Should().BeFalse();
    }

    [Fact]
    public void A_url_stops_verifying_once_its_window_passes()
    {
        var factory = CreateFactory();
        var signed = factory.Create("clean/abc", "kit.zip", TimeSpan.FromMinutes(15));
        var expiry = ExpiryOf(signed.Url);
        var signature = SignatureOf(signed.Url);

        _clock.Advance(TimeSpan.FromMinutes(16));

        factory.Verify("clean/abc", expiry, signature).Should().BeFalse();
    }

    [Fact]
    public void The_raw_storage_key_is_never_handed_over_unsigned()
    {
        var signed = CreateFactory().Create("clean/abc", "kit.zip", TimeSpan.FromMinutes(15));

        signed.Url.Should().Contain("sig=");
        signed.Url.Should().Contain("expires=");
    }
}
