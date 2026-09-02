using TradeLedger.Application;

using TradeLedger.Domain;

namespace TradeLedger.Api.Contracts.Fills;

public sealed record CreateFillRequest(
    Guid? FillId,
    string? Symbol,
    Side Side,
    decimal Quantity,
    decimal Price,
    DateTimeOffset ExecutedAt);
