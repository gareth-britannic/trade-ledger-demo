using TradeLedger.Application.Records;

namespace TradeLedger.Application.Interfaces;

/// <summary>Applies one persisted pending request to its symbol ledger atomically.</summary>
public interface IFillRequestProcessor
{
    Task<FillProcessingResult> ProcessAsync(Guid requestId, CancellationToken cancellationToken);
}
