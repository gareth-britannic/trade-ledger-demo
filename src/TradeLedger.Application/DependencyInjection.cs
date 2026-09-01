using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Services;
using TradeLedger.Application.Validators;

namespace TradeLedger.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ICorrelationIdProvider, CorrelationIdProvider>();
        services.AddValidatorsFromAssemblyContaining<CreateFillCommandValidator>();
        services.AddScoped<ICreateFillService, CreateFillService>();
        services.AddScoped<IProcessFillService, ProcessFillService>();
        services.AddScoped<IPositionQueryService, PositionQueryService>();
        services.AddScoped<IExplainService, ExplainService>();
        return services;
    }
}
