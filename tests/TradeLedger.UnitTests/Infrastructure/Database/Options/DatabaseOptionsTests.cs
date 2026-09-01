using Npgsql;
using Shouldly;
using TradeLedger.Infrastructure.Database.Options;
using Xunit;

namespace TradeLedger.UnitTests.Infrastructure.Database.Options;

public sealed class DatabaseOptionsTests
{
    [Fact]
    public void BuildConnectionString_ExplicitConnectionString_ReturnsItUnchanged()
    {
        const string connectionString = "Host=database;Database=ledger;Username=user;Password=secret";
        var options = new DatabaseOptions { ConnectionString = connectionString };

        options.IsValid().ShouldBeTrue();
        options.BuildConnectionString().ShouldBe(connectionString);
    }

    [Fact]
    public void BuildConnectionString_IndividualProperties_BuildsExpectedConnectionString()
    {
        var options = Options();

        options.IsValid().ShouldBeTrue();
        var connectionString = new NpgsqlConnectionStringBuilder(options.BuildConnectionString());
        connectionString.Host.ShouldBe("database");
        connectionString.Port.ShouldBe(5433);
        connectionString.Database.ShouldBe("ledger");
        connectionString.Username.ShouldBe("user");
        connectionString.Password.ShouldBe("secret");
    }

    [Theory]
    [InlineData(null, 5432, "ledger", "user", "secret")]
    [InlineData("database", 0, "ledger", "user", "secret")]
    [InlineData("database", 65536, "ledger", "user", "secret")]
    [InlineData("database", 5432, "", "user", "secret")]
    [InlineData("database", 5432, "ledger", "", "secret")]
    [InlineData("database", 5432, "ledger", "user", "")]
    public void IsValid_IncompleteProperties_ReturnsFalse(
        string? host,
        int port,
        string name,
        string username,
        string password)
    {
        var options = new DatabaseOptions
        {
            Host = host,
            Port = port,
            Name = name,
            Username = username,
            Password = password
        };

        options.IsValid().ShouldBeFalse();
    }

    private static DatabaseOptions Options() => new()
    {
        Host = "database",
        Port = 5433,
        Name = "ledger",
        Username = "user",
        Password = "secret"
    };
}
