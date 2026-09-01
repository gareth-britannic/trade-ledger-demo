using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Options;
using TradeLedger.Application;
using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Records;
using TradeLedger.Application.Services;
using TradeLedger.Infrastructure.Messaging.Options;
using TradeLedger.Infrastructure.Messaging.Records;

namespace TradeLedger.Infrastructure.Messaging;

internal sealed class SqsFillPublisher(
    IAmazonSQS sqs,
    IOptions<FillQueueOptions> queueOptions,
    ICorrelationIdProvider correlationIdProvider) : IFillPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task PublishAsync(Fill fill, CancellationToken cancellationToken)
    {
        var correlationId = correlationIdProvider.CorrelationId
            ?? throw new InvalidOperationException("A correlation ID is required before publishing a fill.");
        var message = new FillMessage(
            fill.Id,
            fill.Symbol,
            fill.Side.ToString(),
            fill.Quantity,
            fill.Price,
            fill.ExecutedAt);

        var request = new SendMessageRequest
        {
            QueueUrl = queueOptions.Value.Url,
            MessageBody = JsonSerializer.Serialize(message, SerializerOptions),
            MessageDeduplicationId = fill.Id.ToString("D"),
            MessageGroupId = fill.Symbol,
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                [CorrelationIdMetadata.PropertyName] = new() { DataType = "String", StringValue = correlationId }
            }
        };

        await sqs.SendMessageAsync(request, cancellationToken);
    }
}
