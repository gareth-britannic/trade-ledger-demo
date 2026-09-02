using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Database.Entities;

namespace TradeLedger.Database.Configurations;

internal sealed class PositionEntityConfiguration : IEntityTypeConfiguration<PositionEntity>
{
    public void Configure(EntityTypeBuilder<PositionEntity> builder)
    {
        builder.ToTable("positions");
        builder.HasKey(position => position.Symbol).HasName("pk_positions");

        builder.Property(position => position.Symbol)
            .HasColumnName("symbol")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(position => position.RealisedPnl)
            .HasColumnName("realised_pnl")
            .HasPrecision(28, 8)
            .IsRequired();
    }
}
