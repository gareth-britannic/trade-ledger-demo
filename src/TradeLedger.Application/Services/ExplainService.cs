using System.Globalization;
using FluentValidation;
using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Records;

namespace TradeLedger.Application.Services;

public sealed class ExplainService(
    IPositionQueryService positionQueryService,
    IRealisedPnlRepository realisedPnlRepository,
    IValidator<ExplainQuery> validator,
    TimeProvider timeProvider) : IExplainService
{
    public async Task<ExplainResult> ExplainAsync(
        ExplainQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        await validator.ValidateAndThrowAsync(query, cancellationToken);

        var toolCalls = new List<string> { "get_positions()" };
        var positions = await positionQueryService.GetPositionsAsync(cancellationToken);
        var symbol = positions
            .Select(position => position.Symbol)
            .FirstOrDefault(candidate => query.Question.Contains(candidate, StringComparison.OrdinalIgnoreCase));

        if (symbol is null)
        {
            return new ExplainResult(toolCalls, "I couldn't find a position symbol in that question.");
        }

        var period = GetPeriod(query.Question, timeProvider.GetUtcNow());
        toolCalls.Add($"get_realised_pnl(\"{symbol}\", \"{period.Name}\")");
        var realisedPnl = await realisedPnlRepository.GetTotalAsync(
            symbol,
            period.FromInclusive,
            period.ToExclusive,
            cancellationToken);

        toolCalls.Add($"get_lots(\"{symbol}\")");
        var lots = await positionQueryService.GetOpenLotsAsync(symbol, cancellationToken);
        var lotSummary = lots.Count == 0
            ? "There are no open lots."
            : $"There are {lots.Count} open lots, ordered oldest first for FIFO.";

        return new ExplainResult(
            toolCalls,
            $"Your realised P&L on {symbol} {period.Description} is {FormatMoney(realisedPnl)}. {lotSummary}");
    }

    private static ExplainPeriod GetPeriod(string question, DateTimeOffset now)
    {
        if (!question.Contains("month", StringComparison.OrdinalIgnoreCase))
        {
            return new ExplainPeriod("all", "in total", null, null);
        }

        var start = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        return new ExplainPeriod("month", "this month", start, start.AddMonths(1));
    }

    private static string FormatMoney(decimal value) => value.ToString(
        "+£#,##0.00;-£#,##0.00;£0.00",
        CultureInfo.InvariantCulture);

    private sealed record ExplainPeriod(
        string Name,
        string Description,
        DateTimeOffset? FromInclusive,
        DateTimeOffset? ToExclusive);
}
