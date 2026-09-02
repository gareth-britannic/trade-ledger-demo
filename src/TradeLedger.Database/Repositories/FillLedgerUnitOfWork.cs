using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Records;
using TradeLedger.Database.Entities;
using TradeLedger.Domain;

namespace TradeLedger.Database.Repositories;

internal sealed class FillLedgerUnitOfWork(TradeLedgerDbContext dbContext) : IFillLedgerUnitOfWork
{
    private IDbContextTransaction? _transaction;

    public async Task BeginAsync(CancellationToken cancellationToken)
    {
        if (_transaction is not null)
        {
            throw new InvalidOperationException("A fill-processing transaction is already active.");
        }

        if (dbContext.Database.IsRelational())
        {
            _transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        }
    }

    public async Task<PendingFillRequest?> FindRequestAsync(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var request = await dbContext.Fills
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == requestId, cancellationToken);
        return request?.ToRequest();
    }

    public async Task AcquireSymbolLockAsync(string symbol, CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsNpgsql())
        {
            return;
        }

        // A transaction-scoped advisory lock is safe even before a position row exists.
        // Hash collisions only serialize unrelated symbols; they cannot weaken correctness.
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({symbol}, 0))",
            cancellationToken);
    }

    public async Task<IReadOnlyList<PendingFillRequest>> GetOrderedRequestsAsync(
        string symbol,
        CancellationToken cancellationToken)
    {
        var requests = await dbContext.Fills
            .AsNoTracking()
            .Where(fill => fill.Symbol == symbol)
            .OrderBy(fill => fill.ExecutedAt)
            .ThenBy(fill => fill.Id)
            .ToListAsync(cancellationToken);
        return requests.Select(request => request.ToRequest()).ToList();
    }

    public async Task ReplaceSymbolLedgerAsync(
        Position position,
        IReadOnlyList<RealisedPnlEntry> realisedPnlEntries,
        IReadOnlyCollection<Guid> newlyProcessedFillIds,
        DateTimeOffset processedAt,
        PendingFillRequest orderingWatermark,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(realisedPnlEntries);
        ArgumentNullException.ThrowIfNull(newlyProcessedFillIds);
        ArgumentNullException.ThrowIfNull(orderingWatermark);

        var existingLots = await dbContext.Lots
            .Where(lot => lot.Symbol == position.Symbol)
            .ToListAsync(cancellationToken);
        var existingRealisedPnl = await dbContext.RealisedPnlEntries
            .Where(entry => entry.Symbol == position.Symbol)
            .ToListAsync(cancellationToken);
        dbContext.Lots.RemoveRange(existingLots);
        dbContext.RealisedPnlEntries.RemoveRange(existingRealisedPnl);
        dbContext.Lots.AddRange(position.OpenLots.Select(LotEntity.FromModel));
        dbContext.RealisedPnlEntries.AddRange(realisedPnlEntries.Select(RealisedPnlEntryEntity.FromModel));

        var positionEntity = await dbContext.Positions
            .SingleOrDefaultAsync(candidate => candidate.Symbol == position.Symbol, cancellationToken);
        if (positionEntity is null)
        {
            positionEntity = new PositionEntity { Symbol = position.Symbol };
            dbContext.Positions.Add(positionEntity);
        }

        positionEntity.OpenQuantity = position.OpenLots.Sum(lot => lot.RemainingQuantity);
        positionEntity.RealisedPnl = position.RealisedPnl;
        positionEntity.LastAppliedExecutedAt = orderingWatermark.ExecutedAt.ToUniversalTime();
        positionEntity.LastAppliedFillId = orderingWatermark.Id;
        positionEntity.UpdatedAt = processedAt.ToUniversalTime();

        if (newlyProcessedFillIds.Count > 0)
        {
            var fills = await dbContext.Fills
                .Where(fill => newlyProcessedFillIds.Contains(fill.Id))
                .ToListAsync(cancellationToken);
            foreach (var fill in fills.Where(fill => fill.ProcessedAt is null))
            {
                // This timestamp commits with lots, position, and P&L, so rollback restores pending state.
                fill.ProcessedAt = processedAt.ToUniversalTime();
            }
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.CommitAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (_transaction is null)
        {
            dbContext.ChangeTracker.Clear();
            return;
        }

        try
        {
            await _transaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
            dbContext.ChangeTracker.Clear();
        }
    }
}
