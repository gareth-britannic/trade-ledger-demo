using TradeLedger.Application.Records;

namespace TradeLedger.Api.Contracts.Positions;

public sealed record LotResponse(
    Guid Id,
    string Symbol,
    decimal RemainingQuantity,
    decimal UnitCost,
    DateTimeOffset OpenedAt)
{
    public static LotResponse FromApplication(LotResult result) => new(
        result.Id,
        result.Symbol,
        result.RemainingQuantity,
        result.UnitCost,
        result.OpenedAt);
}
