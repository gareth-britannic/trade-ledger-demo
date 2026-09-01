using FluentValidation;
using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Records;

namespace TradeLedger.Application.Services;

public sealed class CreateFillService(
    IFillRepository fillRepository,
    IFillPublisher fillPublisher,
    IValidator<CreateFillCommand> validator) : ICreateFillService
{
    public async Task<CreateFillResult> CreateAsync(
        CreateFillCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var fill = Fill.Create(
            command.FillId ?? Guid.NewGuid(),
            command.Symbol,
            command.Side,
            command.Quantity,
            command.Price,
            command.ExecutedAt);

        // Persistence intentionally precedes publication. A transactional outbox is required
        // to close the remaining database/SQS dual-write window.
        await fillRepository.AddAsync(fill, cancellationToken);
        await fillPublisher.PublishAsync(fill, cancellationToken);

        return new CreateFillResult(fill.Id);
    }
}
