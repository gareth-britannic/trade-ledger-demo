using Moq;
using Shouldly;
using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Messaging;
using TradeLedger.Application.Records;
using TradeLedger.Application.Services;
using TradeLedger.Application.Validators;
using TradeLedger.Common;
using TradeLedger.Domain;
using Xunit;

namespace TradeLedger.UnitTests.Application.Services;

public sealed class CreateFillServiceTests
{
    [Fact]
    public async Task Create_PersistsPendingRequestThenSendsTypedMessage()
    {
        var fillId = Guid.NewGuid();
        using var source = new CancellationTokenSource();
        var sequence = new MockSequence();
        PendingFillRequest? persisted = null;
        FillRequestMessage? sent = null;
        var repository = new Mock<IFillRequestRepository>(MockBehavior.Strict);
        var queue = new Mock<ISqsClient>(MockBehavior.Strict);
        repository.InSequence(sequence)
            .Setup(instance => instance.AddAsync(It.IsAny<PendingFillRequest>(), source.Token))
            .Callback<PendingFillRequest, CancellationToken>((request, _) => persisted = request)
            .Returns(Task.CompletedTask);
        queue.InSequence(sequence)
            .Setup(instance => instance.SendAsync(
                It.IsAny<FillRequestMessage>(), "ACME", fillId.ToString("D"), source.Token))
            .Callback<FillRequestMessage, string, string, CancellationToken>(
                (message, _, _, _) => sent = message)
            .Returns(Task.CompletedTask);
        var service = new CreateFillService(repository.Object, queue.Object, new CreateFillCommandValidator());

        var result = await service.CreateAsync(Command(fillId, " acme "), source.Token);

        result.FillId.ShouldBe(fillId);
        persisted.ShouldNotBeNull();
        persisted.Symbol.ShouldBe("ACME");
        persisted.ProcessedAt.ShouldBeNull();
        sent.ShouldNotBeNull().FillId.ShouldBe(persisted.Id);
        repository.VerifyAll();
        queue.VerifyAll();
    }

    [Fact]
    public async Task Create_WhenPersistenceFails_DoesNotSendMessage()
    {
        var repository = new Mock<IFillRequestRepository>();
        var queue = new Mock<ISqsClient>();
        repository.Setup(instance => instance.AddAsync(
                It.IsAny<PendingFillRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));
        var service = new CreateFillService(repository.Object, queue.Object, new CreateFillCommandValidator());

        await Should.ThrowAsync<InvalidOperationException>(() =>
            service.CreateAsync(Command(Guid.NewGuid(), "ACME"), CancellationToken.None));

        queue.Verify(instance => instance.SendAsync(
            It.IsAny<FillRequestMessage>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_WhenQueueFails_PropagatesFailure()
    {
        var repository = new Mock<IFillRequestRepository>();
        var queue = new Mock<ISqsClient>();
        repository.Setup(instance => instance.AddAsync(
                It.IsAny<PendingFillRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        queue.Setup(instance => instance.SendAsync(
                It.IsAny<FillRequestMessage>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("queue unavailable"));
        var service = new CreateFillService(repository.Object, queue.Object, new CreateFillCommandValidator());

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
