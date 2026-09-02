namespace TradeLedger.Common;

public sealed class SqsQueueOptions
{
    public const string SectionName = "FillQueue";

    public string Url { get; init; } = string.Empty;
}
