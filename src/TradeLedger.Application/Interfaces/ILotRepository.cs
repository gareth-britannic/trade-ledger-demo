using TradeLedger.Application.Records;

namespace TradeLedger.Application.Interfaces;

public interface ILotRepository
{
    Task<IReadOnlyList<Position>> GetPositionsAsync(CancellationToken cancellationToken);

    Task<Position?> GetPositionAsync(string symbol, CancellationToken cancellationToken);
}
