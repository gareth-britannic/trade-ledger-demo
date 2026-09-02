using TradeLedger.Application.Records;

namespace TradeLedger.Application.Interfaces;

public interface ILlmClient
{
    Task<ExplainResult> ExplainAsync(string question, CancellationToken cancellationToken);
}
