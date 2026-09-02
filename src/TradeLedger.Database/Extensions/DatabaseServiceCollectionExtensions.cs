using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TradeLedger.Application.Interfaces;
using TradeLedger.Database.HealthChecks;
using TradeLedger.Database.Options;
using TradeLedger.Database.Repositories;

namespace TradeLedger.Database;

public static class DatabaseServiceCollectionExtensions
{
    private const string PostgresHealthCheckName = "postgresql";

    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .Validate(options => options.IsValid(),
                $"Set {DatabaseOptions.SectionName}:{nameof(DatabaseOptions.ConnectionString)} or all required " +
                $"{DatabaseOptions.SectionName} connection properties.")
            .ValidateOnStart();

        services.AddDbContext<TradeLedgerDbContext>((serviceProvider, options) =>
            options.UseNpgsql(serviceProvider
                .GetRequiredService<IOptions<DatabaseOptions>>()
                .Value
                .BuildConnectionString()));
        services.AddScoped<IFillRequestRepository, FillRequestRepository>();
        services.AddScoped<IFillLedgerUnitOfWork, FillLedgerUnitOfWork>();
        services.AddScoped<ILotRepository, LotRepository>();
        services.AddScoped<IRealisedPnlRepository, RealisedPnlRepository>();
        return services;
    }

    public static IServiceCollection AddDatabaseHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks().AddCheck<PostgresHealthCheck>(PostgresHealthCheckName);
        return services;
    }
}
