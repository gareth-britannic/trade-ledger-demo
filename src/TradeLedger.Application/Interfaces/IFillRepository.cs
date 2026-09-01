using TradeLedger.Application.Records;

namespace TradeLedger.Application.Interfaces;

public interface IFillRepository
{
    Task AddAsync(Fill fill, CancellationToken cancellationToken);
}
