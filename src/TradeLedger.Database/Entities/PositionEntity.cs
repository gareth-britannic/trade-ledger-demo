using TradeLedger.Domain;

namespace TradeLedger.Database.Entities;

internal sealed class PositionEntity
{
    public string Symbol { get; set; } = string.Empty;

    public decimal OpenQuantity { get; set; }

    public decimal RealisedPnl { get; set; }

    public DateTimeOffset LastAppliedExecutedAt { get; set; }

    public Guid LastAppliedFillId { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Position ToModel(IReadOnlyList<Lot> openLots) => new(Symbol, openLots, RealisedPnl);
}
