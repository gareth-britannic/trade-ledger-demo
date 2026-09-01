using Shouldly;
using global::TradeLedger.Domain;
using global::TradeLedger.Domain.Records;
using Xunit;

namespace TradeLedger.UnitTests.Domain;

public class FifoMatcherTests
{
    private const string Symbol = "ACME";
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ApplyBuy_AppendsANewLotWithoutChangingTheExistingLots()
    {
        // Arrange
        var existingLot = Lot(100m, 10m);
        IReadOnlyList<Lot> openLots = [existingLot];
        var buy = Buy(100m, 12m, Start.AddMinutes(1));

        // Act
        var result = FifoMatcher.ApplyBuy(openLots, buy);

        // Assert
        result.ShouldBe([
            existingLot,
            new Lot(buy.Id, Symbol, 100m, 12m, buy.ExecutedAt)
        ]);
        openLots.ShouldBe([existingLot]);
    }

    [Fact]
    public void ApplyBuy_WhenOpenLotsIsNull_Throws()
    {
        // Arrange
        var buy = Buy(100m, 10m, Start);

        // Act
        var action = () => FifoMatcher.ApplyBuy(null!, buy);

        // Assert
        Should.Throw<ArgumentNullException>(action).ParamName.ShouldBe("openLots");
    }

    [Fact]
    public void ApplyBuy_WhenFillIsNull_Throws()
    {
        // Arrange
        IReadOnlyList<Lot> openLots = [];

        // Act
        var action = () => FifoMatcher.ApplyBuy(openLots, null!);

        // Assert
        Should.Throw<ArgumentNullException>(action).ParamName.ShouldBe("fill");
    }

    [Fact]
    public void ApplyBuy_WhenFillIsNotABuy_Throws()
    {
        // Arrange
        var sell = Sell(100m, 10m, Start);

        // Act
        var action = () => FifoMatcher.ApplyBuy([], sell);

        // Assert
        Should.Throw<ArgumentException>(action).ParamName.ShouldBe("fill");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ApplyBuy_WhenSymbolIsMissing_Throws(string? symbol)
    {
        // Arrange
        var buy = Buy(100m, 10m, Start) with { Symbol = symbol! };

        // Act
        var action = () => FifoMatcher.ApplyBuy([], buy);

        // Assert
        Should.Throw<ArgumentException>(action).ParamName.ShouldBe("fill");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ApplyBuy_WhenQuantityIsNotPositive_Throws(decimal quantity)
    {
        // Arrange
        var buy = Buy(quantity, 10m, Start);

        // Act
        var action = () => FifoMatcher.ApplyBuy([], buy);

        // Assert
        Should.Throw<ArgumentOutOfRangeException>(action).ParamName.ShouldBe("fill");
    }

    [Fact]
    public void ApplyBuy_WhenPriceIsNegative_Throws()
    {
        // Arrange
        var buy = Buy(100m, -0.01m, Start);

        // Act
        var action = () => FifoMatcher.ApplyBuy([], buy);

        // Assert
        Should.Throw<ArgumentOutOfRangeException>(action).ParamName.ShouldBe("fill");
    }

    [Fact]
    public void ApplyBuy_WhenAnOpenLotHasAnotherSymbol_Throws()
    {
        // Arrange
        IReadOnlyList<Lot> openLots = [Lot(100m, 10m) with { Symbol = "OTHER" }];
        var buy = Buy(100m, 12m, Start.AddMinutes(1));

        // Act
        var action = () => FifoMatcher.ApplyBuy(openLots, buy);

        // Assert
        Should.Throw<ArgumentException>(action).ParamName.ShouldBe("openLots");
    }

    [Fact]
    public void ApplySell_MatchesOldestLotsFirst()
    {
        // Arrange
        var firstBuy = Buy(100m, 10m, Start);
        var secondBuy = Buy(100m, 12m, Start.AddMinutes(1));
        var lots = FifoMatcher.ApplyBuy([], firstBuy);
        lots = FifoMatcher.ApplyBuy(lots, secondBuy);
        var sell = Sell(150m, 15m, Start.AddMinutes(2));

        // Act
        var result = FifoMatcher.ApplySell(lots, sell);

        // Assert
        result.RealisedPnl.ShouldBe(650m);
        result.RemainingLots.ShouldBe([
            new Lot(secondBuy.Id, Symbol, 50m, 12m, secondBuy.ExecutedAt)
        ]);
    }

    [Fact]
    public void ApplySell_WhenOldestLotIsExactlyConsumed_PreservesLaterLots()
    {
        // Arrange
        var oldestLot = Lot(100m, 10m);
        var laterLot = Lot(50m, 12m, Start.AddMinutes(1));
        IReadOnlyList<Lot> openLots = [oldestLot, laterLot];
        var sell = Sell(100m, 15m, Start.AddMinutes(2));

        // Act
        var result = FifoMatcher.ApplySell(openLots, sell);

        // Assert
        result.RealisedPnl.ShouldBe(500m);
        result.RemainingLots.ShouldBe([laterLot]);
        openLots.ShouldBe([oldestLot, laterLot]);
    }

    [Fact]
    public void ApplySell_WhenAllLotsAreConsumed_ReturnsNoRemainingLots()
    {
        // Arrange
        IReadOnlyList<Lot> openLots = [Lot(100m, 10m), Lot(50m, 12m, Start.AddMinutes(1))];
        var sell = Sell(150m, 15m, Start.AddMinutes(2));

        // Act
        var result = FifoMatcher.ApplySell(openLots, sell);

        // Assert
        result.RealisedPnl.ShouldBe(650m);
        result.RemainingLots.ShouldBeEmpty();
    }

    [Fact]
    public void ApplySell_WhenOpenLotsIsNull_Throws()
    {
        // Arrange
        var sell = Sell(100m, 15m, Start);

        // Act
        var action = () => FifoMatcher.ApplySell(null!, sell);

        // Assert
        Should.Throw<ArgumentNullException>(action).ParamName.ShouldBe("openLots");
    }

    [Fact]
    public void ApplySell_WhenFillIsNull_Throws()
    {
        // Arrange
        IReadOnlyList<Lot> openLots = [Lot(100m, 10m)];

        // Act
        var action = () => FifoMatcher.ApplySell(openLots, null!);

        // Assert
        Should.Throw<ArgumentNullException>(action).ParamName.ShouldBe("fill");
    }

    [Fact]
    public void ApplySell_WhenFillIsNotASell_Throws()
    {
        // Arrange
        IReadOnlyList<Lot> openLots = [Lot(100m, 10m)];
        var buy = Buy(100m, 15m, Start.AddMinutes(1));

        // Act
        var action = () => FifoMatcher.ApplySell(openLots, buy);

        // Assert
        Should.Throw<ArgumentException>(action).ParamName.ShouldBe("fill");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ApplySell_WhenSymbolIsMissing_Throws(string? symbol)
    {
        // Arrange
        IReadOnlyList<Lot> openLots = [Lot(100m, 10m)];
        var sell = Sell(100m, 15m, Start.AddMinutes(1)) with { Symbol = symbol! };

        // Act
        var action = () => FifoMatcher.ApplySell(openLots, sell);

        // Assert
        Should.Throw<ArgumentException>(action).ParamName.ShouldBe("fill");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ApplySell_WhenQuantityIsNotPositive_Throws(decimal quantity)
    {
        // Arrange
        IReadOnlyList<Lot> openLots = [Lot(100m, 10m)];
        var sell = Sell(quantity, 15m, Start.AddMinutes(1));

        // Act
        var action = () => FifoMatcher.ApplySell(openLots, sell);

        // Assert
        Should.Throw<ArgumentOutOfRangeException>(action).ParamName.ShouldBe("fill");
    }

    [Fact]
    public void ApplySell_WhenPriceIsNegative_Throws()
    {
        // Arrange
        IReadOnlyList<Lot> openLots = [Lot(100m, 10m)];
        var sell = Sell(100m, -0.01m, Start.AddMinutes(1));

        // Act
        var action = () => FifoMatcher.ApplySell(openLots, sell);

        // Assert
        Should.Throw<ArgumentOutOfRangeException>(action).ParamName.ShouldBe("fill");
    }

    [Fact]
    public void ApplySell_WhenAnOpenLotHasAnotherSymbol_Throws()
    {
        // Arrange
        IReadOnlyList<Lot> openLots = [Lot(100m, 10m) with { Symbol = "OTHER" }];
        var sell = Sell(100m, 15m, Start.AddMinutes(1));

        // Act
        var action = () => FifoMatcher.ApplySell(openLots, sell);

        // Assert
        Should.Throw<ArgumentException>(action).ParamName.ShouldBe("openLots");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ApplySell_WhenAnOpenLotDoesNotHavePositiveQuantity_Throws(decimal quantity)
    {
        // Arrange
        IReadOnlyList<Lot> openLots = [Lot(quantity, 10m)];
        var sell = Sell(1m, 15m, Start.AddMinutes(1));

        // Act
        var action = () => FifoMatcher.ApplySell(openLots, sell);

        // Assert
        Should.Throw<ArgumentException>(action).ParamName.ShouldBe("openLots");
    }

    [Fact]
    public void ApplySell_WhenQuantityExceedsOpenQuantity_Throws()
    {
        // Arrange
        IReadOnlyList<Lot> openLots = [Lot(100m, 10m)];
        var sell = Sell(101m, 15m, Start.AddMinutes(1));

        // Act
        var action = () => FifoMatcher.ApplySell(openLots, sell);

        // Assert
        Should.Throw<InvalidOperationException>(action);
    }

    private static Fill Buy(decimal quantity, decimal price, DateTimeOffset executedAt) =>
        new(Guid.NewGuid(), Symbol, Side.Buy, quantity, price, executedAt);

    private static Fill Sell(decimal quantity, decimal price, DateTimeOffset executedAt) =>
        new(Guid.NewGuid(), Symbol, Side.Sell, quantity, price, executedAt);

    private static Lot Lot(decimal quantity, decimal unitCost, DateTimeOffset? openedAt = null) =>
        new(Guid.NewGuid(), Symbol, quantity, unitCost, openedAt ?? Start);
}
