using Dpm.Files.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dpm.Files.Infrastructure.Persistence;

public sealed class FileUploadConfiguration : IEntityTypeConfiguration<FileUpload>
{
    public void Configure(EntityTypeBuilder<FileUpload> builder)
    {
        builder.ToTable("FileUploads", "files");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.PublicId).IsRequired();
        builder.HasIndex(u => u.PublicId).IsUnique();
        builder.Property(u => u.FileName).HasMaxLength(260).IsRequired();
        builder.Property(u => u.DeclaredChecksum).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(u => u.QuarantineKey).HasMaxLength(500).IsRequired();
        builder.Property(u => u.Status).HasConversion<byte>();
        builder.Property<byte[]>("RowVersion").IsRowVersion();
        builder.Ignore(u => u.DomainEvents);
        builder.Ignore(u => u.HasAllParts);
        builder.Ignore(u => u.MissingParts);

        builder.HasMany(u => u.Parts)
            .WithOne()
            .HasForeignKey(p => p.UploadId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(u => u.Parts).UsePropertyAccessMode(PropertyAccessMode.Field).HasField("_parts");
    }
}

public sealed class FileUploadPartConfiguration : IEntityTypeConfiguration<FileUploadPart>
{
    public void Configure(EntityTypeBuilder<FileUploadPart> builder)
    {
        builder.ToTable("FileUploadParts", "files");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Checksum).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.HasIndex(p => new { p.UploadId, p.PartNumber }).IsUnique();
    }
}

public sealed class ProductFileConfiguration : IEntityTypeConfiguration<ProductFile>
{
    public void Configure(EntityTypeBuilder<ProductFile> builder)
    {
        builder.ToTable("ProductFiles", "files");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.PublicId).IsRequired();
        builder.HasIndex(f => f.PublicId).IsUnique();
        builder.Property(f => f.StorageKey).HasMaxLength(500).IsRequired();
        builder.Property(f => f.FileName).HasMaxLength(260).IsRequired();
        builder.Property(f => f.Checksum).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(f => f.ScanStatus).HasConversion<byte>();
        builder.HasIndex(f => f.VariantId);
        builder.Ignore(f => f.DomainEvents);
        builder.Ignore(f => f.IsDownloadable);
    }
}
