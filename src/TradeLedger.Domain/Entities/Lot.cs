namespace TradeLedger.Domain;

public sealed record Lot(
    Guid Id,
    string Symbol,
    decimal RemainingQuantity,
    decimal UnitCost,
    DateTimeOffset OpenedAt);
