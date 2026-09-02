using Amazon.Runtime;
using Amazon.SQS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TradeLedger.Common;

public static class DependencyInjection
{
    public static IServiceCollection AddCommon(this IServiceCollection services)
    {
        services.AddScoped<ICorrelationIdProvider, CorrelationIdProvider>();
        return services;
    }

    public static IServiceCollection AddSqsClient(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddOptions<SqsQueueOptions>()
            .Bind(configuration.GetSection(SqsQueueOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.Url, UriKind.Absolute, out _),
                $"{SqsQueueOptions.SectionName}:{nameof(SqsQueueOptions.Url)} must be an absolute queue URL.")
            .ValidateOnStart();

        services.AddOptions<LocalAwsOptions>()
            .Bind(configuration.GetSection(LocalAwsOptions.SectionName))
            .Validate(options => environment.IsDevelopment() || string.IsNullOrWhiteSpace(options.ServiceUrl),
                $"{LocalAwsOptions.SectionName}:{nameof(LocalAwsOptions.ServiceUrl)} may only be configured in Development.")
            .Validate(HasCompleteLocalConfiguration,
                $"Development LocalStack requires {LocalAwsOptions.SectionName} region and credentials.")
            .ValidateOnStart();

        services.AddSingleton<IAmazonSQS>(_ => CreateSqsClient(configuration, environment));
        services.AddScoped<ISqsClient, SqsClient>();
        
        return services;
    }

    private static bool HasCompleteLocalConfiguration(LocalAwsOptions options) =>
        string.IsNullOrWhiteSpace(options.ServiceUrl) ||
        (!string.IsNullOrWhiteSpace(options.Region) &&
         !string.IsNullOrWhiteSpace(options.AccessKey) &&
         !string.IsNullOrWhiteSpace(options.SecretKey));

    private static IAmazonSQS CreateSqsClient(IConfiguration configuration, IHostEnvironment environment)
    {
        var localOptions = configuration.GetSection(LocalAwsOptions.SectionName).Get<LocalAwsOptions>();
        if (string.IsNullOrWhiteSpace(localOptions?.ServiceUrl))
        {
            return new AmazonSQSClient();
        }

        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"{LocalAwsOptions.SectionName}:{nameof(LocalAwsOptions.ServiceUrl)} may only be configured in Development.");
        }

        if (!HasCompleteLocalConfiguration(localOptions))
        {
            throw new InvalidOperationException(
                $"Development LocalStack requires {LocalAwsOptions.SectionName} region and credentials.");
        }

        var clientConfiguration = new AmazonSQSConfig
        {
            ServiceURL = localOptions.ServiceUrl,
            AuthenticationRegion = localOptions.Region
        };
        return new AmazonSQSClient(
            new BasicAWSCredentials(localOptions.AccessKey, localOptions.SecretKey),
            clientConfiguration);
    }
}
