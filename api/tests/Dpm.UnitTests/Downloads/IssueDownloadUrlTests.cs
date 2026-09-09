using Dpm.Downloads.Application;
using Dpm.Downloads.Application.Abstractions;
using Dpm.Files.Contracts;
using Dpm.Orders.Contracts;
using FluentAssertions;

namespace Dpm.UnitTests.Downloads;

public sealed class IssueDownloadUrlTests
{
    private const long BuyerUserId = 42;
    private static readonly Guid LicenseId = Guid.NewGuid();
    private static readonly Guid FileId = Guid.NewGuid();

    private readonly TestClock _clock = new();
    private readonly FakeOrderDirectory _orders = new();
    private readonly FakeFileDirectory _files = new();
    private readonly RecordingDownloadLog _log = new();

    private IssueDownloadUrlQueryHandler CreateHandler(long? callerUserId = BuyerUserId) =>
        new(_orders, _files, new StubSignedUrlFactory(), _log, new StubDownloader(callerUserId), _clock);

    private void GivenLicense(int limit = 5, int used = 0, DateTime? expiresAt = null) =>
        _orders.License = new LicenseSnapshot(
            1, LicenseId, BuyerUserId, VariantId: 10, VersionId: 3, limit, used, expiresAt);

    private void GivenFile(string scanStatus = "Clean", long variantId = 10) =>
        _files.File = new StoredFile(
            5, FileId, variantId, "clean/abc", "kit.zip", 1024, scanStatus, scanStatus == "Clean");

    private Task<BuildingBlocks.Application.Result<DownloadUrlDto>> Issue(long? caller = BuyerUserId) =>
        CreateHandler(caller).Handle(new IssueDownloadUrlQuery(LicenseId, FileId), CancellationToken.None);

    [Fact]
    public async Task A_valid_license_and_clean_file_yields_a_short_lived_url()
    {
        GivenLicense();
        GivenFile();

        var result = await Issue();

        result.IsSuccess.Should().BeTrue();
        result.Value.Url.Should().Contain("clean/abc");
        result.Value.ExpiresInSeconds.Should().Be(900, "signed URLs live 15 minutes");
        _orders.DownloadsConsumed.Should().Be(1);
        _log.Records.Should().ContainSingle();
    }

    [Fact]
    public async Task An_anonymous_caller_is_refused()
    {
        GivenLicense();
        GivenFile();

        var result = await Issue(caller: null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("UNAUTHENTICATED");
    }

    [Fact]
    public async Task Another_buyers_license_is_refused_as_no_entitlement()
    {
        GivenLicense();
        GivenFile();

        var result = await Issue(caller: 99);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("NO_ENTITLEMENT");
        _orders.DownloadsConsumed.Should().Be(0);
    }

    [Fact]
    public async Task A_missing_license_is_refused_as_no_entitlement_not_not_found()
    {
        GivenFile();

        var result = await Issue();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("NO_ENTITLEMENT",
            "the response must not reveal whether someone else's license exists");
    }

    [Fact]
    public async Task A_file_belonging_to_another_variant_is_refused()
    {
        GivenLicense();
        GivenFile(variantId: 999);

        var result = await Issue();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("NO_ENTITLEMENT");
    }

    [Fact]
    public async Task An_expired_license_is_refused()
    {
        GivenLicense(expiresAt: _clock.UtcNow.AddDays(-1));
        GivenFile();

        var result = await Issue();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("NO_ENTITLEMENT");
    }

    [Fact]
    public async Task An_exhausted_quota_is_refused_with_the_limit_code()
    {
        GivenLicense(limit: 3, used: 3);
        GivenFile();

        var result = await Issue();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DOWNLOAD_LIMIT");
        _orders.DownloadsConsumed.Should().Be(0);
    }

    [Fact]
    public async Task A_file_still_scanning_reports_not_ready_rather_than_a_broken_link()
    {
        GivenLicense();
        GivenFile(scanStatus: "Pending");

        var result = await Issue();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FILE_NOT_READY");
        _orders.DownloadsConsumed.Should().Be(0, "a refused download must not spend quota");
    }

    [Fact]
    public async Task A_quarantined_file_is_never_served_even_to_its_owner()
    {
        GivenLicense();
        GivenFile(scanStatus: "Infected");

        var result = await Issue();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FILE_QUARANTINED");
        _log.Records.Should().BeEmpty();
    }

    [Fact]
    public async Task Losing_the_race_to_spend_quota_yields_no_url()
    {
        GivenLicense();
        GivenFile();
        _orders.ConsumeSucceeds = false;

        var result = await Issue();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DOWNLOAD_LIMIT");
        _log.Records.Should().BeEmpty();
    }

    private sealed class FakeOrderDirectory : IOrderDirectory
    {
        public LicenseSnapshot? License { get; set; }

        public bool ConsumeSucceeds { get; set; } = true;

        public int DownloadsConsumed { get; private set; }

        public Task<OrderSnapshot?> FindOrderAsync(long orderId, CancellationToken ct) =>
            Task.FromResult<OrderSnapshot?>(null);

        public Task<LicenseSnapshot?> FindLicenseAsync(Guid licensePublicId, CancellationToken ct) =>
            Task.FromResult(License is not null && License.PublicId == licensePublicId ? License : null);

        public Task<bool> TryConsumeDownloadAsync(Guid licensePublicId, CancellationToken ct)
        {
            if (!ConsumeSucceeds)
            {
                return Task.FromResult(false);
            }

            DownloadsConsumed++;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeFileDirectory : IFileDirectory
    {
        public StoredFile? File { get; set; }

        public Task<StoredFile?> FindFileAsync(Guid filePublicId, CancellationToken ct) =>
            Task.FromResult(File is not null && File.PublicId == filePublicId ? File : null);

        public Task<IReadOnlyList<StoredFile>> ListDownloadableAsync(long variantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<StoredFile>>(
                File is not null && File.VariantId == variantId && File.IsDownloadable ? [File] : []);
    }

    private sealed class StubSignedUrlFactory : ISignedUrlFactory
    {
        public SignedUrl Create(string storageKey, string fileName, TimeSpan lifetime) =>
            new($"https://cdn.example/{storageKey}?sig=test", (int)lifetime.TotalSeconds);
    }

    private sealed class RecordingDownloadLog : IDownloadLog
    {
        public List<(long LicenseId, long FileId, long UserId)> Records { get; } = [];

        public Task RecordAsync(long licenseId, long fileId, long userId, string? ipHash, CancellationToken ct)
        {
            Records.Add((licenseId, fileId, userId));
            return Task.CompletedTask;
        }
    }

    private sealed class StubDownloader(long? userId) : ICurrentDownloader
    {
        public Task<long?> ResolveUserIdAsync(CancellationToken ct) => Task.FromResult(userId);

        public string? ClientIpHash => "hashed-ip";
    }
}
