using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using TradeLedger.Application.Messaging;
using TradeLedger.Application.Services;
using TradeLedger.Common;
using Xunit;

namespace TradeLedger.UnitTests.Common;

public sealed class SqsClientTests
{
    [Fact]
    public async Task Send_MissingCorrelationId_Throws()
    {
        var client = new SqsClient(
            Mock.Of<IAmazonSQS>(),
            Options.Create(new SqsQueueOptions { Url = "https://sqs.example.test/fills.fifo" }),
            new CorrelationIdProvider());

        await Should.ThrowAsync<InvalidOperationException>(() => client.SendAsync(
            new { Value = 1 }, "ACME", "deduplication-id", CancellationToken.None));
    }

    [Fact]
    public async Task Send_SerializesAnyTypedMessageAndSetsFifoMetadata()
    {
        using var source = new CancellationTokenSource();
        var fillId = Guid.NewGuid();
        var message = new FillRequestMessage(
            fillId, "ACME", "Buy", 10m, 12.34m, DateTimeOffset.UtcNow);
        SendMessageRequest? captured = null;
        var sqs = new Mock<IAmazonSQS>();
        sqs.Setup(instance => instance.SendMessageAsync(It.IsAny<SendMessageRequest>(), source.Token))
            .Callback<SendMessageRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new SendMessageResponse());
        var client = new SqsClient(
            sqs.Object,
            Options.Create(new SqsQueueOptions { Url = "https://sqs.example.test/fills.fifo" }),
            new CorrelationIdProvider { CorrelationId = "correlation-123" });

        await client.SendAsync(message, "ACME", fillId.ToString("D"), source.Token);

        captured.ShouldNotBeNull();
        captured.QueueUrl.ShouldBe("https://sqs.example.test/fills.fifo");
        captured.MessageDeduplicationId.ShouldBe(fillId.ToString("D"));
        captured.MessageGroupId.ShouldBe("ACME");
        captured.MessageAttributes["CorrelationId"].StringValue.ShouldBe("correlation-123");
        using var body = JsonDocument.Parse(captured.MessageBody);
        body.RootElement.GetProperty("fillId").GetGuid().ShouldBe(fillId);
        sqs.VerifyAll();
    }
}
