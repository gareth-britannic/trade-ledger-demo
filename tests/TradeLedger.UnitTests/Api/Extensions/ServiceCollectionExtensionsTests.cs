using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using TradeLedger.Api.Extensions;
using TradeLedger.Application.Options;
using Xunit;

namespace TradeLedger.UnitTests.Api.Extensions;

public sealed class ServiceCollectionExtensionsTests
{
    [Theory]
    [InlineData("Development", false)]
    [InlineData("Production", true)]
    public void AddApiServices_ValidCognitoConfiguration_ConfiguresJwtBearer(
        string environmentName,
        bool requireHttpsMetadata)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(Environment(environmentName));
        services.AddApiServices(Configuration("https://cognito.example.test/pool", "trade-ledger"));
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        options.Authority.ShouldBe("https://cognito.example.test/pool");
        options.Audience.ShouldBe("trade-ledger");
        options.RequireHttpsMetadata.ShouldBe(requireHttpsMetadata);
        options.TokenValidationParameters.ClockSkew.ShouldBe(TimeSpan.FromMinutes(1));
        options.TokenValidationParameters.ValidateIssuer.ShouldBeTrue();
        options.TokenValidationParameters.ValidateAudience.ShouldBeTrue();
        options.TokenValidationParameters.ValidateLifetime.ShouldBeTrue();
        options.TokenValidationParameters.ValidateIssuerSigningKey.ShouldBeTrue();
    }

    [Theory]
    [InlineData("not-a-uri", "trade-ledger")]
    [InlineData("http://cognito.example.test/pool", "trade-ledger")]
    [InlineData("https://cognito.example.test/pool", "")]
    public void AddApiServices_InvalidCognitoConfiguration_FailsOptionsValidation(
        string authority,
        string audience)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(Environment(Environments.Production));
        services.AddApiServices(Configuration(authority, audience));
        using var provider = services.BuildServiceProvider();

        Should.Throw<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<CognitoOptions>>().Value);
    }

    private static IConfiguration Configuration(string authority, string audience) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Cognito:Authority"] = authority,
            ["Authentication:Cognito:Audience"] = audience
        }).Build();

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
