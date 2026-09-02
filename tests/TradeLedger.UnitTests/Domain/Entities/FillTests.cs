using Shouldly;
using TradeLedger.Application;
using TradeLedger.Application.Records;
using TradeLedger.Domain;
using Xunit;

namespace TradeLedger.UnitTests.Domain.Records;

public sealed class FillTests
{
    private static readonly DateTimeOffset ExecutedAt =
        new(2026, 9, 1, 17, 0, 0, TimeSpan.FromHours(7));

    [Fact]
    public void Create_NormalizesSymbolAndTimestamp()
    {
        var fill = Fill.Create(Guid.NewGuid(), " acme ", Side.Buy, 10m, 12m, ExecutedAt);

        fill.Symbol.ShouldBe("ACME");
        fill.ExecutedAt.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Create_EmptyId_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() =>
            Fill.Create(Guid.Empty, "ACME", Side.Buy, 10m, 12m, ExecutedAt));

        exception.ParamName.ShouldBe("id");
    }

    [Fact]
    public void Create_InvalidSymbol_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() =>
            Fill.Create(Guid.NewGuid(), "$ACME", Side.Buy, 10m, 12m, ExecutedAt));

        exception.ParamName.ShouldBe("symbol");
    }

    [Fact]
    public void Create_InvalidSide_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            Fill.Create(Guid.NewGuid(), "ACME", (Side)999, 10m, 12m, ExecutedAt));

        exception.ParamName.ShouldBe("side");
    }

    [Fact]
    public void Create_NonPositiveQuantity_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            Fill.Create(Guid.NewGuid(), "ACME", Side.Buy, 0m, 12m, ExecutedAt));

        exception.ParamName.ShouldBe("quantity");
    }

    [Fact]
    public void Create_NonPositivePrice_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            Fill.Create(Guid.NewGuid(), "ACME", Side.Buy, 10m, 0m, ExecutedAt));

        exception.ParamName.ShouldBe("price");
    }

    [Fact]
    public void Create_MissingTimestamp_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() =>
            Fill.Create(Guid.NewGuid(), "ACME", Side.Buy, 10m, 12m, default));

        exception.ParamName.ShouldBe("executedAt");
    }
}
