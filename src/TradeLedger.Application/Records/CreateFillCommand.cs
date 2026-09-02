using TradeLedger.Domain;

namespace TradeLedger.Application.Records;

public sealed record CreateFillCommand(
    Guid? FillId,
    string Symbol,
    Side Side,
    decimal Quantity,
    decimal Price,
    DateTimeOffset ExecutedAt);
