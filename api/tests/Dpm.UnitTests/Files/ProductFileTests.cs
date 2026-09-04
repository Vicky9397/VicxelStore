using Dpm.Files.Domain;
using FluentAssertions;

namespace Dpm.UnitTests.Files;

public sealed class ProductFileTests
{
    private readonly TestClock _clock = new();

    private ProductFile CreateFile() =>
        ProductFile.CreatePending(1, 2, "quarantine/abc", "kit.zip", 1024, "ABC123", _clock);

    [Fact]
    public void A_newly_stored_file_is_pending_and_not_downloadable()
    {
        var file = CreateFile();

        file.ScanStatus.Should().Be(ScanStatus.Pending);
        file.IsDownloadable.Should().BeFalse();
        file.Checksum.Should().Be("abc123", "checksums are normalized to lowercase");
    }

    [Fact]
    public void A_clean_scan_promotes_the_file_out_of_quarantine()
    {
        var file = CreateFile();

        file.MarkScanned(isClean: true, "clean/abc");

        file.ScanStatus.Should().Be(ScanStatus.Clean);
        file.IsDownloadable.Should().BeTrue();
        file.StorageKey.Should().Be("clean/abc");
    }

    [Fact]
    public void An_infected_file_stays_quarantined_and_never_becomes_downloadable()
    {
        var file = CreateFile();

        file.MarkScanned(isClean: false, "clean/abc");

        file.ScanStatus.Should().Be(ScanStatus.Infected);
        file.IsDownloadable.Should().BeFalse();
        file.StorageKey.Should().Be("quarantine/abc", "an infected file is never moved to the clean bucket");
    }

    [Fact]
    public void A_second_scan_report_cannot_flip_an_infected_file_to_clean()
    {
        var file = CreateFile();
        file.MarkScanned(isClean: false, "clean/abc");

        file.MarkScanned(isClean: true, "clean/abc");

        file.ScanStatus.Should().Be(ScanStatus.Infected);
        file.IsDownloadable.Should().BeFalse();
    }
}
