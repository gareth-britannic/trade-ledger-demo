namespace TradeLedger.Application.Interfaces;

public interface ICorrelationIdProvider
{
    string? CorrelationId { get; set; }
}
