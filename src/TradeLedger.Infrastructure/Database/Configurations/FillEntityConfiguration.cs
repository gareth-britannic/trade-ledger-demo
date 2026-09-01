using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Infrastructure.Database.Entities;

namespace TradeLedger.Infrastructure.Database.Configurations;

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

        builder.HasIndex(fill => fill.Symbol).HasDatabaseName("ix_fills_symbol");
        builder.HasIndex(fill => fill.ExecutedAt).HasDatabaseName("ix_fills_executed_at");
        builder.HasIndex(fill => fill.ProcessedAt).HasDatabaseName("ix_fills_processed_at");
    }
}
