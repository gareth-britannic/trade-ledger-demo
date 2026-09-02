using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Formatting.Compact;

namespace TradeLedger.Common;

public static class LoggingExtensions
{
    public static void ConfigureBootstrapLogger()
    {
        Log.Logger = Configure(new LoggerConfiguration()).CreateBootstrapLogger();
    }

    public static IHostBuilder UseTradeLedgerSerilog(this IHostBuilder builder)
    {
        return builder.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services);

            Configure(configuration);
        });
    }

    private static LoggerConfiguration Configure(LoggerConfiguration configuration)
    {
        return configuration
            .Enrich.FromLogContext()
            .WriteTo.Console(new RenderedCompactJsonFormatter());
    }
}
