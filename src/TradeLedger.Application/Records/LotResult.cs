namespace TradeLedger.Application.Records;

public sealed record LotResult(
    Guid Id,
    string Symbol,
    decimal RemainingQuantity,
    decimal UnitCost,
    DateTimeOffset OpenedAt);
