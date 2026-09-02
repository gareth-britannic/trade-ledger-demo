using TradeLedger.Application.Exceptions;
using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Messaging;
using TradeLedger.Application.Records;
using TradeLedger.Domain;
using TradeLedger.Processor.Handlers;
using TradeLedger.Processor.Validation;

namespace TradeLedger.Processor.Messages;

public sealed class FillMessageHandler(
    IFillLedgerUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    FillMessageValidator validator) : ISqsMessageHandler<FillRequestMessage>
{
    public async Task ProcessAsync(
        FillRequestMessage message,
        string? messageGroupId,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(message, messageGroupId, cancellationToken);

        var transactionStarted = false;
        try
        {
            await unitOfWork.BeginAsync(cancellationToken);
            transactionStarted = true;

            var request = await unitOfWork.FindRequestAsync(message.FillId, cancellationToken)
                ?? throw new ResourceNotFoundException($"Fill request '{message.FillId:D}' was not found.");
            await unitOfWork.AcquireSymbolLockAsync(request.Symbol, cancellationToken);

            request = await unitOfWork.FindRequestAsync(message.FillId, cancellationToken)
                ?? throw new ResourceNotFoundException($"Fill request '{message.FillId:D}' was not found.");
            if (request.ProcessedAt is not null)
            {
                await unitOfWork.CommitAsync(cancellationToken);
                transactionStarted = false;
                return;
            }

            var orderedRequests = await unitOfWork.GetOrderedRequestsAsync(request.Symbol, cancellationToken);
            var ledger = FifoMatcher.Replay(orderedRequests.Select(item => item.ToFill()).ToList());
            var pendingIds = orderedRequests
                .Where(item => item.ProcessedAt is null)
                .Select(item => item.Id)
                .ToList();

            await unitOfWork.ReplaceSymbolLedgerAsync(
                new Position(request.Symbol, ledger.OpenLots, ledger.RealisedPnl),
                ledger.RealisedPnlEntries,
                pendingIds,
                timeProvider.GetUtcNow().ToUniversalTime(),
                orderedRequests[^1],
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
            transactionStarted = false;
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
