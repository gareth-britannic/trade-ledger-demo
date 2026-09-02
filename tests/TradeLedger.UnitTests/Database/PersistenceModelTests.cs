using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using TradeLedger.Database;
using TradeLedger.Database.Entities;
using Xunit;

namespace TradeLedger.UnitTests.Database;

public sealed class PersistenceModelTests
{
    [Fact]
    public void Model_ConfiguresKeysIndexesEnumTimestampAndDecimalPrecision()
    {
        // Arrange
        using var context = new TradeLedgerDbContext(
            new DbContextOptionsBuilder<TradeLedgerDbContext>()
                .UseNpgsql("Host=localhost;Database=model_only;Username=test;Password=test")
                .Options);

        // Act
        var fill = context.Model.FindEntityType(typeof(FillEntity)).ShouldNotBeNull();

        // Assert
        fill.FindPrimaryKey().ShouldNotBeNull().Properties.Single().Name.ShouldBe(nameof(FillEntity.Id));
        fill.GetIndexes().ShouldContain(index =>
            index.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(FillEntity.Symbol), nameof(FillEntity.ProcessedAt)
            }));
        fill.GetIndexes().ShouldContain(index =>
            index.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(FillEntity.Symbol), nameof(FillEntity.ExecutedAt), nameof(FillEntity.Id)
            }));
        var side = fill.FindProperty(nameof(FillEntity.Side)).ShouldNotBeNull();
        side.GetProviderClrType().ShouldBe(typeof(string));
        fill.FindProperty(nameof(FillEntity.ExecutedAt)).ShouldNotBeNull()
            .GetColumnType().ShouldBe("timestamp with time zone");
        fill.FindProperty(nameof(FillEntity.ProcessedAt)).ShouldNotBeNull()
            .GetColumnType().ShouldBe("timestamp with time zone");
        AssertDecimal(fill, nameof(FillEntity.Quantity));
        AssertDecimal(fill, nameof(FillEntity.Price));

        var lot = context.Model.FindEntityType(typeof(LotEntity)).ShouldNotBeNull();
        AssertDecimal(lot, nameof(LotEntity.RemainingQuantity));
        AssertDecimal(lot, nameof(LotEntity.UnitCost));

        var realisedPnl = context.Model.FindEntityType(typeof(RealisedPnlEntryEntity)).ShouldNotBeNull();
        realisedPnl.FindPrimaryKey().ShouldNotBeNull().Properties.Single().Name
            .ShouldBe(nameof(RealisedPnlEntryEntity.FillId));
        realisedPnl.FindProperty(nameof(RealisedPnlEntryEntity.RealisedAt)).ShouldNotBeNull()
            .GetColumnType().ShouldBe("timestamp with time zone");
        AssertDecimal(realisedPnl, nameof(RealisedPnlEntryEntity.Amount));

        var position = context.Model.FindEntityType(typeof(PositionEntity)).ShouldNotBeNull();
        position.FindPrimaryKey().ShouldNotBeNull().Properties.Single().Name
            .ShouldBe(nameof(PositionEntity.Symbol));
        position.FindProperty(nameof(PositionEntity.LastAppliedExecutedAt)).ShouldNotBeNull()
            .GetColumnType().ShouldBe("timestamp with time zone");
        position.FindProperty(nameof(PositionEntity.UpdatedAt)).ShouldNotBeNull()
            .GetColumnType().ShouldBe("timestamp with time zone");
        AssertDecimal(position, nameof(PositionEntity.OpenQuantity));
        AssertDecimal(position, nameof(PositionEntity.RealisedPnl));
    }

    private static void AssertDecimal(IEntityType entity, string propertyName)
    {
        var property = entity.FindProperty(propertyName).ShouldNotBeNull();
        property.GetPrecision().ShouldBe(28);
        property.GetScale().ShouldBe(8);
    }
}
