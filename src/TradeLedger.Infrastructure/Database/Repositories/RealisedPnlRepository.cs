using Microsoft.EntityFrameworkCore;
using TradeLedger.Application.Interfaces;

namespace TradeLedger.Infrastructure.Database.Repositories;

internal sealed class RealisedPnlRepository(TradeLedgerDbContext dbContext) : IRealisedPnlRepository
{
    public async Task<decimal> GetTotalAsync(
        string symbol,
        DateTimeOffset? fromInclusive,
        DateTimeOffset? toExclusive,
        CancellationToken cancellationToken)
    {
        var entries = dbContext.RealisedPnlEntries
            .AsNoTracking()
            .Where(entry => entry.Symbol == symbol);

        if (fromInclusive.HasValue)
        {
            entries = entries.Where(entry => entry.RealisedAt >= fromInclusive.Value);
        }

        if (toExclusive.HasValue)
        {
            entries = entries.Where(entry => entry.RealisedAt < toExclusive.Value);
        }

        return await entries.SumAsync(entry => entry.Amount, cancellationToken);
    }
}
