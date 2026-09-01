using TradeLedger.Application.Records;

namespace TradeLedger.Api.Contracts.Explain;

public sealed record ExplainResponse(
    IReadOnlyList<string> ToolCalls,
    string Answer)
{
    public static ExplainResponse FromApplication(ExplainResult result) => new(
        result.ToolCalls,
        result.Answer);
}
