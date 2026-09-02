namespace TradeLedger.Domain;

public sealed record MatchResult(
    IReadOnlyList<Lot> RemainingLots,
    decimal RealisedPnl);
