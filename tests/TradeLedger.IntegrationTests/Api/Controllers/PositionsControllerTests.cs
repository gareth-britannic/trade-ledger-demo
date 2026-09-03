using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using TradeLedger.Api.Contracts.Positions;
using TradeLedger.Api.Middleware;
using TradeLedger.IntegrationTests.Api.Support;
using Xunit;

namespace TradeLedger.IntegrationTests.Api.Controllers;

[Collection(ApiCollection.Name)]
public sealed class PositionsControllerTests(TradeLedgerApiFactory factory)
{
    [Fact]
    public async Task Get_ReturnsPositionsInSymbolOrderWithValuesDerivedFromOpenLots()
    {
        await ApiTestData.ResetAsync(factory.Services);
        var openedAt = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
        await ApiTestData.SeedPositionAsync(
            factory.Services,
            "BETA",
            3m,
            new LotSeed(Guid.NewGuid(), 5m, 7m, openedAt));
        await ApiTestData.SeedPositionAsync(
            factory.Services,
            "ACME",
            20m,
            new LotSeed(Guid.NewGuid(), 10m, 10m, openedAt),
            new LotSeed(Guid.NewGuid(), 10m, 20m, openedAt.AddMinutes(1)));
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/positions");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        (await response.Content.ReadFromJsonAsync<PositionResponse[]>()).ShouldBe([
            new PositionResponse("ACME", 20m, 15m, 20m),
            new PositionResponse("BETA", 5m, 7m, 3m)
        ]);
    }

    [Fact]
    public async Task Get_WhenLedgerIsEmpty_ReturnsEmptyArray()
    {
        await ApiTestData.ResetAsync(factory.Services);
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/positions");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<PositionResponse[]>()).ShouldBeEmpty();
    }

    [Fact]
    public async Task GetLots_QueryParameterRoundTripsSlashSymbolAndReturnsFifoOrder()
    {
        await ApiTestData.ResetAsync(factory.Services);
        var earlyId = Guid.NewGuid();
        var lateId = Guid.NewGuid();
        var early = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
        var late = early.AddMinutes(1);
        await ApiTestData.SeedPositionAsync(
            factory.Services,
            "BRK/B",
            7m,
            new LotSeed(lateId, 4m, 12m, late),
            new LotSeed(earlyId, 6m, 10m, early));
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/positions/lots?symbol=%20brk%2Fb%20");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<LotResponse[]>()).ShouldBe([
            new LotResponse(earlyId, "BRK/B", 6m, 10m, early),
            new LotResponse(lateId, "BRK/B", 4m, 12m, late)
        ]);
    }

    [Fact]
    public async Task GetLots_WhenPositionDoesNotExist_ReturnsNotFoundProblem()
    {
        await ApiTestData.ResetAsync(factory.Services);
        using var client = factory.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, "missing-position-correlation");

        var response = await client.GetAsync("/api/positions/lots?symbol=missing");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("status").GetInt32().ShouldBe(404);
        problem.RootElement.GetProperty("detail").GetString()
            .ShouldBe("Position 'MISSING' was not found.");
        problem.RootElement.GetProperty("correlationId").GetString()
            .ShouldBe("missing-position-correlation");
    }

    [Fact]
    public async Task GetLots_WhenSymbolIsInvalid_ReturnsValidationProblem()
    {
        using var client = factory.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, "invalid-symbol-correlation");

        var response = await client.GetAsync("/api/positions/lots?symbol=%24bad");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("errors").TryGetProperty("symbol", out _).ShouldBeTrue();
        problem.RootElement.GetProperty("correlationId").GetString()
            .ShouldBe("invalid-symbol-correlation");
    }

    [Fact]
    public async Task GetLots_WhenSymbolQueryIsMissing_ReturnsValidationProblem()
    {
        using var client = factory.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, "missing-symbol-correlation");

        var response = await client.GetAsync("/api/positions/lots");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("errors").TryGetProperty("symbol", out _).ShouldBeTrue();
        problem.RootElement.GetProperty("correlationId").GetString()
            .ShouldBe("missing-symbol-correlation");
    }

    [Fact]
    public async Task Get_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/positions");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.Contains(CorrelationIdMiddleware.HeaderName).ShouldBeTrue();
    }
}
