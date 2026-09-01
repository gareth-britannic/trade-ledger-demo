using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using TradeLedger.Application;
using TradeLedger.Application.Records;
using TradeLedger.Application.Services;
using TradeLedger.Infrastructure.Messaging;
using TradeLedger.Infrastructure.Messaging.Options;
using Xunit;

namespace TradeLedger.UnitTests.Infrastructure.Messaging;

public sealed class SqsFillPublisherTests
{
    [Fact]
    public async Task Publish_MissingCorrelationId_ThrowsInvalidOperationException()
    {
        var publisher = new SqsFillPublisher(
            Mock.Of<IAmazonSQS>(),
            Options.Create(new FillQueueOptions { Url = "https://sqs.example.test/fills.fifo" }),
            new CorrelationIdProvider());
        var fill = Fill.Create(Guid.NewGuid(), "ACME", Side.Buy, 10m, 12m, DateTimeOffset.UtcNow);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            publisher.PublishAsync(fill, CancellationToken.None));
    }

    [Fact]
    public async Task Publish_UsesStableContractFifoIdentifiersCorrelationAndCancellationToken()
    {
        using var source = new CancellationTokenSource();
        var fill = Fill.Create(Guid.NewGuid(), " acme ", Side.Buy, 10m, 12.34m, DateTimeOffset.UtcNow);
        var provider = new CorrelationIdProvider { CorrelationId = "correlation-123" };
        SendMessageRequest? captured = null;
        var sqs = new Mock<IAmazonSQS>();
        sqs.Setup(instance => instance.SendMessageAsync(It.IsAny<SendMessageRequest>(), source.Token))
            .Callback<SendMessageRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new SendMessageResponse());
        var publisher = new SqsFillPublisher(
            sqs.Object,
            Options.Create(new FillQueueOptions { Url = "https://sqs.example.test/fills.fifo" }),
            provider);

        await publisher.PublishAsync(fill, source.Token);

        captured.ShouldNotBeNull();
        captured.QueueUrl.ShouldBe("https://sqs.example.test/fills.fifo");
        captured.MessageDeduplicationId.ShouldBe(fill.Id.ToString("D"));
        captured.MessageGroupId.ShouldBe("ACME");
        captured.MessageAttributes["CorrelationId"].StringValue.ShouldBe("correlation-123");
        using var body = JsonDocument.Parse(captured.MessageBody);
        body.RootElement.GetProperty("fillId").GetGuid().ShouldBe(fill.Id);
        body.RootElement.GetProperty("symbol").GetString().ShouldBe("ACME");
        body.RootElement.GetProperty("side").GetString().ShouldBe("Buy");
        body.RootElement.GetProperty("quantity").GetDecimal().ShouldBe(10m);
        body.RootElement.GetProperty("price").GetDecimal().ShouldBe(12.34m);
        body.RootElement.TryGetProperty("executedAt", out _).ShouldBeTrue();
        sqs.VerifyAll();
    }
}
