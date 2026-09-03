using System.Net;
using System.Text.Json;
using Shouldly;
using TradeLedger.IntegrationTests.Api.Support;
using Xunit;

namespace TradeLedger.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public sealed class ApiInfrastructureTests(TradeLedgerApiFactory factory)
{
    [Fact]
    public async Task Health_WithoutAuthentication_ReturnsHealthy()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain("healthy");
    }

    [Fact]
    public async Task Swagger_DescribesEveryControllerActionAndBearerAuthentication()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var paths = root.GetProperty("paths");
        OperationId(paths, "/api/fills", "post").ShouldBe("CreateFill");
        OperationId(paths, "/api/positions", "get").ShouldBe("GetPositions");
        var lotsOperation = paths.GetProperty("/api/positions/lots").GetProperty("get");
        lotsOperation.GetProperty("operationId").GetString().ShouldBe("GetPositionLots");
        var symbolParameter = lotsOperation.GetProperty("parameters").EnumerateArray()
            .Single(parameter => parameter.GetProperty("name").GetString() == "symbol");
        symbolParameter.GetProperty("in").GetString().ShouldBe("query");
        symbolParameter.GetProperty("required").GetBoolean().ShouldBeTrue();
        OperationId(paths, "/api/explain", "post").ShouldBe("ExplainLedger");
        root.GetProperty("components").GetProperty("securitySchemes")
            .TryGetProperty("Bearer", out _).ShouldBeTrue();
    }

    private static string? OperationId(JsonElement paths, string path, string method) =>
        paths.GetProperty(path).GetProperty(method).GetProperty("operationId").GetString();
}
