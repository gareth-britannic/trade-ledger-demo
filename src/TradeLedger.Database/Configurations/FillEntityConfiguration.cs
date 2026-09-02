using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Database.Entities;

namespace TradeLedger.Database.Configurations;

internal sealed class FillEntityConfiguration : IEntityTypeConfiguration<FillEntity>
{
    public void Configure(EntityTypeBuilder<FillEntity> builder)
    {
        builder.ToTable("fills");
        builder.HasKey(fill => fill.Id).HasName("pk_fills");

        builder.Property(fill => fill.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(fill => fill.Symbol).HasColumnName("symbol").HasMaxLength(32).IsRequired();
        builder.Property(fill => fill.Side)
            .HasColumnName("side")
            .HasConversion<string>()
            .HasMaxLength(8)
            .IsRequired();
        builder.Property(fill => fill.Quantity).HasColumnName("quantity").HasPrecision(28, 8).IsRequired();
        builder.Property(fill => fill.Price).HasColumnName("price").HasPrecision(28, 8).IsRequired();
        builder.Property(fill => fill.ExecutedAt)
            .HasColumnName("executed_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(fill => fill.ProcessedAt)
            .HasColumnName("processed_at")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(fill => new { fill.Symbol, fill.ProcessedAt })
            .HasDatabaseName("ix_fills_symbol_processed_at");
        builder.HasIndex(fill => new { fill.Symbol, fill.ExecutedAt, fill.Id })
            .HasDatabaseName("ix_fills_symbol_executed_at_id");
    }
}
