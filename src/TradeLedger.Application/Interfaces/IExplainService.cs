using TradeLedger.Application.Records;

namespace TradeLedger.Application.Interfaces;

public interface IExplainService
{
    Task<ExplainResult> ExplainAsync(ExplainQuery query, CancellationToken cancellationToken);
}
