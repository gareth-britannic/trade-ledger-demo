using TradeLedger.Application.Exceptions;
using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Records;
using TradeLedger.Domain;

namespace TradeLedger.Application.Services;

/// <summary>Applies a queued fill request to the ledger inside one serialized transaction.</summary>
public sealed class FillRequestProcessor(
    IFillLedgerUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IFillRequestProcessor
{
    public async Task<FillProcessingResult> ProcessAsync(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("A fill request ID must not be empty.", nameof(requestId));
        }

        var transactionStarted = false;
        try
        {
            await unitOfWork.BeginAsync(cancellationToken);
            transactionStarted = true;

            var request = await unitOfWork.FindRequestAsync(requestId, cancellationToken)
                ?? throw new ResourceNotFoundException($"Fill request '{requestId:D}' was not found.");
            await unitOfWork.AcquireSymbolLockAsync(request.Symbol, cancellationToken);

            request = await unitOfWork.FindRequestAsync(requestId, cancellationToken)
                ?? throw new ResourceNotFoundException($"Fill request '{requestId:D}' was not found.");
            if (request.ProcessedAt is { } originalProcessedAt)
            {
                await unitOfWork.CommitAsync(cancellationToken);
                transactionStarted = false;
                return new FillProcessingResult(
                    FillProcessingOutcome.AlreadyProcessed,
                    request.Symbol,
                    0,
                    false,
                    originalProcessedAt);
            }

            var orderedRequests = await unitOfWork.GetOrderedRequestsAsync(request.Symbol, cancellationToken);
            var ledger = FifoMatcher.Replay(orderedRequests.Select(item => item.ToFill()).ToList());
            var pendingIds = orderedRequests
                .Where(item => item.ProcessedAt is null)
                .Select(item => item.Id)
                .ToList();
            var processedAt = timeProvider.GetUtcNow().ToUniversalTime();

            await unitOfWork.ReplaceSymbolLedgerAsync(
                new Position(request.Symbol, ledger.OpenLots, ledger.RealisedPnl),
                ledger.RealisedPnlEntries,
                pendingIds,
                processedAt,
                orderedRequests[^1],
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
            transactionStarted = false;

            return new FillProcessingResult(
                FillProcessingOutcome.Applied,
                request.Symbol,
                pendingIds.Count,
                true);
        }
        catch
        {
            if (transactionStarted)
            {
                await unitOfWork.RollbackAsync(CancellationToken.None);
            }

            throw;
        }
    }
}
