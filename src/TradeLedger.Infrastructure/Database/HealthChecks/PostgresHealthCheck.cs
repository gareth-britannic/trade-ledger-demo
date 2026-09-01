using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TradeLedger.Infrastructure.Database.HealthChecks;

internal sealed class PostgresHealthCheck(TradeLedgerDbContext dbContext) : IHealthCheck
{
    const string ErrorMessage = "PostgreSQL is unavailable.";
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy(ErrorMessage);
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(ErrorMessage, exception);
        }
    }
}
