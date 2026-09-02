namespace TradeLedger.Domain;

public sealed record Position(
    string Symbol,
    IReadOnlyList<Lot> OpenLots,
    decimal RealisedPnl);
