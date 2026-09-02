using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TradeLedger.Database;
using TradeLedger.Database.Entities;

namespace TradeLedger.IntegrationTests.Api.Support;

internal static class ApiTestData
{
    public static async Task ResetAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TradeLedgerDbContext>();
        context.Lots.RemoveRange(await context.Lots.ToListAsync());
        context.RealisedPnlEntries.RemoveRange(await context.RealisedPnlEntries.ToListAsync());
        context.Positions.RemoveRange(await context.Positions.ToListAsync());
        context.Fills.RemoveRange(await context.Fills.ToListAsync());
        await context.SaveChangesAsync();
    }

    public static async Task SeedPositionAsync(
        IServiceProvider services,
        string symbol,
        decimal realisedPnl,
        params LotSeed[] lots)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TradeLedgerDbContext>();
        context.Lots.AddRange(lots.Select(lot => new LotEntity
        {
            Id = lot.Id,
            Symbol = symbol,
            RemainingQuantity = lot.RemainingQuantity,
            UnitCost = lot.UnitCost,
            OpenedAt = lot.OpenedAt
        }));

        if (realisedPnl != 0)
        {
            context.RealisedPnlEntries.Add(new RealisedPnlEntryEntity
            {
                FillId = Guid.NewGuid(),
                Symbol = symbol,
                Amount = realisedPnl,
                RealisedAt = new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero)
            });
        }

        var latestLot = lots.OrderBy(lot => lot.OpenedAt).ThenBy(lot => lot.Id).Last();
        context.Positions.Add(new PositionEntity
        {
            Symbol = symbol,
            OpenQuantity = lots.Sum(lot => lot.RemainingQuantity),
            RealisedPnl = realisedPnl,
            LastAppliedExecutedAt = latestLot.OpenedAt,
            LastAppliedFillId = latestLot.Id,
            UpdatedAt = latestLot.OpenedAt
        });
        await context.SaveChangesAsync();
    }
}

internal sealed record LotSeed(
    Guid Id,
    decimal RemainingQuantity,
    decimal UnitCost,
    DateTimeOffset OpenedAt);
