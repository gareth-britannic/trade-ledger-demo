using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using TradeLedger.Database;
using TradeLedger.Database.Options;
using Xunit;

namespace TradeLedger.UnitTests.Database;

public sealed class DatabaseServiceCollectionExtensionsTests
{
    [Fact]
    public void AddDatabase_ValidConfiguration_RegistersNpgsqlContext()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["Database:ConnectionString"] =
                "Host=database;Database=ledger;Username=user;Password=secret"
        });
        var services = new ServiceCollection();

        services.AddDatabase(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TradeLedgerDbContext>();
        context.Database.ProviderName.ShouldBe("Npgsql.EntityFrameworkCore.PostgreSQL");
    }

    [Fact]
    public void AddDatabase_InvalidConfiguration_FailsOptionsValidation()
    {
        var services = new ServiceCollection();
        services.AddDatabase(Configuration([]));
        using var provider = services.BuildServiceProvider();

        Should.Throw<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<DatabaseOptions>>().Value);
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
