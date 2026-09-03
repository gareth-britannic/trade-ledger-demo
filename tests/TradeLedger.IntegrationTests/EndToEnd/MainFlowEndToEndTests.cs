using System.Net;
using System.Net.Http.Json;
using Shouldly;
using TradeLedger.Api.Contracts.Fills;
using TradeLedger.Api.Contracts.Positions;
using TradeLedger.Domain;
using TradeLedger.IntegrationTests.Support;
using Xunit;

namespace TradeLedger.IntegrationTests.EndToEnd;

[Collection(LocalStackCollection.Name)]
public sealed class MainFlowEndToEndTests
{
    private static readonly DateTimeOffset ExecutedAt =
        new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);

    [LocalStackFact]
    [Trait("Category", "External")]
    public async Task AddFills_ProcessesFifoLedgerAndReturnsPositionAndOpenLots()
    {
        // Arrange
        await using var factory = new TradeLedgerEndToEndFactory();
        using var client = factory.CreateAuthenticatedClient();
        var symbol = $"E2E{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var firstBuyId = Guid.NewGuid();
        var secondBuyId = Guid.NewGuid();
        var sellId = Guid.NewGuid();
        CreateFillRequest[] requests =
        [
            new(firstBuyId, symbol, Side.Buy, 100m, 10m, ExecutedAt),
            new(secondBuyId, symbol, Side.Buy, 100m, 12m, ExecutedAt.AddMinutes(1)),
            new(sellId, symbol, Side.Sell, 150m, 15m, ExecutedAt.AddMinutes(2))
        ];

        // Act
        foreach (var request in requests)
        {
            using var response = await client.PostAsJsonAsync("/api/fills", request);

            response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            (await response.Content.ReadFromJsonAsync<CreateFillResponse>())
                .ShouldBe(new CreateFillResponse(request.FillId!.Value));
        }

        var position = await WaitForFinalPositionAsync(client, symbol);
        using var lotsResponse = await client.GetAsync(
            $"/api/positions/lots?symbol={Uri.EscapeDataString(symbol)}");

        // Assert
        position.ShouldBe(new PositionResponse(symbol, 50m, 12m, 650m));
        lotsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await lotsResponse.Content.ReadFromJsonAsync<LotResponse[]>()).ShouldBe([
            new LotResponse(secondBuyId, symbol, 50m, 12m, ExecutedAt.AddMinutes(1))
        ]);
    }

    private static async Task<PositionResponse> WaitForFinalPositionAsync(HttpClient client, string symbol)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(90);
        PositionResponse? latest = null;

        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            using var response = await client.GetAsync("/api/positions");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var positions = await response.Content.ReadFromJsonAsync<PositionResponse[]>();
            latest = positions?.SingleOrDefault(position => position.Symbol == symbol);

            if (latest == new PositionResponse(symbol, 50m, 12m, 650m))
            {
                return latest;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException(
            $"The API did not return the final FIFO position within the bounded timeout. Last state: {latest}");
    }
}
