namespace TradeLedger.Common;

public sealed class CorrelationIdProvider : ICorrelationIdProvider
{
    public string? CorrelationId { get; set; }
}
