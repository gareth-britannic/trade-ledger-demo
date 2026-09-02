using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TradeLedger.Api.Contracts.Fills;
using TradeLedger.Api.Middleware;
using TradeLedger.Database;
using TradeLedger.Domain;
using TradeLedger.IntegrationTests.Api.Support;
using Xunit;

namespace TradeLedger.IntegrationTests.Api.Controllers;

[Collection(ApiCollection.Name)]
public sealed class FillsControllerTests(TradeLedgerApiFactory factory)
{
    [Fact]
    public async Task Create_ValidRequest_ReturnsAcceptedPersistsPendingRequestAndPublishesMessage()
    {
        await ResetCaptureAsync();
        var sqs = factory.Services.GetRequiredService<CapturingSqsClient>();
        using var client = factory.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, "create-fill-correlation");
        var fillId = Guid.NewGuid();
        var executedAt = new DateTimeOffset(2026, 9, 1, 17, 0, 0, TimeSpan.FromHours(7));
        var request = new CreateFillRequest(fillId, " acme ", Side.Sell, 10m, 12.34m, executedAt);

        var response = await client.PostAsJsonAsync("/api/fills", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single()
            .ShouldBe("create-fill-correlation");
        (await response.Content.ReadFromJsonAsync<CreateFillResponse>())
            .ShouldBe(new CreateFillResponse(fillId));

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TradeLedgerDbContext>();
        var persisted = await context.Fills.AsNoTracking().SingleAsync(fill => fill.Id == fillId);
        persisted.Symbol.ShouldBe("ACME");
        persisted.Side.ShouldBe(Side.Sell);
        persisted.Quantity.ShouldBe(10m);
        persisted.Price.ShouldBe(12.34m);
        persisted.ExecutedAt.ShouldBe(executedAt.ToUniversalTime());
        persisted.ProcessedAt.ShouldBeNull();

        var sent = sqs.Sent.ShouldHaveSingleItem();
        sent.Message.FillId.ShouldBe(fillId);
        sent.Message.Symbol.ShouldBe("ACME");
        sent.Message.Side.ShouldBe(nameof(Side.Sell));
        sent.Message.Quantity.ShouldBe(10m);
        sent.Message.Price.ShouldBe(12.34m);
        sent.Message.ExecutedAt.ShouldBe(executedAt.ToUniversalTime());
        sent.MessageGroupId.ShouldBe("ACME");
        sent.DeduplicationId.ShouldBe(fillId.ToString("D"));
    }

    [Fact]
    public async Task Create_InvalidRequest_ReturnsValidationProblemWithEveryInvalidField()
    {
        await ResetCaptureAsync();
        using var client = factory.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, "invalid-fill-correlation");
        var request = new CreateFillRequest(
            Guid.Empty,
            "bad symbol",
            (Side)999,
            0m,
            -1m,
            default);

        var response = await client.PostAsJsonAsync("/api/fills", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single()
            .ShouldBe("invalid-fill-correlation");
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("correlationId").GetString()
            .ShouldBe("invalid-fill-correlation");
        var errors = problem.RootElement.GetProperty("errors");
        errors.TryGetProperty("FillId", out _).ShouldBeTrue();
        errors.TryGetProperty("Symbol", out _).ShouldBeTrue();
        errors.TryGetProperty("Side", out _).ShouldBeTrue();
        errors.TryGetProperty("Quantity", out _).ShouldBeTrue();
        errors.TryGetProperty("Price", out _).ShouldBeTrue();
        errors.TryGetProperty("ExecutedAt", out _).ShouldBeTrue();
        await AssertNothingAcceptedAsync();
    }

    [Fact]
    public async Task Create_MalformedJson_ReturnsModelBindingProblem()
    {
        await ResetCaptureAsync();
        using var client = factory.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, "malformed-fill-correlation");
        using var content = new StringContent("{", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/fills", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("correlationId").GetString()
            .ShouldBe("malformed-fill-correlation");
        problem.RootElement.GetProperty("errors").EnumerateObject().ShouldNotBeEmpty();
        await AssertNothingAcceptedAsync();
    }

    [Fact]
    public async Task Create_WithoutAuthentication_ReturnsUnauthorized()
    {
        await ResetCaptureAsync();
        using var client = factory.CreateClient();
        var request = new CreateFillRequest(
            Guid.NewGuid(),
            "ACME",
            Side.Buy,
            1m,
            10m,
            DateTimeOffset.UtcNow);

        var response = await client.PostAsJsonAsync("/api/fills", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.Contains(CorrelationIdMiddleware.HeaderName).ShouldBeTrue();
        await AssertNothingAcceptedAsync();
    }

    private async Task ResetCaptureAsync()
    {
        await ApiTestData.ResetAsync(factory.Services);
        factory.Services.GetRequiredService<CapturingSqsClient>().Sent.Clear();
    }

    private async Task AssertNothingAcceptedAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TradeLedgerDbContext>();
        (await context.Fills.AsNoTracking().AnyAsync()).ShouldBeFalse();
        factory.Services.GetRequiredService<CapturingSqsClient>().Sent.ShouldBeEmpty();
    }
}
