namespace TradeLedger.Domain;

public sealed record Fill
{
    private Fill(
        Guid id,
        string symbol,
        Side side,
        decimal quantity,
        decimal price,
        DateTimeOffset executedAt)
    {
        Id = id;
        Symbol = symbol;
        Side = side;
        Quantity = quantity;
        Price = price;
        ExecutedAt = executedAt;
    }

    public Guid Id { get; }

    public string Symbol { get; }

    public Side Side { get; }

    public decimal Quantity { get; }

    public decimal Price { get; }

    public DateTimeOffset ExecutedAt { get; }

    public static Fill Create(
        Guid id,
        string symbol,
        Side side,
        decimal quantity,
        decimal price,
        DateTimeOffset executedAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A fill ID must not be empty.", nameof(id));
        }

        if (!SymbolNormalizer.IsValid(symbol))
        {
            throw new ArgumentException("The fill symbol is invalid.", nameof(symbol));
        }

        if (!Enum.IsDefined(side))
        {
            throw new ArgumentOutOfRangeException(nameof(side), side, "The fill side is invalid.");
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "A fill quantity must be positive.");
        }

        if (price <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), price, "A fill price must be positive.");
        }

        if (executedAt == default)
        {
            throw new ArgumentException("A fill execution timestamp is required.", nameof(executedAt));
        }

        return new Fill(
            id,
            SymbolNormalizer.Normalize(symbol),
            side,
            quantity,
            price,
            executedAt.ToUniversalTime());
    }
}
