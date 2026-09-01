using Moq;
using Shouldly;
using TradeLedger.Application;
using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Records;
using TradeLedger.Application.Services;
using Xunit;

namespace TradeLedger.UnitTests.Application.Services;

public sealed class ProcessFillServiceTests
{
    private const string Symbol = "ACME";
    private const decimal BuyQuantity = 10m;
    private const decimal FirstLotQuantity = 100m;
    private const decimal SecondLotQuantity = 100m;
    private const decimal SellQuantity = 150m;
    private const decimal SellPrice = 15m;
    private const decimal ExpectedRemainingQuantity = 50m;
    private const decimal ExpectedRealisedPnl = 650m;
    private static readonly DateTimeOffset ProcessedAt =
        new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Process_Buy_AppendsLotAndCompletesFill()
    {
        // Arrange
        var fill = Fill.Create(Guid.NewGuid(), Symbol, Side.Buy, BuyQuantity, 12m, ProcessedAt.AddMinutes(-1));
        var repository = CreateRepositoryForPendingFill(fill, []);
        var service = CreateService(repository);

        // Act
        var processed = await service.ProcessAsync(fill.Id, CancellationToken.None);

        // Assert
        processed.ShouldBeTrue();
        repository.Verify(instance => instance.CompleteAsync(
            fill.Id,
            Symbol,
            It.Is<IReadOnlyList<Lot>>(lots => lots.Count == 1 && lots[0].Id == fill.Id),
            null,
            ProcessedAt,
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Process_Sell_AppliesFifoAndPersistsDatedRealisedPnl()
    {
        // Arrange
        var first = new Lot(Guid.NewGuid(), Symbol, FirstLotQuantity, 10m, ProcessedAt.AddDays(-2));
        var second = new Lot(Guid.NewGuid(), Symbol, SecondLotQuantity, 12m, ProcessedAt.AddDays(-1));
        var sell = Fill.Create(Guid.NewGuid(), Symbol, Side.Sell, SellQuantity, SellPrice, ProcessedAt);
        var repository = CreateRepositoryForPendingFill(sell, [first, second]);
        var service = CreateService(repository);

        // Act
        await service.ProcessAsync(sell.Id, CancellationToken.None);

        // Assert
        repository.Verify(instance => instance.CompleteAsync(
            sell.Id,
            Symbol,
            It.Is<IReadOnlyList<Lot>>(lots =>
                lots.Count == 1 && lots[0].RemainingQuantity == ExpectedRemainingQuantity),
            It.Is<RealisedPnlEntry>(entry =>
                entry.FillId == sell.Id &&
                entry.Amount == ExpectedRealisedPnl &&
                entry.RealisedAt == sell.ExecutedAt),
            ProcessedAt,
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Process_AlreadyProcessedFill_IsIdempotent()
    {
        // Arrange
        var repository = new Mock<IFillProcessingRepository>();
        repository.Setup(instance => instance.GetPendingFillAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Fill?)null);
        var service = CreateService(repository);

        // Act
        var processed = await service.ProcessAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        processed.ShouldBeFalse();
        repository.Verify(instance => instance.CompleteAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<IReadOnlyList<Lot>>(),
            It.IsAny<RealisedPnlEntry?>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IFillProcessingRepository> CreateRepositoryForPendingFill(
        Fill fill,
        IReadOnlyList<Lot> openLots)
    {
        var repository = new Mock<IFillProcessingRepository>();
        repository.Setup(instance => instance.GetPendingFillAsync(fill.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fill);
        repository.Setup(instance => instance.GetOpenLotsAsync(fill.Symbol, It.IsAny<CancellationToken>()))
            .ReturnsAsync(openLots);
        return repository;
    }

    private static ProcessFillService CreateService(Mock<IFillProcessingRepository> repository) =>
        new(repository.Object, CreateClock());

    private static TimeProvider CreateClock()
    {
        var clock = new Mock<TimeProvider>();
        clock.Setup(instance => instance.GetUtcNow()).Returns(ProcessedAt);
        return clock.Object;
    }
}
