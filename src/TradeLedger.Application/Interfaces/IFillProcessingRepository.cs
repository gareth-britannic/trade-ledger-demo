using TradeLedger.Application.Records;

namespace TradeLedger.Application.Interfaces;

public interface IFillProcessingRepository
{
    Task<Fill?> GetPendingFillAsync(Guid fillId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Lot>> GetOpenLotsAsync(string symbol, CancellationToken cancellationToken);

    Task CompleteAsync(
        Guid fillId,
        string symbol,
        IReadOnlyList<Lot> openLots,
        RealisedPnlEntry? realisedPnl,
        DateTimeOffset processedAt,
        CancellationToken cancellationToken);
}
