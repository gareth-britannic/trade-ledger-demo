using Microsoft.EntityFrameworkCore;
using Shouldly;
using TradeLedger.Application;
using TradeLedger.Application.Records;
using TradeLedger.Infrastructure.Database;
using TradeLedger.Infrastructure.Database.Entities;
using TradeLedger.Infrastructure.Database.Repositories;
using Xunit;

namespace TradeLedger.UnitTests.Infrastructure.Database;

public sealed class RepositoryTests
{
    private const string Symbol = "ACME";
    private const string OtherSymbol = "BETA";
    private const decimal ExpectedRealisedPnl = 12m;
    private static readonly DateTimeOffset September =
        new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task FillRepository_PersistsAndMapsFillModel()
    {
        // Arrange
        await using var context = Context();
        var repository = new FillRepository(context);
        var fill = Fill.Create(Guid.NewGuid(), Symbol, Side.Sell, 10.12345678m, 12.87654321m,
            DateTimeOffset.UtcNow);

        // Act
        await repository.AddAsync(fill, CancellationToken.None);

        // Assert
        var entity = await context.Fills.AsNoTracking().SingleAsync();
        entity.ToModel().ShouldBe(fill);
    }

    [Fact]
    public async Task LotRepository_DerivesOpenLotsAndRealisedPnl_UsingUntrackedModels()
    {
        // Arrange
        await using var context = Context();
        context.Lots.AddRange(
            Lot(Symbol, 10m, 12m, 1),
            Lot(OtherSymbol, 3m, 4m, 0));
        context.RealisedPnlEntries.AddRange(
            RealisedPnl(Symbol, 5m, September),
            RealisedPnl(Symbol, 7m, September.AddMinutes(1)),
            RealisedPnl(OtherSymbol, -2m, September));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var repository = new LotRepository(context);

        // Act
        var positions = await repository.GetPositionsAsync(CancellationToken.None);
        var acme = positions.Single(position => position.Symbol == Symbol);

        // Assert
        acme.OpenLots.ShouldHaveSingleItem().RemainingQuantity.ShouldBe(10m);
        acme.RealisedPnl.ShouldBe(ExpectedRealisedPnl);
        context.ChangeTracker.Entries().ShouldBeEmpty();
    }

    [Fact]
    public async Task LotRepository_MissingSymbol_ReturnsNull()
    {
        // Arrange
        await using var context = Context();
        var repository = new LotRepository(context);

        // Act
        var result = await repository.GetPositionAsync("MISSING", CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task FillProcessingRepository_CompletesFillLotsAndRealisedPnlAtomically()
    {
        // Arrange
        await using var context = Context();
        var fill = Fill.Create(Guid.NewGuid(), Symbol, Side.Sell, 5m, 15m, September);
        context.Fills.Add(FillEntity.FromModel(fill));
        context.Lots.Add(Lot(Symbol, 10m, 12m, 0));
        await context.SaveChangesAsync();
        var remainingLot = new Lot(Guid.NewGuid(), Symbol, 5m, 12m, September.AddDays(-1));
        var realisedPnl = new RealisedPnlEntry(fill.Id, Symbol, 15m, fill.ExecutedAt);
        var processedAt = September.AddMinutes(1);
        var repository = new FillProcessingRepository(context);

        // Act
        await repository.CompleteAsync(
            fill.Id,
            Symbol,
            [remainingLot],
            realisedPnl,
            processedAt,
            CancellationToken.None);

        // Assert
        (await context.Fills.SingleAsync()).ProcessedAt.ShouldBe(processedAt);
        (await context.Lots.SingleAsync()).ToModel().ShouldBe(remainingLot);
        var persistedPnl = await context.RealisedPnlEntries.SingleAsync();
        persistedPnl.FillId.ShouldBe(fill.Id);
        persistedPnl.Amount.ShouldBe(realisedPnl.Amount);
        persistedPnl.RealisedAt.ShouldBe(fill.ExecutedAt);
    }

    [Fact]
    public async Task RealisedPnlRepository_FiltersByUtcExecutionPeriod()
    {
        // Arrange
        await using var context = Context();
        context.RealisedPnlEntries.AddRange(
            RealisedPnl(Symbol, 10m, September.AddDays(-1)),
            RealisedPnl(Symbol, 20m, September),
            RealisedPnl(Symbol, -5m, September.AddMonths(1)));
        await context.SaveChangesAsync();
        var repository = new RealisedPnlRepository(context);

        // Act
        var result = await repository.GetTotalAsync(
            Symbol,
            September,
            September.AddMonths(1),
            CancellationToken.None);

        // Assert
        result.ShouldBe(20m);
    }

    private static TradeLedgerDbContext Context()
    {
        var options = new DbContextOptionsBuilder<TradeLedgerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new TradeLedgerDbContext(options);
    }

    private static LotEntity Lot(
        string symbol,
        decimal remaining,
        decimal cost,
        int minute) => new()
        {
            Id = Guid.NewGuid(),
            Symbol = symbol,
            RemainingQuantity = remaining,
            UnitCost = cost,
            OpenedAt = September.AddMinutes(minute)
        };

    private static RealisedPnlEntryEntity RealisedPnl(
        string symbol,
        decimal amount,
        DateTimeOffset realisedAt) => new()
        {
            FillId = Guid.NewGuid(),
            Symbol = symbol,
            Amount = amount,
            RealisedAt = realisedAt
        };
}
