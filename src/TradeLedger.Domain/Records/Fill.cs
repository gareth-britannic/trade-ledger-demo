namespace TradeLedger.Domain.Records;

public record Fill(
    Guid Id,
    string Symbol,
    Side Side,
    decimal Quantity,
    decimal Price,
    DateTimeOffset ExecutedAt);