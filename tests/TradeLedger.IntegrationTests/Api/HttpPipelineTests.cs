using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TradeLedger.Api.Contracts.Fills;
using TradeLedger.Api.Contracts.Explain;
using TradeLedger.Api.Contracts.Positions;
using TradeLedger.Api.Middleware;
using TradeLedger.Application;
using TradeLedger.Application.Interfaces;
using TradeLedger.Infrastructure.Database;
using TradeLedger.Infrastructure.Database.Entities;
using Xunit;

namespace TradeLedger.IntegrationTests.Api;

public sealed class HttpPipelineTests : IClassFixture<TradeLedgerApiFactory>
{
    private const string Symbol = "ACME";
    private const string ExplainQuestion = "What's my realised P&L on ACME this month?";
    private const string GetPositionsTool = "get_positions()";
    private const string GetMonthlyPnlTool = "get_realised_pnl(\"ACME\", \"month\")";
    private const string GetLotsTool = "get_lots(\"ACME\")";
    private static readonly DateTimeOffset BuyExecutedAt =
        new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SellExecutedAt = BuyExecutedAt.AddDays(1);
    private readonly TradeLedgerApiFactory _factory;

    public HttpPipelineTests(TradeLedgerApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ProtectedEndpoint_WithoutAuthentication_Returns401WithCorrelationHeader()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/positions");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.Contains(CorrelationIdMiddleware.HeaderName).ShouldBeTrue();
    }

    [Fact]
    public async Task Health_WithoutAuthentication_ReturnsPostgresStatus()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain("healthy");
    }

    [Fact]
    public async Task CreateFill_ValidRequest_PersistsPublishesAndReturns202WithStableId()
    {
        using var client = _factory.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, "http-integration-1");
        var id = Guid.NewGuid();
        var request = new CreateFillRequest(
            id,
            " acme ",
            Side.Buy,
            10m,
            12.34m,
            new DateTimeOffset(2026, 9, 1, 17, 0, 0, TimeSpan.FromHours(7)));

        var response = await client.PostAsJsonAsync("/api/fills", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single().ShouldBe("http-integration-1");
        (await response.Content.ReadFromJsonAsync<CreateFillResponse>()).ShouldBe(new CreateFillResponse(id));
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TradeLedgerDbContext>();
        var persisted = await context.Fills.AsNoTracking().SingleAsync(fill => fill.Id == id);
        persisted.Symbol.ShouldBe("ACME");
        persisted.ExecutedAt.Offset.ShouldBe(TimeSpan.Zero);
        scope.ServiceProvider.GetRequiredService<CapturingFillPublisher>()
            .Published.ShouldContain(fill => fill.Id == id && fill.Symbol == "ACME");
    }

    [Fact]
    public async Task CreateFill_InvalidRequest_ReturnsProblemDetailsWithSameCorrelationId()
    {
        using var client = _factory.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, "validation-request");
        var request = new CreateFillRequest(null, "bad symbol", Side.Buy, 0m, -1m, default);

        var response = await client.PostAsJsonAsync("/api/fills", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single().ShouldBe("validation-request");
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("correlationId").GetString().ShouldBe("validation-request");
        problem.RootElement.GetProperty("errors").EnumerateObject().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task CreateFill_MalformedJson_ReturnsModelBindingProblemDetails()
    {
        using var client = _factory.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, "model-binding-request");
        using var content = new StringContent("{", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/fills", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("correlationId").GetString().ShouldBe("model-binding-request");
        problem.RootElement.GetProperty("errors").EnumerateObject().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task PositionEndpoints_ReturnExplicitShapesAndMissingSymbolReturns404()
    {
        var lotId = Guid.NewGuid();
        await SeedLot(lotId, "OMEGA", 5m, 20m, 7m);
        using var client = _factory.CreateAuthenticatedClient();

        var positionsResponse = await client.GetAsync("/api/positions");
        var lotsResponse = await client.GetAsync("/api/positions/omega/lots");
        var missingResponse = await client.GetAsync("/api/positions/missing/lots");

        positionsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var positions = (await positionsResponse.Content.ReadFromJsonAsync<PositionResponse[]>()).ShouldNotBeNull();
        positions.ShouldContain(new PositionResponse("OMEGA", 5m, 20m, 7m));
        lotsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var lots = (await lotsResponse.Content.ReadFromJsonAsync<LotResponse[]>()).ShouldNotBeNull();
        lots.ShouldContain(lot => lot.Id == lotId && lot.Symbol == "OMEGA");
        missingResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        missingResponse.Headers.Contains(CorrelationIdMiddleware.HeaderName).ShouldBeTrue();
    }

    [Fact]
    public async Task InvalidRouteSymbol_Returns400ProblemDetails()
    {
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/positions/%24bad/lots");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task DevelopmentSwagger_DescribesStableOperationsAndBearerAuth()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var create = root.GetProperty("paths").GetProperty("/api/fills").GetProperty("post");
        create.GetProperty("operationId").GetString().ShouldBe("CreateFill");
        create.GetProperty("summary").GetString().ShouldBe("Accepts and queues a fill.");
        root.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _)
            .ShouldBeTrue();
    }

    [Fact]
    public async Task FillProcessingAndExplain_ReturnSerializedApiContracts()
    {
        // Arrange
        const decimal buyQuantity = 100m;
        const decimal sellQuantity = 40m;
        const decimal unitCost = 10m;
        const decimal sellPrice = 15m;
        const decimal remainingQuantity = buyQuantity - sellQuantity;
        const decimal realisedPnl = sellQuantity * (sellPrice - unitCost);
        using var client = _factory.CreateAuthenticatedClient();
        var buyId = Guid.NewGuid();
        var sellId = Guid.NewGuid();

        // Act
        var buyResponse = await client.PostAsJsonAsync("/api/fills", new CreateFillRequest(
            buyId,
            Symbol,
            Side.Buy,
            buyQuantity,
            unitCost,
            BuyExecutedAt));
        await ProcessFill(buyId);
        var sellResponse = await client.PostAsJsonAsync("/api/fills", new CreateFillRequest(
            sellId,
            Symbol,
            Side.Sell,
            sellQuantity,
            sellPrice,
            SellExecutedAt));
        await ProcessFill(sellId);
        var positionsResponse = await client.GetAsync("/api/positions");
        var lotsResponse = await client.GetAsync($"/api/positions/{Symbol}/lots");
        var explainResponse = await client.PostAsJsonAsync(
            "/api/explain",
            new ExplainRequest(ExplainQuestion));

        // Assert
        await AssertAcceptedFillResponseAsync(buyResponse, buyId);
        await AssertAcceptedFillResponseAsync(sellResponse, sellId);
        await AssertPositionsResponseAsync(
            positionsResponse,
            new PositionResponse(Symbol, remainingQuantity, unitCost, realisedPnl));
        await AssertLotsResponseAsync(
            lotsResponse,
            new LotResponse(buyId, Symbol, remainingQuantity, unitCost, BuyExecutedAt));
        await AssertExplainResponseAsync(explainResponse, "+£200.00");
    }

    private async Task SeedLot(Guid id, string symbol, decimal remaining, decimal cost, decimal realisedPnl)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TradeLedgerDbContext>();
        context.Lots.Add(new LotEntity
        {
            Id = id,
            Symbol = symbol,
            RemainingQuantity = remaining,
            UnitCost = cost,
            OpenedAt = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero)
        });
        if (realisedPnl != 0)
        {
            context.RealisedPnlEntries.Add(new RealisedPnlEntryEntity
            {
                FillId = Guid.NewGuid(),
                Symbol = symbol,
                Amount = realisedPnl,
                RealisedAt = new DateTimeOffset(2026, 9, 1, 11, 0, 0, TimeSpan.Zero)
            });
        }

        await context.SaveChangesAsync();
    }

    private async Task ProcessFill(Guid fillId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IProcessFillService>()
            .ProcessAsync(fillId, CancellationToken.None);
    }

    private static async Task AssertAcceptedFillResponseAsync(HttpResponseMessage response, Guid fillId)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await response.Content.ReadFromJsonAsync<CreateFillResponse>())
            .ShouldBe(new CreateFillResponse(fillId));
    }

    private static async Task AssertPositionsResponseAsync(
        HttpResponseMessage response,
        PositionResponse expectedPosition)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var positions = (await response.Content.ReadFromJsonAsync<PositionResponse[]>()).ShouldNotBeNull();
        positions.ShouldBe([expectedPosition]);
    }

    private static async Task AssertLotsResponseAsync(
        HttpResponseMessage response,
        LotResponse expectedLot)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var lots = (await response.Content.ReadFromJsonAsync<LotResponse[]>()).ShouldNotBeNull();
        lots.ShouldBe([expectedLot]);
    }

    private static async Task AssertExplainResponseAsync(
        HttpResponseMessage response,
        string expectedAmount)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var explanation = (await response.Content.ReadFromJsonAsync<ExplainResponse>()).ShouldNotBeNull();
        explanation.ToolCalls.ShouldBe([GetPositionsTool, GetMonthlyPnlTool, GetLotsTool]);
        explanation.Answer.ShouldContain(expectedAmount);
    }
}
