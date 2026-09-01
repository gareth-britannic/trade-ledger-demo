using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TradeLedger.Application.Interfaces;
using TradeLedger.Infrastructure.Database.Options;
using TradeLedger.Infrastructure.Database.Repositories;

namespace TradeLedger.Infrastructure.Database;

internal static class DatabaseServiceCollectionExtensions
{
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
        services.AddScoped<IFillRepository, FillRepository>();
        services.AddScoped<IFillProcessingRepository, FillProcessingRepository>();
        services.AddScoped<ILotRepository, LotRepository>();
        services.AddScoped<IRealisedPnlRepository, RealisedPnlRepository>();
        return services;
    }
}
