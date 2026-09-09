using Dpm.BuildingBlocks.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Dpm.Outbox;

public sealed class OutboxDbContext(DbContextOptions<OutboxDbContext> options) : DbContext(options)
{
    public DbSet<OutboxMessage> Messages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var message = modelBuilder.Entity<OutboxMessage>();
        message.ToTable("OutboxMessages", "dbo");
        message.HasKey(m => m.Id);
        message.HasIndex(m => m.EventId).IsUnique();
        message.Property(m => m.Type).HasMaxLength(200).IsRequired();
        message.Property(m => m.PayloadJson).IsRequired();
    }
}
