using Amazon.Runtime;
using Amazon.SQS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Options;
using TradeLedger.Infrastructure.Messaging.Options;

namespace TradeLedger.Infrastructure.Messaging;

internal static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddOptions<FillQueueOptions>()
            .Bind(configuration.GetSection(FillQueueOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.Url, UriKind.Absolute, out _),
                $"{FillQueueOptions.SectionName}:{nameof(FillQueueOptions.Url)} must be an absolute queue URL.")
            .ValidateOnStart();
        services.AddOptions<LocalAwsOptions>()
            .Bind(configuration.GetSection(LocalAwsOptions.SectionName))
            .Validate(options => environment.IsDevelopment() || string.IsNullOrWhiteSpace(options.ServiceUrl),
                $"{LocalAwsOptions.SectionName}:{nameof(LocalAwsOptions.ServiceUrl)} may only be configured in Development.")
            .Validate(HasCompleteLocalConfiguration,
                $"Development LocalStack requires {LocalAwsOptions.SectionName} region and credentials.")
            .ValidateOnStart();
        services.AddOptions<FillProcessingOptions>()
            .Bind(configuration.GetSection(FillProcessingOptions.SectionName));

        services.AddSingleton<IAmazonSQS>(_ => CreateSqsClient(configuration, environment));
        services.AddScoped<IFillPublisher, SqsFillPublisher>();
        services.AddHostedService<SqsFillProcessor>();
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
