using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeLedger.Application;
using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Records;
using TradeLedger.Infrastructure.Database;

namespace TradeLedger.IntegrationTests.Api;

public sealed class TradeLedgerApiFactory : WebApplicationFactory<Program>
{
    public const string AuthenticationScheme = "IntegrationTest";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = "Host=unused;Database=unused;Username=unused;Password=unused",
                ["FillQueue:Url"] = "https://sqs.example.test/fills.fifo",
                ["FillProcessing:Enabled"] = "false",
                ["Authentication:Cognito:Authority"] = "https://cognito.example.test/user-pool",
                ["Authentication:Cognito:Audience"] = "integration-tests"
            }));
        builder.ConfigureServices(services =>
        {
            var databaseName = $"trade-ledger-{Guid.NewGuid():N}";
            services.RemoveAll<DbContextOptions<TradeLedgerDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<TradeLedgerDbContext>>();
            services.RemoveAll<TradeLedgerDbContext>();
            services.AddDbContext<TradeLedgerDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));

            services.RemoveAll<IFillPublisher>();
            services.AddSingleton<CapturingFillPublisher>();
            services.AddSingleton<IFillPublisher>(provider => provider.GetRequiredService<CapturingFillPublisher>());
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new IntegrationTimeProvider(
                new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero)));
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = AuthenticationScheme;
                    options.DefaultChallengeScheme = AuthenticationScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(AuthenticationScheme, _ => { });
        });
    }

    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
        return client;
    }
}

internal sealed class IntegrationTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}

public sealed class CapturingFillPublisher : IFillPublisher
{
    public List<Fill> Published { get; } = [];

    public Task PublishAsync(Fill fill, CancellationToken cancellationToken)
    {
        Published.Add(fill);
        return Task.CompletedTask;
    }
}

internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authorization) ||
            authorization.ToString() != "Bearer test-token")
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "integration-user")], Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
