using Dpm.BuildingBlocks.Infrastructure;
using Dpm.Marketplace.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dpm.Marketplace.Infrastructure.Persistence;

public sealed class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> builder)
    {
        builder.ToTable("Stores", "market");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.PublicId).IsRequired();
        builder.HasIndex(s => s.PublicId).IsUnique();
        builder.Property(s => s.Slug).HasMaxLength(80).IsRequired();
        builder.HasIndex(s => s.Slug).IsUnique();
        builder.Property(s => s.Name).HasMaxLength(120).IsRequired();
        builder.HasIndex(s => s.OwnerUserId).IsUnique();
        builder.Property(s => s.LogoUrl).HasMaxLength(500);
        builder.Property(s => s.BannerUrl).HasMaxLength(500);
        builder.Property(s => s.Status).HasConversion<byte>();
        builder.Property<byte[]>("RowVersion").IsRowVersion();
        builder.Ignore(s => s.DomainEvents);

        builder.HasOne(s => s.Profile)
            .WithOne()
            .HasForeignKey<SellerProfile>(p => p.StoreId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Profile).IsRequired().AutoInclude();
    }
}

public sealed class SellerProfileConfiguration : IEntityTypeConfiguration<SellerProfile>
{
    public void Configure(EntityTypeBuilder<SellerProfile> builder)
    {
        builder.ToTable("SellerProfiles", "market");
        builder.HasKey(p => p.StoreId);
        builder.Property(p => p.StoreId).ValueGeneratedNever();
        builder.Property(p => p.KycStatus).HasConversion<byte>();
        builder.Property(p => p.LegalName).HasMaxLength(200);
        builder.Property(p => p.TaxIdType).HasMaxLength(20);
        builder.Property(p => p.TaxId).HasMaxLength(50);
        builder.Property(p => p.BankRef).HasMaxLength(200);
        builder.Property(p => p.CommissionOverridePct).HasColumnType("decimal(5,2)");
        builder.Ignore(p => p.HasTaxInfo);
        builder.Ignore(p => p.IsPublishReady);
    }
}

public sealed class MarketplaceOutboxConfiguration : IEntityTypeConfiguration<OutboxMessage>
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
