using Dpm.Files.Domain;
using FluentAssertions;

namespace Dpm.UnitTests.Files;

public sealed class FileUploadTests
{
    private const string ValidChecksum = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    private readonly TestClock _clock = new();

    private FileUpload Start(long sizeBytes = 20 * 1024 * 1024, int partSize = 8 * 1024 * 1024) =>
        FileUpload.Start(1, 2, 3, "kit.zip", sizeBytes, ValidChecksum, "quarantine/abc", _clock, partSize).Value;

    [Fact]
    public void The_part_count_covers_the_whole_file()
    {
        var upload = Start(sizeBytes: 20 * 1024 * 1024, partSize: 8 * 1024 * 1024);

        upload.TotalParts.Should().Be(3, "20 MB in 8 MB parts needs three parts");
        upload.MissingParts.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void A_file_over_the_twenty_gigabyte_ceiling_is_rejected()
    {
        FileUpload.Start(1, 2, 3, "huge.zip", FileUpload.MaxSizeBytes + 1, ValidChecksum, "k", _clock)
            .IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B85")]
    public void A_malformed_checksum_is_rejected(string checksum)
    {
        FileUpload.Start(1, 2, 3, "kit.zip", 1024, checksum, "k", _clock)
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public void A_declared_checksum_is_stored_lowercased()
    {
        var upload = FileUpload.Start(
            1, 2, 3, "kit.zip", 1024, ValidChecksum.ToUpperInvariant(), "k", _clock).Value;

        upload.DeclaredChecksum.Should().Be(ValidChecksum);
    }

    [Fact]
    public void A_part_number_outside_the_range_is_rejected()
    {
        var upload = Start();

        upload.AcceptPart(0, 1024, ValidChecksum, _clock).IsFailure.Should().BeTrue();
        upload.AcceptPart(upload.TotalParts + 1, 1024, ValidChecksum, _clock).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void A_part_larger_than_the_part_size_is_rejected()
    {
        var upload = Start(partSize: 1024);

        upload.AcceptPart(1, 2048, ValidChecksum, _clock).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Re_sending_a_part_replaces_it_so_a_resumed_upload_recovers()
    {
        var upload = Start();
        upload.AcceptPart(1, 1024, ValidChecksum, _clock);

        upload.AcceptPart(1, 2048, ValidChecksum, _clock).IsSuccess.Should().BeTrue();

        upload.Parts.Should().ContainSingle();
        upload.Parts[0].SizeBytes.Should().Be(2048);
    }

    [Fact]
    public void Completion_is_refused_while_parts_are_missing()
    {
        var upload = Start();
        upload.AcceptPart(1, 1024, ValidChecksum, _clock);

        var complete = upload.Complete(ValidChecksum, upload.DeclaredSizeBytes, _clock);

        complete.IsFailure.Should().BeTrue();
        complete.Error.Message.Should().Contain("missing part");
    }

    [Fact]
    public void Completion_is_refused_when_the_assembled_checksum_differs()
    {
        var upload = Start(sizeBytes: 1024, partSize: 1024);
        upload.AcceptPart(1, 1024, ValidChecksum, _clock);

        var tampered = new string('a', 64);
        var complete = upload.Complete(tampered, 1024, _clock);

        complete.IsFailure.Should().BeTrue();
        complete.Error.Message.Should().Contain("checksum");
        upload.Status.Should().Be(UploadStatus.InProgress);
    }

    [Fact]
    public void Completion_is_refused_when_the_assembled_size_differs()
    {
        var upload = Start(sizeBytes: 1024, partSize: 1024);
        upload.AcceptPart(1, 1024, ValidChecksum, _clock);

        upload.Complete(ValidChecksum, 2048, _clock).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void A_complete_and_matching_upload_closes_the_session()
    {
        var upload = Start(sizeBytes: 1024, partSize: 1024);
        upload.AcceptPart(1, 1024, ValidChecksum, _clock);

        var complete = upload.Complete(ValidChecksum, 1024, _clock);

        complete.IsSuccess.Should().BeTrue();
        upload.Status.Should().Be(UploadStatus.Completed);
        upload.CompletedAtUtc.Should().Be(_clock.UtcNow);
    }

    [Fact]
    public void A_completed_session_accepts_no_further_parts()
    {
        var upload = Start(sizeBytes: 1024, partSize: 1024);
        upload.AcceptPart(1, 1024, ValidChecksum, _clock);
        upload.Complete(ValidChecksum, 1024, _clock);

        upload.AcceptPart(1, 1024, ValidChecksum, _clock).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Completing_twice_is_a_no_op_rather_than_an_error()
    {
        var upload = Start(sizeBytes: 1024, partSize: 1024);
        upload.AcceptPart(1, 1024, ValidChecksum, _clock);
        upload.Complete(ValidChecksum, 1024, _clock);

        upload.Complete(ValidChecksum, 1024, _clock).IsSuccess.Should().BeTrue();
    }
}
