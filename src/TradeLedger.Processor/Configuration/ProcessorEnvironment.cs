namespace TradeLedger.Processor.Configuration;

public static class ProcessorEnvironment
{
    private static readonly string[] RequiredVariables =
    [
        "Database__Host",
        "Database__Port",
        "Database__Name",
        "Database__Username",
        "Database__Password"
    ];

    public static void ValidateRequired(Func<string, string?>? getEnvironmentVariable = null)
    {
        getEnvironmentVariable ??= Environment.GetEnvironmentVariable;
        var missing = RequiredVariables
            .Where(name => string.IsNullOrWhiteSpace(getEnvironmentVariable(name)))
            .ToList();

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Missing required Lambda environment variable(s): {string.Join(", ", missing)}.");
        }
    }
}
