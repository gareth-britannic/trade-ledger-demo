using Serilog;
using Serilog.Formatting.Compact;
using TradeLedger.Api.Extensions;
using TradeLedger.Application;
using TradeLedger.Infrastructure.Extensions;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new RenderedCompactJsonFormatter())
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(new RenderedCompactJsonFormatter()));

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
    builder.Services.AddInfrastructureHealthChecks();
    builder.Services.AddApiServices(builder.Configuration);

    var app = builder.Build();
    app.UseApiPipeline();
    app.Run();
}
catch (HostAbortedException)
{
    // EF Core design-time tooling intentionally aborts the temporary host after resolving services.
}
catch (Exception exception)
{
    Log.Fatal(exception, "Trade Ledger API terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program;
