namespace TradeLedger.Application.Records;

public sealed record PositionResult(
    string Symbol,
    decimal OpenQuantity,
    decimal? AverageUnitCost,
    decimal RealisedPnl);
