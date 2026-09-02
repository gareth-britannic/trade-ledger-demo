namespace TradeLedger.Domain;

public sealed record LedgerReplayResult(
    IReadOnlyList<Lot> OpenLots,
    IReadOnlyList<RealisedPnlEntry> RealisedPnlEntries,
    decimal RealisedPnl);
