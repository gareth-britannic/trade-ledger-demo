namespace TradeLedger.Application.Records;

public record Lot(
    Guid Id,
    string Symbol,
    decimal RemainingQuantity,
    decimal UnitCost,
    DateTimeOffset OpenedAt);
