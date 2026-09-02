using TradeLedger.Domain;

namespace TradeLedger.Database.Entities;

internal sealed class LotEntity
{
    public Guid Id { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public decimal RemainingQuantity { get; set; }

    public decimal UnitCost { get; set; }

    public DateTimeOffset OpenedAt { get; set; }

    public Lot ToModel() => new(Id, Symbol, RemainingQuantity, UnitCost, OpenedAt);

    public static LotEntity FromModel(Lot lot) => new()
    {
        Id = lot.Id,
        Symbol = lot.Symbol,
        RemainingQuantity = lot.RemainingQuantity,
        UnitCost = lot.UnitCost,
        OpenedAt = lot.OpenedAt
    };
}
