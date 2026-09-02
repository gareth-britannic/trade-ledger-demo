namespace TradeLedger.Common;

public sealed class LocalAwsOptions
{
    public const string SectionName = "AWS";

    public string? ServiceUrl { get; init; }
    public string? Region { get; init; }
    public string? AccessKey { get; init; }
    public string? SecretKey { get; init; }
}
