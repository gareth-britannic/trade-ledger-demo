using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Records;
using TradeLedger.Infrastructure.Database.Entities;

namespace TradeLedger.Infrastructure.Database.Repositories;

internal sealed class FillRepository(TradeLedgerDbContext dbContext) : IFillRepository
{
    public async Task AddAsync(Fill fill, CancellationToken cancellationToken)
    {
        await dbContext.Fills.AddAsync(FillEntity.FromModel(fill), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
