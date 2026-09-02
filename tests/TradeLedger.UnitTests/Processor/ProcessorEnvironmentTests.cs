using Shouldly;
using TradeLedger.Processor.Configuration;
using Xunit;

namespace TradeLedger.UnitTests.Processor;

public sealed class ProcessorEnvironmentTests
{
    private static readonly IReadOnlyDictionary<string, string> CompleteEnvironment =
        new Dictionary<string, string>
        {
            ["Database__Host"] = "postgres",
            ["Database__Port"] = "5432",
            ["Database__Name"] = "trade_ledger",
            ["Database__Username"] = "trade_ledger",
            ["Database__Password"] = "secret"
        };

    [Fact]
    public void CompleteEnvironment_DoesNotThrow() =>
        Should.NotThrow(() => ProcessorEnvironment.ValidateRequired(GetCompleteValue));

    [Fact]
    public void MissingEnvironment_ThrowsWithEveryMissingVariable()
    {
        var exception = Should.Throw<InvalidOperationException>(() =>
            ProcessorEnvironment.ValidateRequired(name =>
                name is "Database__Host" or "Database__Password" ? null : CompleteEnvironment[name]));

        exception.Message.ShouldContain("Missing required Lambda environment variable(s)");
        exception.Message.ShouldContain("Database__Host");
        exception.Message.ShouldContain("Database__Password");
    }

    private static string? GetCompleteValue(string name) => CompleteEnvironment.GetValueOrDefault(name);
}
