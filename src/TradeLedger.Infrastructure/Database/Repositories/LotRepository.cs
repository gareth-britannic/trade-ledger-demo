using Microsoft.EntityFrameworkCore;
using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Records;
using TradeLedger.Infrastructure.Database.Entities;

namespace TradeLedger.Infrastructure.Database.Repositories;

internal sealed class LotRepository(TradeLedgerDbContext dbContext) : ILotRepository
{
    public async Task<IReadOnlyList<Position>> GetPositionsAsync(CancellationToken cancellationToken)
    {
        var lots = await dbContext.Lots
            .AsNoTracking()
            .Where(lot => lot.RemainingQuantity > 0)
            .OrderBy(lot => lot.Symbol)
            .ThenBy(lot => lot.OpenedAt)
            .ThenBy(lot => lot.Id)
            .ToListAsync(cancellationToken);
        var realisedPnl = await dbContext.RealisedPnlEntries
            .AsNoTracking()
            .GroupBy(entry => entry.Symbol)
            .Select(entries => new { Symbol = entries.Key, Amount = entries.Sum(entry => entry.Amount) })
            .ToDictionaryAsync(entry => entry.Symbol, entry => entry.Amount, cancellationToken);

        return lots.Select(lot => lot.Symbol)
            .Concat(realisedPnl.Keys)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(symbol => MapPosition(
                symbol,
                lots.Where(lot => lot.Symbol == symbol),
                realisedPnl.GetValueOrDefault(symbol)))
            .ToArray();
    }

    public async Task<Position?> GetPositionAsync(string symbol, CancellationToken cancellationToken)
    {
        var lots = await dbContext.Lots
            .AsNoTracking()
            .Where(lot => lot.Symbol == symbol && lot.RemainingQuantity > 0)
            .OrderBy(lot => lot.OpenedAt)
            .ThenBy(lot => lot.Id)
            .ToListAsync(cancellationToken);
        var realisedEntries = await dbContext.RealisedPnlEntries
            .AsNoTracking()
            .Where(entry => entry.Symbol == symbol)
            .Select(entry => entry.Amount)
            .ToListAsync(cancellationToken);

        return lots.Count == 0 && realisedEntries.Count == 0
            ? null
            : MapPosition(symbol, lots, realisedEntries.Sum());
    }

    private static Position MapPosition(
        string symbol,
        IEnumerable<LotEntity> lots,
        decimal realisedPnl) => new(
        symbol,
        lots.Select(lot => lot.ToModel()).ToArray(),
        realisedPnl);
}
