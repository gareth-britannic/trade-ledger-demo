using Moq;
using Shouldly;
using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Records;
using TradeLedger.Application.Services;
using Xunit;

namespace TradeLedger.UnitTests.Application.Services;

public sealed class InMemoryLlmClientTests
{
    private const string Symbol = "AAPL";
    private const string MonthlyQuestion = "What's my realised P&L on AAPL this month?";
    private const string UnknownSymbolQuestion = "What happened to TSLA?";
    private const string GetPositionsTool = "get_positions()";
    private const string GetMonthlyPnlTool = "get_realised_pnl(\"AAPL\", \"month\")";
    private const string GetLotsTool = "get_lots(\"AAPL\")";
    private const decimal ExpectedRealisedPnl = 1240.50m;
    private static readonly DateTimeOffset Now =
        new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Explain_MonthlyQuestion_ReturnsActualToolCallsAndAnswer()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        var positions = new Mock<IPositionQueryService>();
        positions.Setup(instance => instance.GetPositionsAsync(source.Token))
            .ReturnsAsync([new PositionResult(Symbol, 250m, 184.32m, ExpectedRealisedPnl)]);
        positions.Setup(instance => instance.GetOpenLotsAsync(Symbol, source.Token))
            .ReturnsAsync([
                new LotResult(Guid.NewGuid(), Symbol, 100m, 172m, Now.AddMonths(-2)),
                new LotResult(Guid.NewGuid(), Symbol, 150m, 192.53m, Now.AddMonths(-1))
            ]);
        var realisedPnl = new Mock<IRealisedPnlRepository>();
        realisedPnl.Setup(instance => instance.GetTotalAsync(
                Symbol,
                new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
                source.Token))
            .ReturnsAsync(ExpectedRealisedPnl);
        var client = CreateClient(positions, realisedPnl);

        // Act
        var result = await client.ExplainAsync(MonthlyQuestion, source.Token);

        // Assert
        result.ToolCalls.ShouldBe([
            GetPositionsTool,
            GetMonthlyPnlTool,
            GetLotsTool
        ]);
        result.Answer.ShouldContain("+£1,240.50");
        result.Answer.ShouldContain("2 open lots");
        positions.VerifyAll();
        realisedPnl.VerifyAll();
    }

    [Fact]
    public async Task Explain_QuestionWithoutKnownSymbol_ReturnsAfterPositionsTool()
    {
        // Arrange
        var positions = new Mock<IPositionQueryService>();
        positions.Setup(instance => instance.GetPositionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PositionResult(Symbol, 250m, 184.32m, ExpectedRealisedPnl)]);
        var realisedPnl = new Mock<IRealisedPnlRepository>();
        var client = CreateClient(positions, realisedPnl);

        // Act
        var result = await client.ExplainAsync(UnknownSymbolQuestion, CancellationToken.None);

        // Assert
        result.ToolCalls.ShouldBe([GetPositionsTool]);
        result.Answer.ShouldContain("couldn't find");
        realisedPnl.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Explain_AllTimeQuestionWithNoOpenLots_ReturnsTotalAndNoLotsAnswer()
    {
        using var source = new CancellationTokenSource();
        var positions = new Mock<IPositionQueryService>();
        positions.Setup(instance => instance.GetPositionsAsync(source.Token))
            .ReturnsAsync([new PositionResult(Symbol, 0m, null, -25m)]);
        positions.Setup(instance => instance.GetOpenLotsAsync(Symbol, source.Token))
            .ReturnsAsync([]);
        var realisedPnl = new Mock<IRealisedPnlRepository>();
        realisedPnl.Setup(instance => instance.GetTotalAsync(
                Symbol,
                null,
                null,
                source.Token))
            .ReturnsAsync(-25m);
        var client = CreateClient(positions, realisedPnl);

        var result = await client.ExplainAsync(
            "What's my realised P&L on AAPL?",
            source.Token);

        result.ToolCalls.ShouldBe([
            GetPositionsTool,
            "get_realised_pnl(\"AAPL\", \"all\")",
            GetLotsTool
        ]);
        result.Answer.ShouldBe(
            "Your realised P&L on AAPL in total is -£25.00. There are no open lots.");
        positions.VerifyAll();
        realisedPnl.VerifyAll();
    }

    private static InMemoryLlmClient CreateClient(
        Mock<IPositionQueryService> positions,
        Mock<IRealisedPnlRepository> realisedPnl) => new(
        positions.Object,
        realisedPnl.Object,
        CreateClock());

    private static TimeProvider CreateClock()
    {
        var clock = new Mock<TimeProvider>();
        clock.Setup(instance => instance.GetUtcNow()).Returns(Now);
        return clock.Object;
    }
}
