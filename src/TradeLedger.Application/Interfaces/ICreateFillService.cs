using TradeLedger.Application.Records;

namespace TradeLedger.Application.Interfaces;

public interface ICreateFillService
{
    Task<CreateFillResult> CreateAsync(CreateFillCommand command, CancellationToken cancellationToken);
}
