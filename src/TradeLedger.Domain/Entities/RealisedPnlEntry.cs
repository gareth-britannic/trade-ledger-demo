namespace TradeLedger.Domain;

public sealed record RealisedPnlEntry(
    Guid FillId,
    string Symbol,
    decimal Amount,
    DateTimeOffset RealisedAt);
