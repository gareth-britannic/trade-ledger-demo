namespace TradeLedger.Application.Records;

public sealed record MatchResult(
    IReadOnlyList<Lot> RemainingLots,
    decimal RealisedPnl);
