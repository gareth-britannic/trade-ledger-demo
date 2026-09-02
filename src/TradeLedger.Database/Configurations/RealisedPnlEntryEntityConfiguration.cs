using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradeLedger.Database.Entities;

namespace TradeLedger.Database.Configurations;

internal sealed class RealisedPnlEntryEntityConfiguration : IEntityTypeConfiguration<RealisedPnlEntryEntity>
{
    public void Configure(EntityTypeBuilder<RealisedPnlEntryEntity> builder)
    {
        builder.ToTable("realised_pnl_entries");
        builder.HasKey(entry => entry.FillId).HasName("pk_realised_pnl_entries");

        builder.Property(entry => entry.FillId).HasColumnName("fill_id").ValueGeneratedNever();
        builder.Property(entry => entry.Symbol).HasColumnName("symbol").HasMaxLength(32).IsRequired();
        builder.Property(entry => entry.Amount).HasColumnName("amount").HasPrecision(28, 8).IsRequired();
        builder.Property(entry => entry.RealisedAt)
            .HasColumnName("realised_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(entry => new { entry.Symbol, entry.RealisedAt })
            .HasDatabaseName("ix_realised_pnl_entries_symbol_realised_at");

        builder.HasOne<FillEntity>()
            .WithMany()
            .HasForeignKey(entry => entry.FillId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_realised_pnl_entries_fills_fill_id");
    }
}
