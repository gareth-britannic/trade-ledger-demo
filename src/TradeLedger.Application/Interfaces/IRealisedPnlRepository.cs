namespace TradeLedger.Application.Interfaces;

public interface IRealisedPnlRepository
{
    Task<decimal> GetTotalAsync(
        string symbol,
        DateTimeOffset? fromInclusive,
        DateTimeOffset? toExclusive,
        CancellationToken cancellationToken);
}
