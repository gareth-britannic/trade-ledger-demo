using Moq;
using Shouldly;
using TradeLedger.Application.Messaging;
using TradeLedger.Application.Records;
using TradeLedger.Application.Services;
using TradeLedger.Application.Validators;
using TradeLedger.Common;
using TradeLedger.Domain;
using Xunit;

namespace TradeLedger.UnitTests.Application.Services;

public sealed class FillRequestServiceTests
{
    [Fact]
    public async Task Create_SendsTypedMessage()
    {
        var fillId = Guid.NewGuid();
        using var source = new CancellationTokenSource();
        FillRequestMessage? sent = null;
        var queue = new Mock<ISqsClient>(MockBehavior.Strict);
        queue.Setup(instance => instance.SendAsync(
                It.IsAny<FillRequestMessage>(), "ACME", fillId.ToString("D"), source.Token))
            .Callback<FillRequestMessage, string, string, CancellationToken>(
                (message, _, _, _) => sent = message)
            .Returns(Task.CompletedTask);
        var service = new FillRequestService(queue.Object, new CreateFillCommandValidator());

        var result = await service.CreateAsync(Command(fillId, " acme "), source.Token);

        result.FillId.ShouldBe(fillId);
        sent.ShouldNotBeNull().FillId.ShouldBe(fillId);
        sent.Symbol.ShouldBe("ACME");
        queue.VerifyAll();
    }

    [Fact]
    public async Task Create_WhenQueueFails_PropagatesFailure()
    {
        var queue = new Mock<ISqsClient>();
        queue.Setup(instance => instance.SendAsync(
                It.IsAny<FillRequestMessage>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("queue unavailable"));
        var service = new FillRequestService(queue.Object, new CreateFillCommandValidator());

        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            service.CreateAsync(Command(Guid.NewGuid(), "ACME"), CancellationToken.None));

        exception.Message.ShouldBe("queue unavailable");
    }

    private static CreateFillCommand Command(Guid id, string symbol) => new(
        id,
        symbol,
        Side.Buy,
        10m,
        12m,
        new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.FromHours(7)));
}
