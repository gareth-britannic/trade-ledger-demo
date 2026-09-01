using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Records;

namespace TradeLedger.Application.Services;

public sealed class ProcessFillService(
    IFillProcessingRepository repository,
    TimeProvider timeProvider) : IProcessFillService
{
    public async Task<bool> ProcessAsync(Guid fillId, CancellationToken cancellationToken)
    {
        if (fillId == Guid.Empty)
        {
            throw new ArgumentException("A fill ID must not be empty.", nameof(fillId));
        }

        var fill = await repository.GetPendingFillAsync(fillId, cancellationToken);
        if (fill is null)
        {
            return false;
        }

        var currentLots = await repository.GetOpenLotsAsync(fill.Symbol, cancellationToken);
        IReadOnlyList<Lot> openLots;
        RealisedPnlEntry? realisedPnl = null;

        if (fill.Side == Side.Buy)
        {
            openLots = FifoMatcher.ApplyBuy(currentLots, fill);
        }
        else
        {
            var result = FifoMatcher.ApplySell(currentLots, fill);
            openLots = result.RemainingLots;
            realisedPnl = new RealisedPnlEntry(fill.Id, fill.Symbol, result.RealisedPnl, fill.ExecutedAt);
        }

        await repository.CompleteAsync(
            fill.Id,
            fill.Symbol,
            openLots,
            realisedPnl,
            timeProvider.GetUtcNow(),
            cancellationToken);
        return true;
    }
}
