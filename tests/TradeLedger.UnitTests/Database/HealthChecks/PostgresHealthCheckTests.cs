using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shouldly;
using TradeLedger.Database;
using TradeLedger.Database.HealthChecks;
using Xunit;

namespace TradeLedger.UnitTests.Database.HealthChecks;

public sealed class PostgresHealthCheckTests
{
    [Fact]
    public async Task CheckHealth_DatabaseIsUnavailable_ReturnsUnhealthy()
    {
        await using var context = new TradeLedgerDbContext(
            new DbContextOptionsBuilder<TradeLedgerDbContext>()
                .UseNpgsql("Host=127.0.0.1;Port=1;Database=missing;Username=test;Password=test;Timeout=1")
                .Options);
        var healthCheck = new PostgresHealthCheck(context);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldBe("PostgreSQL is unavailable.");
        result.Exception.ShouldBeNull();
    }

    [Fact]
    public async Task CheckHealth_ContextThrows_ReturnsUnhealthyWithException()
    {
        var context = new TradeLedgerDbContext(
            new DbContextOptionsBuilder<TradeLedgerDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options);
        await context.DisposeAsync();
        var healthCheck = new PostgresHealthCheck(context);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Exception.ShouldBeOfType<ObjectDisposedException>();
    }
}
