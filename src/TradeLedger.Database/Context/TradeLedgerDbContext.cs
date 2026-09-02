using Microsoft.EntityFrameworkCore;
using TradeLedger.Database.Entities;

namespace TradeLedger.Database;

public sealed class TradeLedgerDbContext(DbContextOptions<TradeLedgerDbContext> options) : DbContext(options)
{
    internal DbSet<FillEntity> Fills => Set<FillEntity>();

    internal DbSet<LotEntity> Lots => Set<LotEntity>();

    internal DbSet<RealisedPnlEntryEntity> RealisedPnlEntries => Set<RealisedPnlEntryEntity>();

    internal DbSet<PositionEntity> Positions => Set<PositionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TradeLedgerDbContext).Assembly);
    }
}
