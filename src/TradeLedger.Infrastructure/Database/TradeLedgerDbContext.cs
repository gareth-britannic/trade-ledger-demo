using Microsoft.EntityFrameworkCore;
using TradeLedger.Infrastructure.Database.Entities;

namespace TradeLedger.Infrastructure.Database;

public sealed class TradeLedgerDbContext(DbContextOptions<TradeLedgerDbContext> options) : DbContext(options)
{
    internal DbSet<FillEntity> Fills => Set<FillEntity>();

    internal DbSet<LotEntity> Lots => Set<LotEntity>();

    internal DbSet<RealisedPnlEntryEntity> RealisedPnlEntries => Set<RealisedPnlEntryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TradeLedgerDbContext).Assembly);
    }
}
