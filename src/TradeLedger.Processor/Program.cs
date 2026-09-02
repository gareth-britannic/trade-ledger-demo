using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SQSEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using TradeLedger.Common;
using TradeLedger.Processor;
using TradeLedger.Processor.Configuration;
using TradeLedger.Processor.Extensions;

LoggingExtensions.ConfigureBootstrapLogger();

try
{
    ProcessorEnvironment.ValidateRequired();
    using var host = Host.CreateDefaultBuilder(args)
        .UseTradeLedgerSerilog()
        .ConfigureServices((context, services) => services.AddProcessor(context.Configuration))
        .Build();
    await host.StartAsync();
    var function = host.Services.GetRequiredService<Function>();
    await LambdaBootstrapBuilder
        .Create<SQSEvent, SQSBatchResponse>(
            function.FunctionHandler,
            new DefaultLambdaJsonSerializer())
        .Build()
        .RunAsync();
    await host.StopAsync();
}
catch (Exception exception)
{
    Log.Fatal(exception, "Trade Ledger Processor terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
