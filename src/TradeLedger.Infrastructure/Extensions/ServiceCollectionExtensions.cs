using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TradeLedger.Infrastructure.Database;
using TradeLedger.Infrastructure.Database.HealthChecks;
using TradeLedger.Infrastructure.Messaging;

namespace TradeLedger.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    private const string PostgresHealthCheckName = "postgresql";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddDatabase(configuration);
        services.AddMessaging(configuration, environment);
        return services;
    }

    public static IServiceCollection AddInfrastructureHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks().AddCheck<PostgresHealthCheck>(PostgresHealthCheckName);
        return services;
    }
}
