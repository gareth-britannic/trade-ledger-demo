namespace TradeLedger.Application.Records;

public sealed record ExplainResult(
    IReadOnlyList<string> ToolCalls,
    string Answer);
