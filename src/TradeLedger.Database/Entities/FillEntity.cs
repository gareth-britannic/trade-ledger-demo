using TradeLedger.Application.Records;
using TradeLedger.Domain;

namespace TradeLedger.Database.Entities;

internal sealed class FillEntity
{
    public Guid Id { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public Side Side { get; set; }

    public decimal Quantity { get; set; }

    public decimal Price { get; set; }

    public DateTimeOffset ExecutedAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public static FillEntity FromRequest(PendingFillRequest request) => new()
    {
        Id = request.Id,
        Symbol = request.Symbol,
        Side = request.Side,
        Quantity = request.Quantity,
        Price = request.Price,
        ExecutedAt = request.ExecutedAt
    };

    public PendingFillRequest ToRequest() =>
        new(Id, Symbol, Side, Quantity, Price, ExecutedAt, ProcessedAt);
}
