using Xunit;

namespace TradeLedger.IntegrationTests.Api.Support;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ApiCollection : ICollectionFixture<TradeLedgerApiFactory>
{
    public const string Name = "API integration tests";
}
