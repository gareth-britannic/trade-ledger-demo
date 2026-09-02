using TradeLedger.Domain;

namespace TradeLedger.Database.Entities;

internal sealed class PositionEntity
{
    public string Symbol { get; set; } = string.Empty;

    public decimal RealisedPnl { get; set; }

    public Position ToModel(IReadOnlyList<Lot> openLots) => new(Symbol, openLots, RealisedPnl);
}
