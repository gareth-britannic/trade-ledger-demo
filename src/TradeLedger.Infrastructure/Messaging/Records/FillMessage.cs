namespace TradeLedger.Infrastructure.Messaging.Records;

internal sealed record FillMessage(
    Guid FillId,
    string Symbol,
    string Side,
    decimal Quantity,
    decimal Price,
    DateTimeOffset ExecutedAt);
