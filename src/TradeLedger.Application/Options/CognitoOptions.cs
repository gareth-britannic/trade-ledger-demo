namespace TradeLedger.Application.Options;

public sealed class CognitoOptions
{
    public const string SectionName = "Authentication:Cognito";

    public string Authority { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;
}
