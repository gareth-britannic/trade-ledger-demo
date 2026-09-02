namespace TradeLedger.Common;

public interface ICorrelationIdProvider
{
    string? CorrelationId { get; set; }
}
