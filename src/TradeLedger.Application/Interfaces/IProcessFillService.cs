namespace TradeLedger.Application.Interfaces;

public interface IProcessFillService
{
    Task<bool> ProcessAsync(Guid fillId, CancellationToken cancellationToken);
}
