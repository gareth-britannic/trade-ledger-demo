using Xunit;

namespace TradeLedger.IntegrationTests.Support;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LocalStackCollection
{
    public const string Name = "LocalStack integration tests";
}
