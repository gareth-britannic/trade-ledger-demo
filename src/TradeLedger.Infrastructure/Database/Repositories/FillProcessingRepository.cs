using Microsoft.EntityFrameworkCore;
using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Records;
using TradeLedger.Infrastructure.Database.Entities;

namespace TradeLedger.Infrastructure.Database.Repositories;

internal sealed class FillProcessingRepository(TradeLedgerDbContext dbContext) : IFillProcessingRepository
{
    public async Task<Fill?> GetPendingFillAsync(Guid fillId, CancellationToken cancellationToken)
    {
        var fill = await dbContext.Fills
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == fillId, cancellationToken);
        return fill?.ProcessedAt is null ? fill?.ToModel() : null;
    }

    public async Task<IReadOnlyList<Lot>> GetOpenLotsAsync(
        string symbol,
        CancellationToken cancellationToken) => await dbContext.Lots
        .AsNoTracking()
        .Where(lot => lot.Symbol == symbol && lot.RemainingQuantity > 0)
        .OrderBy(lot => lot.OpenedAt)
        .ThenBy(lot => lot.Id)
        .Select(lot => lot.ToModel())
        .ToArrayAsync(cancellationToken);

    public async Task CompleteAsync(
        Guid fillId,
        string symbol,
        IReadOnlyList<Lot> openLots,
        RealisedPnlEntry? realisedPnl,
        DateTimeOffset processedAt,
        CancellationToken cancellationToken)
    {
        var fill = await dbContext.Fills.SingleAsync(candidate => candidate.Id == fillId, cancellationToken);
        if (fill.ProcessedAt.HasValue)
        {
            return;
        }

        var existingLots = await dbContext.Lots
            .Where(lot => lot.Symbol == symbol)
            .ToArrayAsync(cancellationToken);
        dbContext.Lots.RemoveRange(existingLots);
        dbContext.Lots.AddRange(openLots.Select(LotEntity.FromModel));

        if (realisedPnl is not null)
        {
            dbContext.RealisedPnlEntries.Add(RealisedPnlEntryEntity.FromModel(realisedPnl));
        }

        fill.ProcessedAt = processedAt.ToUniversalTime();
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
