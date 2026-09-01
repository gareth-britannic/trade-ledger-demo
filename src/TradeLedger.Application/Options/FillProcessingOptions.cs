namespace TradeLedger.Application.Options;

public sealed class FillProcessingOptions
{
    public const string SectionName = "FillProcessing";

    public bool Enabled { get; init; }
}
