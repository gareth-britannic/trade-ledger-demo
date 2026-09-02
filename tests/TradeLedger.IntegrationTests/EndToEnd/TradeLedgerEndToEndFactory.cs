using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradeLedger.IntegrationTests.Api;

namespace TradeLedger.IntegrationTests.EndToEnd;

internal sealed class TradeLedgerEndToEndFactory : WebApplicationFactory<Program>
{
    private const string AuthenticationScheme = "EndToEndTest";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] =
                    "Host=localhost;Port=55432;Database=trade_ledger;Username=trade_ledger;Password=trade_ledger",
                ["FillQueue:Url"] =
                    "http://localhost:4566/000000000000/trade-ledger-fills.fifo",
                ["Authentication:Cognito:Authority"] = "https://cognito.example.test/user-pool",
                ["Authentication:Cognito:Audience"] = "end-to-end-tests"
            }));
        builder.ConfigureServices(services =>
        {
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
