namespace TradeLedger.Application.Messaging;

/// <summary>The stable fill notification contract shared by producers and queue adapters.</summary>
public sealed record FillRequestMessage(
    Guid FillId,
    string Symbol,
    string Side,
    decimal Quantity,
    decimal Price,
    DateTimeOffset ExecutedAt);
