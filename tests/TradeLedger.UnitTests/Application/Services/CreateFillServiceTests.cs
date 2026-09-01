using Moq;
using Shouldly;
using TradeLedger.Application;
using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Records;
using TradeLedger.Application.Services;
using TradeLedger.Application.Validators;
using Xunit;

namespace TradeLedger.UnitTests.Application.Services;

public sealed class CreateFillServiceTests
{
    [Fact]
    public async Task Create_PersistsBeforePublishing_WithSameFillAndCancellationToken()
    {
        var fillId = Guid.NewGuid();
        using var source = new CancellationTokenSource();
        var sequence = new MockSequence();
        Fill? persisted = null;
        Fill? published = null;
        var repository = new Mock<IFillRepository>(MockBehavior.Strict);
        var publisher = new Mock<IFillPublisher>(MockBehavior.Strict);
        repository.InSequence(sequence)
            .Setup(instance => instance.AddAsync(It.IsAny<Fill>(), source.Token))
            .Callback<Fill, CancellationToken>((fill, _) => persisted = fill)
            .Returns(Task.CompletedTask);
        publisher.InSequence(sequence)
            .Setup(instance => instance.PublishAsync(It.IsAny<Fill>(), source.Token))
            .Callback<Fill, CancellationToken>((fill, _) => published = fill)
            .Returns(Task.CompletedTask);
        var service = new CreateFillService(repository.Object, publisher.Object, new CreateFillCommandValidator());

        var result = await service.CreateAsync(Command(fillId, " acme "), source.Token);

        result.FillId.ShouldBe(fillId);
        persisted.ShouldNotBeNull();
        published.ShouldBeSameAs(persisted);
        persisted.Symbol.ShouldBe("ACME");
        repository.VerifyAll();
        publisher.VerifyAll();
    }

    [Fact]
    public async Task Create_WhenRepositoryFails_DoesNotPublish()
    {
        var repository = new Mock<IFillRepository>();
        var publisher = new Mock<IFillPublisher>();
        repository.Setup(instance => instance.AddAsync(It.IsAny<Fill>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));
        var service = new CreateFillService(repository.Object, publisher.Object, new CreateFillCommandValidator());

        await Should.ThrowAsync<InvalidOperationException>(() =>
            service.CreateAsync(Command(Guid.NewGuid(), "ACME"), CancellationToken.None));

        publisher.Verify(
            instance => instance.PublishAsync(It.IsAny<Fill>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Create_WhenPublishingFails_PropagatesFailure()
    {
        var repository = new Mock<IFillRepository>();
        var publisher = new Mock<IFillPublisher>();
        repository.Setup(instance => instance.AddAsync(It.IsAny<Fill>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        publisher.Setup(instance => instance.PublishAsync(It.IsAny<Fill>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("queue unavailable"));
        var service = new CreateFillService(repository.Object, publisher.Object, new CreateFillCommandValidator());

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
