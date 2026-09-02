using Moq;
using Shouldly;
using TradeLedger.Application;
using TradeLedger.Application.Exceptions;
using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Records;
using TradeLedger.Application.Services;
using TradeLedger.Domain;
using Xunit;

namespace TradeLedger.UnitTests.Application.Services;

public sealed class PositionQueryServiceTests
{
    [Fact]
    public async Task GetPositions_MapsQuantitiesWeightedCostAndRealisedPnl()
    {
        var repository = new Mock<IPositionRepository>();
        repository.Setup(instance => instance.GetPositionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new Position("ACME", [Lot(20m, 10m), Lot(10m, 16m)], 42m)
            ]);
        var service = new PositionQueryService(repository.Object);

        var result = await service.GetPositionsAsync(CancellationToken.None);

        result.ShouldHaveSingleItem().ShouldBe(new PositionResult("ACME", 30m, 12m, 42m));
    }

    [Fact]
    public async Task GetPositions_ClosedPositionDoesNotFabricateAverageCost()
    {
        var repository = new Mock<IPositionRepository>();
        repository.Setup(instance => instance.GetPositionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Position("ACME", [], 42m)]);
        var service = new PositionQueryService(repository.Object);

        var result = await service.GetPositionsAsync(CancellationToken.None);

        result.Single().AverageUnitCost.ShouldBeNull();
    }

    [Fact]
    public async Task GetOpenLots_NormalizesSymbolAndMapsLots()
    {
        using var source = new CancellationTokenSource();
        var lot = Lot(20m, 10m);
        var repository = new Mock<IPositionRepository>();
        repository.Setup(instance => instance.GetPositionAsync("ACME", source.Token))
            .ReturnsAsync(new Position("ACME", [lot], 0m));
        var service = new PositionQueryService(repository.Object);

        var result = await service.GetOpenLotsAsync(" acme ", source.Token);

        result.ShouldBe([new LotResult(lot.Id, lot.Symbol, lot.RemainingQuantity, lot.UnitCost, lot.OpenedAt)]);
        repository.VerifyAll();
    }

    [Fact]
    public async Task GetOpenLots_MissingPosition_ThrowsNotFound()
    {
        var repository = new Mock<IPositionRepository>();
        repository.Setup(instance => instance.GetPositionAsync("MISSING", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Position?)null);
        var service = new PositionQueryService(repository.Object);

        await Should.ThrowAsync<ResourceNotFoundException>(() =>
            service.GetOpenLotsAsync("missing", CancellationToken.None));
    }

    private static Lot Lot(decimal quantity, decimal cost) => new(
        Guid.NewGuid(),
        "ACME",
        quantity,
        cost,
        new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero));
}
