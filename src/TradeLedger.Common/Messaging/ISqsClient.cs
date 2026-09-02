namespace TradeLedger.Common;

public interface ISqsClient
{
    Task SendAsync<TMessage>(
        TMessage message,
        string messageGroupId,
        string deduplicationId,
        CancellationToken cancellationToken)
        where TMessage : notnull;
}
