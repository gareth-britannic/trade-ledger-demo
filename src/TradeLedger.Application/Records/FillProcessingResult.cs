namespace TradeLedger.Application.Records;

public enum FillProcessingOutcome
{
    Applied,
    AlreadyProcessed
}

public sealed record FillProcessingResult(
    FillProcessingOutcome Outcome,
    string Symbol,
    int AppliedFillCount,
    bool RebuiltFromHistory,
    DateTimeOffset? OriginalProcessedAt = null);
