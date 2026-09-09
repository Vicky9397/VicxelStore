using Dpm.Ledger.Application.Abstractions;
using Dpm.Ledger.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dpm.Ledger.Infrastructure.Persistence;

public sealed class LedgerRepository(LedgerDbContext dbContext) : ILedgerRepository
{
    public Task<bool> ExistsForReferenceAsync(string refType, long refId, CancellationToken ct) =>
        dbContext.Transactions.AnyAsync(t => t.RefType == refType && t.RefId == refId, ct);

    public void Add(LedgerTransaction transaction) => dbContext.Transactions.Add(transaction);
}

public sealed class LedgerUnitOfWork(LedgerDbContext dbContext) : ILedgerUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
