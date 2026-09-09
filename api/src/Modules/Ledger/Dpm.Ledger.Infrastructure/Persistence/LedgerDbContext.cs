using Dpm.Ledger.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dpm.Ledger.Infrastructure.Persistence;

/// <summary>
/// Ledger persistence. There is deliberately no update or delete path: the
/// tables are append-only and corrections are new reversing transactions
/// (spec 11B section 12).
/// </summary>
public sealed class LedgerDbContext(DbContextOptions<LedgerDbContext> options) : DbContext(options)
{
    public DbSet<LedgerTransaction> Transactions => Set<LedgerTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LedgerDbContext).Assembly);
}
