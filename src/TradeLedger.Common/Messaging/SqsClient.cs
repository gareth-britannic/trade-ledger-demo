using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Options;

namespace TradeLedger.Common;

public sealed class SqsClient(
    IAmazonSQS sqs,
    IOptions<SqsQueueOptions> queueOptions,
    ICorrelationIdProvider correlationIdProvider) : ISqsClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task SendAsync<TMessage>(
        TMessage message,
        string messageGroupId,
        string deduplicationId,
        CancellationToken cancellationToken)
        where TMessage : notnull
    {
        var correlationId = correlationIdProvider.CorrelationId
            ?? throw new InvalidOperationException("A correlation ID is required before publishing a message.");

        await sqs.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = queueOptions.Value.Url,
            MessageBody = JsonSerializer.Serialize(message, SerializerOptions),
            MessageGroupId = messageGroupId,
            MessageDeduplicationId = deduplicationId,
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                [CorrelationIdMetadata.PropertyName] = new()
                {
                    DataType = "String",
                    StringValue = correlationId
                }
            }
        }, cancellationToken);
    }
}
