using TradeLedger.Application.Records;

namespace TradeLedger.Application.Interfaces;

public interface IFillRequestService
{
    Task<CreateFillResult> CreateAsync(CreateFillCommand command, CancellationToken cancellationToken);
}
