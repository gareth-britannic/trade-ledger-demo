using FluentValidation;
using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Messaging;
using TradeLedger.Application.Records;
using TradeLedger.Common;

namespace TradeLedger.Application.Services;

public sealed class CreateFillService(
    IFillRequestRepository requestRepository,
    ISqsClient sqsClient,
    IValidator<CreateFillCommand> validator) : ICreateFillService
{
    public async Task<CreateFillResult> CreateAsync(
        CreateFillCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var request = PendingFillRequest.Create(
            command.FillId ?? Guid.NewGuid(),
            command.Symbol,
            command.Side,
            command.Quantity,
            command.Price,
            command.ExecutedAt);

        await requestRepository.AddAsync(request, cancellationToken);
        await sqsClient.SendAsync(
            new FillRequestMessage(
                request.Id,
                request.Symbol,
                request.Side.ToString(),
                request.Quantity,
                request.Price,
                request.ExecutedAt),
            request.Symbol,
            request.Id.ToString("D"),
            cancellationToken);

        return new CreateFillResult(request.Id);
    }
}
