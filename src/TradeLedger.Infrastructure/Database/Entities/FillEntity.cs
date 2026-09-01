using TradeLedger.Application;
using TradeLedger.Application.Records;

namespace TradeLedger.Infrastructure.Database.Entities;

internal sealed class FillEntity
{
    public Guid Id { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public Side Side { get; set; }

    public decimal Quantity { get; set; }

    public decimal Price { get; set; }

    public DateTimeOffset ExecutedAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public static FillEntity FromModel(Fill fill) => new()
    {
        Id = fill.Id,
        Symbol = fill.Symbol,
        Side = fill.Side,
        Quantity = fill.Quantity,
        Price = fill.Price,
        ExecutedAt = fill.ExecutedAt
    };

    public Fill ToModel() => new(Id, Symbol, Side, Quantity, Price, ExecutedAt);
}
