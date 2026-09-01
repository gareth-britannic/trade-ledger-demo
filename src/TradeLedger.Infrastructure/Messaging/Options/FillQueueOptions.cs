namespace TradeLedger.Infrastructure.Messaging.Options;

public sealed class FillQueueOptions
{
    public const string SectionName = "FillQueue";

    public string Url { get; init; } = string.Empty;
}
