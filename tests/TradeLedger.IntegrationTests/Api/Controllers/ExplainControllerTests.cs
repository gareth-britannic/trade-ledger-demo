using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using TradeLedger.Api.Contracts.Explain;
using TradeLedger.Api.Middleware;
using TradeLedger.IntegrationTests.Api.Support;
using Xunit;

namespace TradeLedger.IntegrationTests.Api.Controllers;

[Collection(ApiCollection.Name)]
public sealed class ExplainControllerTests(TradeLedgerApiFactory factory)
{
    [Fact]
    public async Task Explain_KnownSymbol_ReturnsToolCallsAndAnswerFromPersistedLedger()
    {
        await ApiTestData.ResetAsync(factory.Services);
        await ApiTestData.SeedPositionAsync(
            factory.Services,
            "ACME",
            200m,
            new LotSeed(
                Guid.NewGuid(),
                60m,
                10m,
                new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero)));
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync(
            "/api/explain",
            new ExplainRequest("What's my realised P&L on ACME this month?"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        var explanation = (await response.Content.ReadFromJsonAsync<ExplainResponse>()).ShouldNotBeNull();
        explanation.ToolCalls.ShouldBe([
            "get_positions()",
            "get_realised_pnl(\"ACME\", \"month\")",
            "get_lots(\"ACME\")"
        ]);
        explanation.Answer.ShouldBe(
            "Your realised P&L on ACME this month is +£200.00. " +
            "There are 1 open lots, ordered oldest first for FIFO.");
    }

    [Fact]
    public async Task Explain_QuestionWithoutKnownSymbol_ReturnsNoMatchAnswer()
    {
        await ApiTestData.ResetAsync(factory.Services);
        await ApiTestData.SeedPositionAsync(
            factory.Services,
            "ACME",
            0m,
            new LotSeed(
                Guid.NewGuid(),
                1m,
                10m,
                new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero)));
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync(
            "/api/explain",
            new ExplainRequest("What happened today?"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var explanation = (await response.Content.ReadFromJsonAsync<ExplainResponse>()).ShouldNotBeNull();
        explanation.ToolCalls.ShouldBe(["get_positions()"]);
        explanation.Answer.ShouldBe("I couldn't find a position symbol in that question.");
    }

    [Fact]
    public async Task Explain_BlankQuestion_ReturnsValidationProblem()
    {
        using var client = factory.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, "invalid-explain-correlation");

        var response = await client.PostAsJsonAsync("/api/explain", new ExplainRequest(" "));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("errors").TryGetProperty("Question", out _).ShouldBeTrue();
        problem.RootElement.GetProperty("correlationId").GetString()
            .ShouldBe("invalid-explain-correlation");
    }

    [Fact]
    public async Task Explain_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/explain",
            new ExplainRequest("What's my realised P&L on ACME?"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.Contains(CorrelationIdMiddleware.HeaderName).ShouldBeTrue();
    }
}
