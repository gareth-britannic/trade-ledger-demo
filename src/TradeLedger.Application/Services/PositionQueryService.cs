using FluentValidation;
using FluentValidation.Results;
using TradeLedger.Application.Exceptions;
using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Records;

namespace TradeLedger.Application.Services;

public sealed class PositionQueryService(ILotRepository lotRepository) : IPositionQueryService
{
    public async Task<IReadOnlyList<PositionResult>> GetPositionsAsync(CancellationToken cancellationToken)
    {
        var positions = await lotRepository.GetPositionsAsync(cancellationToken);
        return positions.Select(MapPosition).ToArray();
    }

    public async Task<IReadOnlyList<LotResult>> GetOpenLotsAsync(
        string symbol,
        CancellationToken cancellationToken)
    {
        if (!SymbolNormalizer.IsValid(symbol))
        {
            throw new ValidationException([
                new ValidationFailure(nameof(symbol), "A valid symbol is required.")
            ]);
        }

        var normalizedSymbol = SymbolNormalizer.Normalize(symbol);
        var position = await lotRepository.GetPositionAsync(normalizedSymbol, cancellationToken)
            ?? throw new ResourceNotFoundException($"Position '{normalizedSymbol}' was not found.");

        return position.OpenLots.Select(lot => new LotResult(
            lot.Id,
            lot.Symbol,
            lot.RemainingQuantity,
            lot.UnitCost,
            lot.OpenedAt)).ToArray();
    }

    private static PositionResult MapPosition(Position position)
    {
        var openQuantity = position.OpenLots.Sum(lot => lot.RemainingQuantity);
        decimal? averageUnitCost = openQuantity == 0
            ? null
            : position.OpenLots.Sum(lot => lot.RemainingQuantity * lot.UnitCost) / openQuantity;

        return new PositionResult(position.Symbol, openQuantity, averageUnitCost, position.RealisedPnl);
    }
}
