using TradeLedger.Application.Records;

namespace TradeLedger.Application.Interfaces;

public interface IFillRequestRepository
{
    Task AddAsync(PendingFillRequest request, CancellationToken cancellationToken);
}
