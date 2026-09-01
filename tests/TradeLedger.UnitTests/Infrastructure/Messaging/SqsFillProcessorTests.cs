using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Options;
using TradeLedger.Infrastructure.Messaging;
using TradeLedger.Infrastructure.Messaging.Options;
using TradeLedger.Infrastructure.Messaging.Records;
using Xunit;

namespace TradeLedger.UnitTests.Infrastructure.Messaging;

public sealed class SqsFillProcessorTests
{
    private const string QueueUrl = "https://sqs.example.test/fills.fifo";
    private const string ReceiptHandle = "receipt-handle";
    private const string MessageId = "message-id";

    [Fact]
    public async Task ProcessMessage_ValidContract_ProcessesFillThenDeletesMessage()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        var fillId = Guid.NewGuid();
        var processor = new Mock<IProcessFillService>();
        processor.Setup(instance => instance.ProcessAsync(fillId, source.Token)).ReturnsAsync(true);
        await using var services = Services(processor.Object);
        var sqs = new Mock<IAmazonSQS>();
        sqs.Setup(instance => instance.DeleteMessageAsync(QueueUrl, ReceiptHandle, source.Token))
            .ReturnsAsync(new DeleteMessageResponse());
        var worker = Worker(sqs.Object, services);
        var message = new Message
        {
            MessageId = MessageId,
            ReceiptHandle = ReceiptHandle,
            Body = JsonSerializer.Serialize(new FillMessage(
                fillId,
                "ACME",
                "Buy",
                10m,
                12m,
                new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero)))
        };

        // Act
        await worker.ProcessMessageAsync(message, source.Token);

        // Assert
        processor.VerifyAll();
        sqs.VerifyAll();
    }

    [Fact]
    public async Task ProcessMessage_InvalidContract_LeavesMessageForRetry()
    {
        // Arrange
        await using var services = Services(Mock.Of<IProcessFillService>());
        var sqs = new Mock<IAmazonSQS>();
        var worker = Worker(sqs.Object, services);
        var message = new Message
        {
            MessageId = MessageId,
            ReceiptHandle = ReceiptHandle,
            Body = "{}"
        };

        // Act
        await worker.ProcessMessageAsync(message, CancellationToken.None);

        // Assert
        sqs.Verify(instance => instance.DeleteMessageAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ServiceProvider Services(IProcessFillService processor)
    {
        var services = new ServiceCollection();
        services.AddSingleton(processor);
        return services.BuildServiceProvider();
    }

    private static SqsFillProcessor Worker(IAmazonSQS sqs, IServiceProvider services) => new(
        sqs,
        Options.Create(new FillQueueOptions { Url = QueueUrl }),
        Options.Create(new FillProcessingOptions { Enabled = true }),
        services.GetRequiredService<IServiceScopeFactory>(),
        NullLogger<SqsFillProcessor>.Instance);
}
