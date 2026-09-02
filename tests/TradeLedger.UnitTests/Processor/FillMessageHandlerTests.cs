using FluentValidation;
using Shouldly;
using TradeLedger.Application.Exceptions;
using TradeLedger.Application.Interfaces;
using TradeLedger.Application.Messaging;
using TradeLedger.Application.Records;
using TradeLedger.Domain;
using TradeLedger.Processor.Messages;
using TradeLedger.Processor.Validation;
using Xunit;

namespace TradeLedger.UnitTests.Processor;

public sealed class FillMessageHandlerTests
{
    private const string Symbol = "ACME";
    private static readonly DateTimeOffset Now =
        new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Buy_RebuildsLotAndPositionAndStampsPendingRequest()
    {
        var buy = Fill.Create(Guid.NewGuid(), Symbol, Side.Buy, 10m, 12m, Now.AddMinutes(-1));
        var unitOfWork = new FakeUnitOfWork([Request(buy)]);

        await Service(unitOfWork).ProcessAsync(Message(buy), Symbol, CancellationToken.None);

        unitOfWork.Position.ShouldNotBeNull().OpenLots.ShouldHaveSingleItem()
            .ShouldBe(new Lot(buy.Id, Symbol, 10m, 12m, buy.ExecutedAt));
        unitOfWork.NewlyProcessedIds.ShouldBe([buy.Id]);
        unitOfWork.ProcessedAt.ShouldBe(Now);
        unitOfWork.Saved.ShouldBeTrue();
        unitOfWork.Committed.ShouldBeTrue();
    }

    [Fact]
    public async Task Sell_UsesExecutionOrderedFifoAndCumulativeRealisedPnl()
    {
        var earlyBuy = Fill.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"),
            Symbol, Side.Buy, 100m, 20m, Now.AddMinutes(-3));
        var laterBuy = Fill.Create(Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Symbol, Side.Buy, 100m, 10m, Now.AddMinutes(-2));
        var sell = Fill.Create(Guid.NewGuid(), Symbol, Side.Sell, 100m, 30m, Now.AddMinutes(-1));
        var unitOfWork = new FakeUnitOfWork([Request(earlyBuy), Request(laterBuy), Request(sell)]);

        await Service(unitOfWork).ProcessAsync(Message(sell), Symbol, CancellationToken.None);

        var position = unitOfWork.Position.ShouldNotBeNull();
        position.OpenLots.ShouldBe([
            new Lot(laterBuy.Id, Symbol, 100m, 10m, laterBuy.ExecutedAt)
        ]);
        position.RealisedPnl.ShouldBe(1000m);
        unitOfWork.RealisedPnlEntries.ShouldHaveSingleItem().Amount.ShouldBe(1000m);
    }

    [Fact]
    public async Task IdenticalTimestamps_UsesRepositoryFillIdTieBreakerOrder()
    {
        var timestamp = Now.AddMinutes(-1);
        var first = Fill.Create(Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Symbol, Side.Buy, 1m, 10m, timestamp);
        var second = Fill.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"),
            Symbol, Side.Buy, 1m, 20m, timestamp);
        var unitOfWork = new FakeUnitOfWork([Request(first), Request(second)]);

        await Service(unitOfWork).ProcessAsync(Message(second), Symbol, CancellationToken.None);

        unitOfWork.Position.ShouldNotBeNull().OpenLots.Select(lot => lot.Id)
            .ShouldBe([first.Id, second.Id]);
        unitOfWork.Watermark.ShouldBe(Request(second));
    }

    [Fact]
    public async Task AlreadyProcessedRequest_IsNoOpAndPreservesTimestamp()
    {
        var originalTimestamp = Now.AddHours(-1);
        var fill = Fill.Create(Guid.NewGuid(), Symbol, Side.Buy, 10m, 12m, Now.AddDays(-1));
        var unitOfWork = new FakeUnitOfWork([Request(fill, originalTimestamp)]);

        await Service(unitOfWork).ProcessAsync(Message(fill), Symbol, CancellationToken.None);

        unitOfWork.Requests.ShouldHaveSingleItem().ProcessedAt.ShouldBe(originalTimestamp);
        unitOfWork.Position.ShouldBeNull();
        unitOfWork.Saved.ShouldBeFalse();
        unitOfWork.Committed.ShouldBeTrue();
    }

    [Fact]
    public async Task InvalidMessageGroup_IsRejectedBeforeStartingTransaction()
    {
        var fill = Fill.Create(Guid.NewGuid(), Symbol, Side.Buy, 10m, 12m, Now);
        var unitOfWork = new FakeUnitOfWork([Request(fill)]);

        await Should.ThrowAsync<ValidationException>(() =>
            Service(unitOfWork).ProcessAsync(Message(fill), "BETA", CancellationToken.None));

        unitOfWork.Begun.ShouldBeFalse();
    }

    [Fact]
    public async Task InvalidReplay_RollsBackAllState()
    {
        var sell = Fill.Create(Guid.NewGuid(), Symbol, Side.Sell, 10m, 20m, Now);
        var unitOfWork = new FakeUnitOfWork([Request(sell)]);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            Service(unitOfWork).ProcessAsync(Message(sell), Symbol, CancellationToken.None));

        unitOfWork.RolledBack.ShouldBeTrue();
        unitOfWork.Saved.ShouldBeFalse();
        unitOfWork.Committed.ShouldBeFalse();
    }

    [Fact]
    public async Task MissingRequest_RollsBackAndThrowsNotFound()
    {
        var fill = Fill.Create(Guid.NewGuid(), Symbol, Side.Buy, 10m, 12m, Now);
        var unitOfWork = new FakeUnitOfWork([]);

        await Should.ThrowAsync<ResourceNotFoundException>(() =>
            Service(unitOfWork).ProcessAsync(Message(fill), Symbol, CancellationToken.None));

        unitOfWork.RolledBack.ShouldBeTrue();
    }

    private static FillMessageHandler Service(IFillLedgerUnitOfWork unitOfWork) =>
        new(unitOfWork, new FixedTimeProvider(Now), new FillMessageValidator());

    private static FillRequestMessage Message(Fill fill) => new(
        fill.Id,
        fill.Symbol,
        fill.Side.ToString(),
        fill.Quantity,
        fill.Price,
        fill.ExecutedAt);

    private static PendingFillRequest Request(Fill fill, DateTimeOffset? processedAt = null) => new(
        fill.Id,
        fill.Symbol,
        fill.Side,
        fill.Quantity,
        fill.Price,
        fill.ExecutedAt,
        processedAt);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeUnitOfWork(IReadOnlyList<PendingFillRequest> requests) : IFillLedgerUnitOfWork
    {
        public IReadOnlyList<PendingFillRequest> Requests => requests;
        public Position? Position { get; private set; }
        public IReadOnlyList<RealisedPnlEntry> RealisedPnlEntries { get; private set; } = [];
        public IReadOnlyCollection<Guid> NewlyProcessedIds { get; private set; } = [];
        public DateTimeOffset ProcessedAt { get; private set; }
        public PendingFillRequest? Watermark { get; private set; }
        public bool Begun { get; private set; }
        public bool Saved { get; private set; }
        public bool Committed { get; private set; }
        public bool RolledBack { get; private set; }

        public Task BeginAsync(CancellationToken cancellationToken)
        {
            Begun = true;
            return Task.CompletedTask;
        }

        public Task<PendingFillRequest?> FindRequestAsync(
            Guid requestId,
            CancellationToken cancellationToken) =>
            Task.FromResult(requests.SingleOrDefault(request => request.Id == requestId));

        public Task AcquireSymbolLockAsync(string symbol, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<PendingFillRequest>> GetOrderedRequestsAsync(
            string symbol,
            CancellationToken cancellationToken) => Task.FromResult(requests);

        public Task ReplaceSymbolLedgerAsync(
            Position position,
            IReadOnlyList<RealisedPnlEntry> realisedPnlEntries,
            IReadOnlyCollection<Guid> newlyProcessedFillIds,
            DateTimeOffset processedAt,
            PendingFillRequest orderingWatermark,
            CancellationToken cancellationToken)
        {
            Position = position;
            RealisedPnlEntries = realisedPnlEntries;
            NewlyProcessedIds = newlyProcessedFillIds;
            ProcessedAt = processedAt;
            Watermark = orderingWatermark;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Saved = true;
            return Task.CompletedTask;
        }

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            Committed = true;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken)
        {
            RolledBack = true;
            return Task.CompletedTask;
        }
    }
}
