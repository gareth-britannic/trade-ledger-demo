using TradeLedger.Application.Records;

namespace TradeLedger.Application.Interfaces;

public interface IFillPublisher
{
    Task PublishAsync(Fill fill, CancellationToken cancellationToken);
}
