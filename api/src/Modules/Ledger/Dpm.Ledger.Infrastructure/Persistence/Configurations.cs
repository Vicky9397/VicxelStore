using Dpm.Ledger.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dpm.Ledger.Infrastructure.Persistence;

public sealed class LedgerTransactionConfiguration : IEntityTypeConfiguration<LedgerTransaction>
{
    public void Configure(EntityTypeBuilder<LedgerTransaction> builder)
    {
        builder.ToTable("LedgerTransactions", "ledger");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.PublicId).IsRequired();
        builder.HasIndex(t => t.PublicId).IsUnique();
        builder.Property(t => t.RefType).HasMaxLength(30).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(300);
        builder.HasIndex(t => new { t.RefType, t.RefId });
        builder.Ignore(t => t.DomainEvents);
        builder.Ignore(t => t.IsBalanced);

        builder.HasMany(t => t.Lines)
            .WithOne()
            .HasForeignKey(l => l.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(t => t.Lines).UsePropertyAccessMode(PropertyAccessMode.Field).HasField("_lines");
    }
}

public sealed class LedgerLineConfiguration : IEntityTypeConfiguration<LedgerLine>
{
    public void Configure(EntityTypeBuilder<LedgerLine> builder)
    {
        builder.ToTable("LedgerLines", "ledger");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Account).HasMaxLength(60).IsRequired();
        builder.Property(l => l.Debit).HasColumnType("decimal(19,4)");
        builder.Property(l => l.Credit).HasColumnType("decimal(19,4)");
        builder.Property(l => l.Currency).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.HasIndex(l => new { l.SellerStoreId, l.Account });
    }
}
