using TradeLedger.Application.Records;

namespace TradeLedger.Application.Interfaces;

public interface IPositionQueryService
{
    Task<IReadOnlyList<PositionResult>> GetPositionsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<LotResult>> GetOpenLotsAsync(string symbol, CancellationToken cancellationToken);
}
