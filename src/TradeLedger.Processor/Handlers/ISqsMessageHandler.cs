namespace TradeLedger.Processor.Handlers;

public interface ISqsMessageHandler<in TMessage>
{
    Task ProcessAsync(
        TMessage message,
        string? messageGroupId,
        CancellationToken cancellationToken);
}
