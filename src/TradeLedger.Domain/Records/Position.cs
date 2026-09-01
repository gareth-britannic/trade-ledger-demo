namespace TradeLedger.Domain.Records;

public sealed record Position(
    string Symbol,
    IReadOnlyList<Lot> OpenLots,
    decimal RealisedPnl);
