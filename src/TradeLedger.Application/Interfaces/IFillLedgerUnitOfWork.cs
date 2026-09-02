using TradeLedger.Application.Records;
using TradeLedger.Domain;

namespace TradeLedger.Application.Interfaces;

/// <summary>
/// Defines the persistence boundary for one serialized symbol-ledger rebuild.
/// Implementations own database transaction and locking mechanics.
/// </summary>
public interface IFillLedgerUnitOfWork
{
    Task BeginAsync(CancellationToken cancellationToken);

    Task<PendingFillRequest?> FindRequestAsync(Guid requestId, CancellationToken cancellationToken);

    Task AcquireSymbolLockAsync(string symbol, CancellationToken cancellationToken);

    Task<IReadOnlyList<PendingFillRequest>> GetOrderedRequestsAsync(
        string symbol,
        CancellationToken cancellationToken);

    Task ReplaceSymbolLedgerAsync(
        Position position,
        IReadOnlyList<RealisedPnlEntry> realisedPnlEntries,
        IReadOnlyCollection<Guid> newlyProcessedFillIds,
        DateTimeOffset processedAt,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task CommitAsync(CancellationToken cancellationToken);

    Task RollbackAsync(CancellationToken cancellationToken);
}
