using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeLedger.Database.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnusedPositionState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_positions_ordering_watermark",
                table: "positions");

            migrationBuilder.DropColumn(
                name: "last_applied_executed_at",
                table: "positions");

            migrationBuilder.DropColumn(
                name: "last_applied_fill_id",
                table: "positions");

            migrationBuilder.DropColumn(
                name: "open_quantity",
                table: "positions");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "positions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_applied_executed_at",
                table: "positions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "last_applied_fill_id",
                table: "positions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<decimal>(
                name: "open_quantity",
                table: "positions",
                type: "numeric(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "positions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateIndex(
                name: "ix_positions_ordering_watermark",
                table: "positions",
                columns: new[] { "last_applied_executed_at", "last_applied_fill_id" });
        }
    }
}
