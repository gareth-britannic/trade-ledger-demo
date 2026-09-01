namespace TradeLedger.Domain.Records;

public sealed record MatchResult(
    IReadOnlyList<Lot> RemainingLots,
    decimal RealisedPnl);
