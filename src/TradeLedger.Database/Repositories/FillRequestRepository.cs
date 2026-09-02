using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Records;
using TradeLedger.Database.Entities;

namespace TradeLedger.Database.Repositories;

internal sealed class FillRequestRepository(TradeLedgerDbContext dbContext) : IFillRequestRepository
{
    public async Task AddAsync(PendingFillRequest request, CancellationToken cancellationToken)
    {
        await dbContext.Fills.AddAsync(FillEntity.FromRequest(request), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
