using System.Diagnostics;
using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace TradeLedger.Processor.Actions;

/// <summary>Wraps a typed SQS action with the common Lambda concerns.</summary>
public sealed class ActionFunction<TMessage>(
    IServiceScopeFactory scopeFactory,
    ILogger<ActionFunction<TMessage>> logger)
    where TMessage : class
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = Serialization.JsonUnmappedMemberHandling.Disallow
    };

    public async Task<SQSBatchResponse> PerformAsync<TAction, TResult>(
        SQSEvent sqsEvent,
        ILambdaContext context,
        Func<TAction, TMessage, string?, CancellationToken, Task<TResult>> action)
        where TAction : notnull
    {
        ArgumentNullException.ThrowIfNull(context);

        var timeout = context.RemainingTime > TimeSpan.FromSeconds(1)
            ? context.RemainingTime - TimeSpan.FromSeconds(1)
            : context.RemainingTime;

        using var timeoutSource = new CancellationTokenSource(timeout);

        return await PerformAsync(sqsEvent, context, action, timeoutSource.Token);
    }

    public async Task<SQSBatchResponse> PerformAsync<TAction, TResult>(
        SQSEvent sqsEvent,
        ILambdaContext context,
        Func<TAction, TMessage, string?, CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken)
        where TAction : notnull
    {
        ArgumentNullException.ThrowIfNull(sqsEvent);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(action);

        var stopwatch = Stopwatch.StartNew();
        using var requestScope = LogContext.PushProperty("LambdaRequestId", context.AwsRequestId);
        logger.LogInformation("Lambda action started");

        try
        {
            var response = await PerformBatchAsync(sqsEvent, action, cancellationToken);
            logger.LogInformation(
                "Lambda action completed; DurationMs: {DurationMs}",
                stopwatch.Elapsed.TotalMilliseconds);
            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Lambda action cancelled; DurationMs: {DurationMs}",
                stopwatch.Elapsed.TotalMilliseconds);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Lambda action failed; DurationMs: {DurationMs}",
                stopwatch.Elapsed.TotalMilliseconds);
            throw;
        }
    }

    private async Task<SQSBatchResponse> PerformBatchAsync<TAction, TResult>(
        SQSEvent sqsEvent,
        Func<TAction, TMessage, string?, CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken)
        where TAction : notnull
    {
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

            if (await PerformRecordAsync(record, messageId, messageGroupId, action, cancellationToken))
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

    private async Task<bool> PerformRecordAsync<TAction, TResult>(
        SQSEvent.SQSMessage record,
        string messageId,
        string? messageGroupId,
        Func<TAction, TMessage, string?, CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken)
        where TAction : notnull
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
            var actionService = scope.ServiceProvider.GetRequiredService<TAction>();

            logger.LogInformation("SQS record processing started");
            var result = await action(actionService, message, messageGroupId, cancellationToken);
            logger.LogInformation(
                "SQS record processing completed; Result: {@Result}; DurationMs: {DurationMs}",
                result,
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
