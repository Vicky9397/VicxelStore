using Dpm.BuildingBlocks.Infrastructure;
using Dpm.Payments.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dpm.Payments.Infrastructure.Persistence;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments", "pay");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.PublicId).IsRequired();
        builder.HasIndex(p => p.PublicId).IsUnique();
        builder.Property(p => p.Provider).HasMaxLength(20).IsRequired();
        builder.Property(p => p.ProviderIntentId).HasMaxLength(100).IsRequired();
        builder.HasIndex(p => p.ProviderIntentId);
        builder.Property(p => p.ProviderChargeId).HasMaxLength(100);
        builder.Property(p => p.Amount).HasColumnType("decimal(19,4)");
        builder.Property(p => p.Currency).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(p => p.FxRate).HasColumnType("decimal(19,8)");
        builder.Property(p => p.SettlementCurrency).HasMaxLength(3).IsFixedLength();
        builder.Property(p => p.Status).HasConversion<byte>();
        builder.HasIndex(p => p.OrderId);
        builder.Ignore(p => p.DomainEvents);
        builder.Ignore(p => p.Money);
    }
}

public sealed class WebhookInboxConfiguration : IEntityTypeConfiguration<WebhookInboxEntry>
{
    public void Configure(EntityTypeBuilder<WebhookInboxEntry> builder)
    {
        builder.ToTable("WebhookInbox", "pay");
        builder.HasKey(e => e.ProviderEventId);
        builder.Property(e => e.ProviderEventId).HasMaxLength(120);
        builder.Property(e => e.Provider).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Type).HasMaxLength(60).IsRequired();
        builder.Property(e => e.PayloadJson).IsRequired();
    }
}

public sealed class IdempotencyConfiguration : IEntityTypeConfiguration<IdempotencyEntry>
{
    public void Configure(EntityTypeBuilder<IdempotencyEntry> builder)
    {
        builder.ToTable("Idempotency", "pay");
        builder.HasKey(e => new { e.KeyHash, e.Endpoint });
        builder.Property(e => e.KeyHash).HasMaxLength(64).IsFixedLength();
        builder.Property(e => e.Endpoint).HasMaxLength(120);
        builder.Property(e => e.ResponseJson).IsRequired();
    }
}

public sealed class PaymentsOutboxConfiguration : IEntityTypeConfiguration<OutboxMessage>
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
