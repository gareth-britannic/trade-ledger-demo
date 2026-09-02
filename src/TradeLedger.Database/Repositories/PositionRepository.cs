using Microsoft.EntityFrameworkCore;
using TradeLedger.Application.Interfaces;
using TradeLedger.Database.Entities;
using TradeLedger.Domain;

namespace TradeLedger.Database.Repositories;

internal sealed class PositionRepository(TradeLedgerDbContext dbContext) : IPositionRepository
{
    public async Task<IReadOnlyList<Position>> GetPositionsAsync(CancellationToken cancellationToken)
    {
        var lots = await dbContext.Lots
            .AsNoTracking()
            .Where(lot => lot.RemainingQuantity > 0)
            .OrderBy(lot => lot.Symbol)
            .ThenBy(lot => lot.OpenedAt)
            .ThenBy(lot => lot.Id)
            .ToListAsync(cancellationToken);
        var positions = await dbContext.Positions
            .AsNoTracking()
            .OrderBy(position => position.Symbol)
            .ToListAsync(cancellationToken);

        return positions.Select(position => position.ToModel(
                lots.Where(lot => lot.Symbol == position.Symbol)
                    .Select(lot => lot.ToModel())
                    .ToList()))
            .ToList();
    }

    public async Task<Position?> GetPositionAsync(string symbol, CancellationToken cancellationToken)
    {
        var lots = await dbContext.Lots
            .AsNoTracking()
            .Where(lot => lot.Symbol == symbol && lot.RemainingQuantity > 0)
            .OrderBy(lot => lot.OpenedAt)
            .ThenBy(lot => lot.Id)
            .ToListAsync(cancellationToken);
        var position = await dbContext.Positions
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Symbol == symbol, cancellationToken);

        return position?.ToModel(lots.Select(lot => lot.ToModel()).ToList());
    }
}
