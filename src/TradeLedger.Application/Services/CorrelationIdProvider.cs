using TradeLedger.Application.Interfaces;

namespace TradeLedger.Application.Services;

public sealed class CorrelationIdProvider : ICorrelationIdProvider
{
    public string? CorrelationId { get; set; }
}

public static class CorrelationIdMetadata
{
    public const string PropertyName = "CorrelationId";
}
