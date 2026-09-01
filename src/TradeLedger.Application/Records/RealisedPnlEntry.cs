namespace TradeLedger.Application.Records;

public sealed record RealisedPnlEntry(
    Guid FillId,
    string Symbol,
    decimal Amount,
    DateTimeOffset RealisedAt);
