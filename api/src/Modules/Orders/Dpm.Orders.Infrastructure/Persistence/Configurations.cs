using Dpm.BuildingBlocks.Infrastructure;
using Dpm.Orders.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dpm.Orders.Infrastructure.Persistence;

public sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("Carts", "orders");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.PublicId).IsRequired();
        builder.HasIndex(c => c.PublicId).IsUnique();
        builder.HasIndex(c => c.UserId).IsUnique();
        builder.Ignore(c => c.DomainEvents);
        builder.Ignore(c => c.PayableItems);

        builder.HasMany(c => c.Items)
            .WithOne()
            .HasForeignKey(i => i.CartId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Items).UsePropertyAccessMode(PropertyAccessMode.Field).HasField("_items");
    }
}

public sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("CartItems", "orders");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.PriceSnapshotAmount).HasColumnType("decimal(19,4)");
        builder.Property(i => i.PriceSnapshotCurrency).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.HasIndex(i => new { i.CartId, i.VariantId }).IsUnique();
        builder.Ignore(i => i.Price);
    }
}

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", "orders");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.PublicId).IsRequired();
        builder.HasIndex(o => o.PublicId).IsUnique();
        builder.Property(o => o.Currency).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(o => o.Subtotal).HasColumnType("decimal(19,4)");
        builder.Property(o => o.DiscountTotal).HasColumnType("decimal(19,4)");
        builder.Property(o => o.TaxTotal).HasColumnType("decimal(19,4)");
        builder.Property(o => o.GrandTotal).HasColumnType("decimal(19,4)");
        builder.Property(o => o.Status).HasConversion<byte>();
        builder.Property(o => o.InvoiceNo).HasMaxLength(40);
        builder.Property(o => o.BillingCountry).HasMaxLength(2).IsFixedLength();
        builder.Property<byte[]>("RowVersion").IsRowVersion();
        builder.HasIndex(o => o.BuyerUserId);
        builder.Ignore(o => o.DomainEvents);
        builder.Ignore(o => o.IsPaid);
        builder.Ignore(o => o.TotalsBalance);

        builder.HasMany(o => o.Lines)
            .WithOne()
            .HasForeignKey(l => l.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(o => o.Lines).UsePropertyAccessMode(PropertyAccessMode.Field).HasField("_lines");

        builder.HasMany(o => o.Licenses)
            .WithOne()
            .HasForeignKey(l => l.OrderLineId)
            .HasPrincipalKey(o => o.Id)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(o => o.Licenses).UsePropertyAccessMode(PropertyAccessMode.Field).HasField("_licenses");
    }
}

public sealed class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        builder.ToTable("OrderLines", "orders");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.UnitAmount).HasColumnType("decimal(19,4)");
        builder.Property(l => l.Currency).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(l => l.CommissionPct).HasColumnType("decimal(5,2)");
        builder.HasIndex(l => l.StoreId);
        builder.Ignore(l => l.UnitPrice);
        builder.Ignore(l => l.Commission);
        builder.Ignore(l => l.SellerEarning);

        // DownloadLimit is carried on the line so the license keeps the quota the
        // buyer paid for; the spec's OrderLines table predates that need, so it
        // lives in its own additive table rather than altering a spec column.
        builder.SplitToTable("OrderLineTerms", "orders", split =>
        {
            split.Property(l => l.DownloadLimit).HasColumnName("DownloadLimit");
        });
    }
}

public sealed class LicenseConfiguration : IEntityTypeConfiguration<License>
{
    public void Configure(EntityTypeBuilder<License> builder)
    {
        builder.ToTable("Licenses", "orders");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.PublicId).IsRequired();
        builder.HasIndex(l => l.PublicId).IsUnique();
        builder.HasIndex(l => l.BuyerUserId);
        builder.Ignore(l => l.HasQuotaRemaining);
    }
}

public sealed class OrdersOutboxConfiguration : IEntityTypeConfiguration<OutboxMessage>
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
