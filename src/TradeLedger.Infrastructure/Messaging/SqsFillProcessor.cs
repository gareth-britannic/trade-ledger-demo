using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Options;
using TradeLedger.Infrastructure.Messaging.Options;
using TradeLedger.Infrastructure.Messaging.Records;

namespace TradeLedger.Infrastructure.Messaging;

internal sealed class SqsFillProcessor(
    IAmazonSQS sqs,
    IOptions<FillQueueOptions> queueOptions,
    IOptions<FillProcessingOptions> processingOptions,
    IServiceScopeFactory scopeFactory,
    ILogger<SqsFillProcessor> logger) : BackgroundService
{
    private const int LongPollSeconds = 20;
    private const int MaximumMessages = 10;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!processingOptions.Value.Enabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var response = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = queueOptions.Value.Url,
                    MaxNumberOfMessages = MaximumMessages,
                    WaitTimeSeconds = LongPollSeconds
                }, stoppingToken);

                foreach (var message in response.Messages)
                {
                    await ProcessMessageAsync(message, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to receive fill messages");
                await Task.Delay(RetryDelay, stoppingToken);
            }
        }
    }

    internal async Task ProcessMessageAsync(Message message, CancellationToken cancellationToken)
    {
        try
        {
            var fill = JsonSerializer.Deserialize<FillMessage>(message.Body, SerializerOptions)
                ?? throw new JsonException("The fill message body is empty.");
            if (fill.FillId == Guid.Empty)
            {
                throw new JsonException("The fill message ID is invalid.");
            }

            await using var scope = scopeFactory.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<IProcessFillService>();
            await processor.ProcessAsync(fill.FillId, cancellationToken);
            await sqs.DeleteMessageAsync(queueOptions.Value.Url, message.ReceiptHandle, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to process fill message {MessageId}", message.MessageId);
        }
    }
}
