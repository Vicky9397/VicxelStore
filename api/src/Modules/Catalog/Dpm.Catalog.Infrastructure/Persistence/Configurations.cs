using Dpm.BuildingBlocks.Infrastructure;
using Dpm.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dpm.Catalog.Infrastructure.Persistence;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", "catalog");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.PublicId).IsRequired();
        builder.HasIndex(p => p.PublicId).IsUnique();
        builder.Property(p => p.Title).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Slug).HasMaxLength(220).IsRequired();
        builder.HasIndex(p => p.Slug);
        builder.Property(p => p.Status).HasConversion<byte>();
        builder.Property(p => p.SeoTitle).HasMaxLength(200);
        builder.Property(p => p.SeoDescription).HasMaxLength(400);
        builder.Property(p => p.RatingAvg).HasColumnType("decimal(3,2)");
        builder.Property<byte[]>("RowVersion").IsRowVersion();
        builder.HasQueryFilter(p => !p.IsDeleted);
        builder.Ignore(p => p.DomainEvents);
        builder.Ignore(p => p.IsPubliclyVisible);

        builder.HasMany(p => p.Variants)
            .WithOne()
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Variants).UsePropertyAccessMode(PropertyAccessMode.Field).HasField("_variants");

        builder.HasMany(p => p.Versions)
            .WithOne()
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Versions).UsePropertyAccessMode(PropertyAccessMode.Field).HasField("_versions");

        builder.HasMany(p => p.Tags)
            .WithMany()
            .UsingEntity(
                "ProductTags",
                right => right.HasOne(typeof(Tag)).WithMany().HasForeignKey("TagId"),
                left => left.HasOne(typeof(Product)).WithMany().HasForeignKey("ProductId"),
                join =>
                {
                    join.ToTable("ProductTags", "catalog");
                    join.HasKey("ProductId", "TagId");
                });
        builder.Navigation(p => p.Tags).UsePropertyAccessMode(PropertyAccessMode.Field).HasField("_tags");
    }
}

public sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("ProductVariants", "catalog");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.PublicId).IsRequired();
        builder.HasIndex(v => v.PublicId).IsUnique();
        builder.Property(v => v.Name).HasMaxLength(120).IsRequired();
        builder.Property(v => v.PriceAmount).HasColumnType("decimal(19,4)");
        builder.Property(v => v.PriceCurrency).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(v => v.LicenseType).HasConversion<byte>();
        builder.Ignore(v => v.Price);
        builder.Ignore(v => v.HasCleanFile);

        // The readiness count lives in its own Catalog-owned table so the
        // spec-defined ProductVariants columns stay exactly as specified.
        builder.SplitToTable("VariantFileReadiness", "catalog", split =>
        {
            split.Property(v => v.CleanFileCount).HasColumnName("CleanFileCount");
            split.Property<DateTime>("ReadinessUpdatedAtUtc").HasColumnName("UpdatedAtUtc");
        });
    }
}

public sealed class ProductVersionConfiguration : IEntityTypeConfiguration<ProductVersion>
{
    public void Configure(EntityTypeBuilder<ProductVersion> builder)
    {
        builder.ToTable("ProductVersions", "catalog");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.VersionNumber).HasMaxLength(20).IsRequired();
    }
}

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories", "catalog");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Slug).HasMaxLength(80).IsRequired();
        builder.HasIndex(c => c.Slug).IsUnique();
        builder.Property(c => c.Name).HasMaxLength(120).IsRequired();
        builder.Property(c => c.CommissionPct).HasColumnType("decimal(5,2)");
    }
}

public sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tags", "catalog");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(60).IsRequired();
        builder.HasIndex(t => t.Name).IsUnique();
    }
}

public sealed class ProductModerationConfiguration : IEntityTypeConfiguration<ProductModeration>
{
    public void Configure(EntityTypeBuilder<ProductModeration> builder)
    {
        builder.ToTable("ProductModerations", "catalog");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Reason).HasMaxLength(1000);
    }
}

public sealed class CatalogOutboxConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages", "dbo");
        builder.HasKey(m => m.Id);
        builder.HasIndex(m => m.EventId).IsUnique();
        builder.Property(m => m.Type).HasMaxLength(200).IsRequired();
        builder.Property(m => m.PayloadJson).IsRequired();
    }
}
