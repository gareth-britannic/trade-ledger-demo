using Microsoft.EntityFrameworkCore;
using Shouldly;
using TradeLedger.Application.Records;
using TradeLedger.Database;
using TradeLedger.Database.Entities;
using TradeLedger.Database.Repositories;
using TradeLedger.Domain;
using TradeLedger.IntegrationTests.Processor;
using Xunit;

namespace TradeLedger.IntegrationTests.Database;

public sealed class FillLedgerUnitOfWorkTests
{
    private const string ConnectionString =
        "Host=localhost;Port=55432;Database=trade_ledger;Username=trade_ledger;Password=trade_ledger";

    [LocalStackFact]
    [Trait("Category", "External")]
    public async Task Rollback_DiscardsEveryLedgerChange()
    {
        var symbol = Symbol();
        var executedAt = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
        var openingRequest = PendingFillRequest.Create(
            Guid.NewGuid(), symbol, Side.Buy, 10m, 12m, executedAt.AddMinutes(-1));
        var request = PendingFillRequest.Create(
            Guid.NewGuid(), symbol, Side.Sell, 5m, 15m, executedAt);
        var originalLot = new LotEntity
        {
            Id = openingRequest.Id,
            Symbol = symbol,
            RemainingQuantity = 10m,
            UnitCost = 12m,
            OpenedAt = executedAt.AddMinutes(-1)
        };

        await using (var seedContext = Context())
        {
            seedContext.Fills.Add(FillEntity.FromRequest(openingRequest));
            seedContext.Fills.Add(FillEntity.FromRequest(request));
            seedContext.Lots.Add(originalLot);
            await seedContext.SaveChangesAsync();
        }

        try
        {
            await using var context = Context();
            var unitOfWork = new FillLedgerUnitOfWork(context);
            var replacementLot = new Lot(openingRequest.Id, symbol, 5m, 12m, originalLot.OpenedAt);
            var realisedPnl = new RealisedPnlEntry(request.Id, symbol, 15m, executedAt);

            await unitOfWork.BeginAsync(CancellationToken.None);
            await unitOfWork.ReplaceSymbolLedgerAsync(
                new Position(symbol, [replacementLot], realisedPnl.Amount),
                [realisedPnl],
                [request.Id],
                executedAt.AddMinutes(1),
                CancellationToken.None);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            await unitOfWork.RollbackAsync(CancellationToken.None);

            await using var assertionContext = Context();
            (await assertionContext.Fills.AsNoTracking().SingleAsync(fill => fill.Id == request.Id))
                .ProcessedAt.ShouldBeNull();
            (await assertionContext.Lots.AsNoTracking().SingleAsync(lot => lot.Symbol == symbol))
                .Id.ShouldBe(originalLot.Id);
            (await assertionContext.RealisedPnlEntries.AsNoTracking().AnyAsync(entry => entry.Symbol == symbol))
                .ShouldBeFalse();
            (await assertionContext.Positions.AsNoTracking().AnyAsync(position => position.Symbol == symbol))
                .ShouldBeFalse();
        }
        finally
        {
            await DeleteSymbolAsync(symbol);
        }
    }

    [LocalStackFact]
    [Trait("Category", "External")]
    public async Task AdvisoryLock_BlocksUntilOwningTransactionCompletes()
    {
        await using var firstContext = Context();
        await using var secondContext = Context();
        var first = new FillLedgerUnitOfWork(firstContext);
        var second = new FillLedgerUnitOfWork(secondContext);
        var symbol = Symbol();

        try
        {
            await first.BeginAsync(CancellationToken.None);
            await first.AcquireSymbolLockAsync(symbol, CancellationToken.None);
            await second.BeginAsync(CancellationToken.None);

            var waitingForLock = second.AcquireSymbolLockAsync(symbol, CancellationToken.None);
            await Task.Delay(TimeSpan.FromMilliseconds(250));
            waitingForLock.IsCompleted.ShouldBeFalse();

            await first.CommitAsync(CancellationToken.None);
            await waitingForLock.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            await first.RollbackAsync(CancellationToken.None);
            await second.RollbackAsync(CancellationToken.None);
        }
    }

    private static TradeLedgerDbContext Context()
    {
        var options = new DbContextOptionsBuilder<TradeLedgerDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new TradeLedgerDbContext(options);
    }

    private static string Symbol() => $"DB{Guid.NewGuid():N}"[..14].ToUpperInvariant();

    private static async Task DeleteSymbolAsync(string symbol)
    {
        await using var context = Context();
        context.RealisedPnlEntries.RemoveRange(
            await context.RealisedPnlEntries.Where(entry => entry.Symbol == symbol).ToListAsync());
        context.Lots.RemoveRange(await context.Lots.Where(lot => lot.Symbol == symbol).ToListAsync());
        context.Positions.RemoveRange(
            await context.Positions.Where(position => position.Symbol == symbol).ToListAsync());
        context.Fills.RemoveRange(await context.Fills.Where(fill => fill.Symbol == symbol).ToListAsync());
        await context.SaveChangesAsync();
    }
}
