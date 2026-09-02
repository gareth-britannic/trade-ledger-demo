using Serilog;
using TradeLedger.Api.Extensions;
using TradeLedger.Application;
using TradeLedger.Database;
using TradeLedger.Common;

LoggingExtensions.ConfigureBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseTradeLedgerSerilog();

    builder.Services.AddCommon();
    builder.Services.AddSqsClient(builder.Configuration, builder.Environment);
    builder.Services.AddApplication();
    builder.Services.AddDatabase(builder.Configuration);
    builder.Services.AddDatabaseHealthChecks();
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
