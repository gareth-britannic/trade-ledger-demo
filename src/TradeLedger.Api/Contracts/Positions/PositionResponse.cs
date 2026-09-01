using TradeLedger.Application.Records;

namespace TradeLedger.Api.Contracts.Positions;

public sealed record PositionResponse(
    string Symbol,
    decimal OpenQuantity,
    decimal? AverageUnitCost,
    decimal RealisedPnl)
{
    public static PositionResponse FromApplication(PositionResult result) => new(
        result.Symbol,
        result.OpenQuantity,
        result.AverageUnitCost,
        result.RealisedPnl);
}
