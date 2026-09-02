using TradeLedger.Domain;

namespace TradeLedger.Application.Records;

/// <summary>A validated request waiting for asynchronous application by the Lambda processor.</summary>
public sealed record PendingFillRequest(
    Guid Id,
    string Symbol,
    Side Side,
    decimal Quantity,
    decimal Price,
    DateTimeOffset ExecutedAt,
    DateTimeOffset? ProcessedAt = null)
{
    public static PendingFillRequest Create(
        Guid id,
        string symbol,
        Side side,
        decimal quantity,
        decimal price,
        DateTimeOffset executedAt)
    {
        var validated = Fill.Create(id, symbol, side, quantity, price, executedAt);
        return new PendingFillRequest(
            validated.Id,
            validated.Symbol,
            validated.Side,
            validated.Quantity,
            validated.Price,
            validated.ExecutedAt);
    }

    /// <summary>Creates the domain fill only when the asynchronous request is being applied.</summary>
    public Fill ToFill() => new(Id, Symbol, Side, Quantity, Price, ExecutedAt);
}
