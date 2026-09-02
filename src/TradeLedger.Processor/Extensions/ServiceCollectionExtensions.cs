using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradeLedger.Application.Messaging;
using TradeLedger.Common;
using TradeLedger.Database;
using TradeLedger.Processor.Handlers;
using TradeLedger.Processor.Messages;
using TradeLedger.Processor.Validation;

namespace TradeLedger.Processor.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProcessor(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCommon();
        services.AddDatabase(configuration);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<FillMessageValidator>();
        services.AddScoped<ISqsMessageHandler<FillRequestMessage>, FillMessageHandler>();
        services.AddSingleton<SqsMessageHandler<FillRequestMessage>>();
        services.AddSingleton<Function>();
        return services;
    }
}
