using System.Diagnostics;
using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using TradeLedger.Common;

namespace TradeLedger.Processor.Handlers;

/// <summary>Handles the common Lambda concerns before processing a typed SQS message.</summary>
public sealed class SqsMessageHandler<TMessage>(
    IServiceScopeFactory scopeFactory,
    ILogger<SqsMessageHandler<TMessage>> logger)
    where TMessage : class
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };

    public async Task<SQSBatchResponse> HandleAsync(
        SQSEvent sqsEvent,
        ILambdaContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var timeout = context.RemainingTime > TimeSpan.FromSeconds(1)
            ? context.RemainingTime - TimeSpan.FromSeconds(1)
            : context.RemainingTime;
        using var timeoutSource = new CancellationTokenSource(timeout);
        return await HandleAsync(sqsEvent, context, timeoutSource.Token);
    }

    public async Task<SQSBatchResponse> HandleAsync(
        SQSEvent sqsEvent,
        ILambdaContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sqsEvent);
        ArgumentNullException.ThrowIfNull(context);

        var stopwatch = Stopwatch.StartNew();
        using var requestScope = LogContext.PushProperty("LambdaRequestId", context.AwsRequestId);
        logger.LogInformation("SQS message handler started");

        try
        {
            var response = await HandleBatchAsync(sqsEvent, cancellationToken);
            logger.LogInformation(
                "SQS message handler completed; DurationMs: {DurationMs}",
                stopwatch.Elapsed.TotalMilliseconds);
            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "SQS message handler cancelled; DurationMs: {DurationMs}",
                stopwatch.Elapsed.TotalMilliseconds);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "SQS message handler failed; DurationMs: {DurationMs}",
                stopwatch.Elapsed.TotalMilliseconds);
            throw;
        }
    }

    private async Task<SQSBatchResponse> HandleBatchAsync(
        SQSEvent sqsEvent,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var failures = new List<SQSBatchResponse.BatchItemFailure>();
        var blockedGroups = new HashSet<string>(StringComparer.Ordinal);
        var successCount = 0;

        foreach (var record in sqsEvent.Records ?? [])
        {
            var messageId = string.IsNullOrWhiteSpace(record.MessageId)
                ? Guid.NewGuid().ToString("N")
                : record.MessageId;
            var messageGroupId = GetMessageGroupId(record);
            var groupKey = messageGroupId ?? $"__missing__:{messageId}";

            if (blockedGroups.Contains(groupKey))
            {
                failures.Add(Failure(messageId));
                logger.LogWarning(
                    "SQS record left unprocessed after an earlier failure in message group " +
                    "{MessageGroupId}; MessageId: {MessageId}",
                    messageGroupId,
                    messageId);
                continue;
            }

            if (await HandleRecordAsync(record, messageId, messageGroupId, cancellationToken))
            {
                successCount++;
                continue;
            }

            failures.Add(Failure(messageId));
            blockedGroups.Add(groupKey);
        }

        logger.LogInformation(
            "SQS batch completed; SuccessCount: {SuccessCount}; FailureCount: {FailureCount}",
            successCount,
            failures.Count);
        return new SQSBatchResponse { BatchItemFailures = failures };
    }

    private async Task<bool> HandleRecordAsync(
        SQSEvent.SQSMessage record,
        string messageId,
        string? messageGroupId,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var correlationId = CorrelationIdFactory.NormalizeOrCreate(GetCorrelationId(record));

        await using var scope = scopeFactory.CreateAsyncScope();
        var correlationIdProvider = scope.ServiceProvider.GetRequiredService<ICorrelationIdProvider>();
        correlationIdProvider.CorrelationId = correlationId;

        using var correlationScope = LogContext.PushProperty(CorrelationIdMetadata.PropertyName, correlationId);
        using var messageScope = LogContext.PushProperty("SqsMessageId", messageId);
        using var groupScope = LogContext.PushProperty("MessageGroupId", messageGroupId);

        try
        {
            var message = JsonSerializer.Deserialize<TMessage>(record.Body, SerializerOptions)
                ?? throw new JsonException("The SQS message body is empty.");
            var messageHandler = scope.ServiceProvider.GetRequiredService<ISqsMessageHandler<TMessage>>();

            logger.LogInformation("SQS record processing started");
            await messageHandler.ProcessAsync(message, messageGroupId, cancellationToken);
            logger.LogInformation(
                "SQS record processing completed; DurationMs: {DurationMs}",
                stopwatch.Elapsed.TotalMilliseconds);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "SQS record processing failed; DurationMs: {DurationMs}",
                stopwatch.Elapsed.TotalMilliseconds);
            return false;
        }
        finally
        {
            correlationIdProvider.CorrelationId = null;
        }
    }

    private static SQSBatchResponse.BatchItemFailure Failure(string messageId) =>
        new() { ItemIdentifier = messageId };

    private static string? GetCorrelationId(SQSEvent.SQSMessage record) =>
        record.MessageAttributes is not null &&
        record.MessageAttributes.TryGetValue(CorrelationIdMetadata.PropertyName, out var attribute)
            ? attribute.StringValue
            : null;

    private static string? GetMessageGroupId(SQSEvent.SQSMessage record) =>
        record.Attributes is not null && record.Attributes.TryGetValue("MessageGroupId", out var messageGroupId)
            ? messageGroupId
            : null;
}
