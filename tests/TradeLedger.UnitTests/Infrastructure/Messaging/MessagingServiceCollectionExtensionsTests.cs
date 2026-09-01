using Amazon.SQS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using TradeLedger.Infrastructure.Messaging;
using TradeLedger.Infrastructure.Messaging.Options;
using Xunit;

namespace TradeLedger.UnitTests.Infrastructure.Messaging;

public sealed class MessagingServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMessaging_ValidLocalConfiguration_RegistersConfiguredSqsClient()
    {
        var services = new ServiceCollection();
        services.AddMessaging(Configuration(ValidLocalConfiguration()), Environment(Environments.Development));
        using var provider = services.BuildServiceProvider();

        using var client = provider.GetRequiredService<IAmazonSQS>();

        client.ShouldBeOfType<AmazonSQSClient>();
        client.Config.ServiceURL.ShouldBe("http://localhost:4566/");
    }

    [Fact]
    public void AddMessaging_LocalConfigurationInProduction_ThrowsWhenClientIsResolved()
    {
        var services = new ServiceCollection();
        services.AddMessaging(Configuration(ValidLocalConfiguration()), Environment(Environments.Production));
        using var provider = services.BuildServiceProvider();

        Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<IAmazonSQS>());
    }

    [Fact]
    public void AddMessaging_IncompleteLocalConfiguration_ThrowsWhenClientIsResolved()
    {
        var configuration = ValidLocalConfiguration();
        configuration.Remove("AWS:SecretKey");
        var services = new ServiceCollection();
        services.AddMessaging(Configuration(configuration), Environment(Environments.Development));
        using var provider = services.BuildServiceProvider();

        Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<IAmazonSQS>());
    }

    [Fact]
    public void AddMessaging_InvalidQueueUrl_FailsOptionsValidation()
    {
        var configuration = ValidLocalConfiguration();
        configuration["FillQueue:Url"] = "not-a-url";
        var services = new ServiceCollection();
        services.AddMessaging(Configuration(configuration), Environment(Environments.Development));
        using var provider = services.BuildServiceProvider();

        Should.Throw<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<FillQueueOptions>>().Value);
    }

    [Fact]
    public void AddMessaging_MissingLocalServiceUrl_AllowsDefaultAwsConfiguration()
    {
        var configuration = ValidLocalConfiguration();
        configuration.Remove("AWS:ServiceUrl");
        var services = new ServiceCollection();
        services.AddMessaging(Configuration(configuration), Environment(Environments.Production));
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<LocalAwsOptions>>().Value.ServiceUrl.ShouldBeNull();
    }

    private static Dictionary<string, string?> ValidLocalConfiguration() => new()
    {
        ["FillQueue:Url"] = "http://localhost:4566/000000000000/fills.fifo",
        ["AWS:ServiceUrl"] = "http://localhost:4566",
        ["AWS:Region"] = "us-east-1",
        ["AWS:AccessKey"] = "test",
        ["AWS:SecretKey"] = "test"
    };

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static IHostEnvironment Environment(string environmentName) => new TestEnvironment
    {
        EnvironmentName = environmentName
    };

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
