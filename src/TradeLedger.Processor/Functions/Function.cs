using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using TradeLedger.Application.Messaging;
using TradeLedger.Processor.Handlers;

namespace TradeLedger.Processor;

/// <summary>Delegates the Lambda event to the typed SQS message handler.</summary>
public sealed class Function(SqsMessageHandler<FillRequestMessage> messageHandler)
{
    public Task<SQSBatchResponse> FunctionHandler(SQSEvent sqsEvent, ILambdaContext context) =>
        messageHandler.HandleAsync(sqsEvent, context);
}
