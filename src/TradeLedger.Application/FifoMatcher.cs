using TradeLedger.Application.Records;

namespace TradeLedger.Application;

public static class FifoMatcher
{
    public static MatchResult ApplySell(IReadOnlyList<Lot> openLots, Fill sell)
    {
        ArgumentNullException.ThrowIfNull(openLots);
        ValidateFill(sell, Side.Sell);

        if (openLots.Any(lot => lot.Symbol != sell.Symbol))
        {
            throw new ArgumentException("All open lots must have the same symbol as the sell fill.", nameof(openLots));
        }

        if (openLots.Any(lot => lot.RemainingQuantity <= 0))
        {
            throw new ArgumentException("Open lots must have a positive remaining quantity.", nameof(openLots));
        }

        if (openLots.Sum(lot => lot.RemainingQuantity) < sell.Quantity)
        {
            throw new InvalidOperationException("A sell cannot exceed the open quantity for a long-only position.");
        }

        var quantityToMatch = sell.Quantity;
        var realisedPnl = 0m;
        var remainingLots = new List<Lot>(openLots.Count);

        foreach (var lot in openLots)
        {
            if (quantityToMatch == 0)
            {
                remainingLots.Add(lot);
                continue;
            }

            var matchedQuantity = Math.Min(lot.RemainingQuantity, quantityToMatch);
            realisedPnl += matchedQuantity * (sell.Price - lot.UnitCost);
            quantityToMatch -= matchedQuantity;

            if (matchedQuantity < lot.RemainingQuantity)
            {
                remainingLots.Add(lot with
                {
                    RemainingQuantity = lot.RemainingQuantity - matchedQuantity
                });
            }
        }

        return new MatchResult(remainingLots, realisedPnl);
    }

    public static IReadOnlyList<Lot> ApplyBuy(IReadOnlyList<Lot> openLots, Fill buy)
    {
        ArgumentNullException.ThrowIfNull(openLots);
        ValidateFill(buy, Side.Buy);

        if (openLots.Any(lot => lot.Symbol != buy.Symbol))
        {
            throw new ArgumentException("All open lots must have the same symbol as the buy fill.", nameof(openLots));
        }

        return
        [
            ..openLots,
            new Lot(buy.Id, buy.Symbol, buy.Quantity, buy.Price, buy.ExecutedAt)
        ];
    }

    private static void ValidateFill(Fill fill, Side expectedSide)
    {
        ArgumentNullException.ThrowIfNull(fill);

        if (fill.Side != expectedSide)
        {
            throw new ArgumentException($"Expected a {expectedSide} fill.", nameof(fill));
        }

        if (string.IsNullOrWhiteSpace(fill.Symbol))
        {
            throw new ArgumentException("A fill must have a symbol.", nameof(fill));
        }

        if (fill.Quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fill), "A fill quantity must be positive.");
        }

        if (fill.Price < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fill), "A fill price cannot be negative.");
        }
    }
}
