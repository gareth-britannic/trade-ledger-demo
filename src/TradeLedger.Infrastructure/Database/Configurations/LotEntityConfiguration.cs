using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Infrastructure.Database.Entities;

namespace TradeLedger.Infrastructure.Database.Configurations;

internal sealed class LotEntityConfiguration : IEntityTypeConfiguration<LotEntity>
{
    public void Configure(EntityTypeBuilder<LotEntity> builder)
    {
        builder.ToTable("lots");
        builder.HasKey(lot => lot.Id).HasName("pk_lots");

        builder.Property(lot => lot.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(lot => lot.Symbol).HasColumnName("symbol").HasMaxLength(32).IsRequired();
        builder.Property(lot => lot.RemainingQuantity)
            .HasColumnName("remaining_quantity")
            .HasPrecision(28, 8)
            .IsRequired();
        builder.Property(lot => lot.UnitCost).HasColumnName("unit_cost").HasPrecision(28, 8).IsRequired();
        builder.Property(lot => lot.OpenedAt)
            .HasColumnName("opened_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(lot => lot.Symbol).HasDatabaseName("ix_lots_symbol");
        builder.HasIndex(lot => new { lot.Symbol, lot.RemainingQuantity })
            .HasDatabaseName("ix_lots_symbol_remaining_quantity");
        builder.HasIndex(lot => new { lot.Symbol, lot.OpenedAt })
            .HasDatabaseName("ix_lots_symbol_opened_at");
    }
}
