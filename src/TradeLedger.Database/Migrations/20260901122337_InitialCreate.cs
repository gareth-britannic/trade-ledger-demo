using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeLedger.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    side = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    price = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    executed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fills", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    remaining_quantity = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "realised_pnl_entries",
                columns: table => new
                {
                    fill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    realised_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_realised_pnl_entries", x => x.fill_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_fills_executed_at",
                table: "fills",
                column: "executed_at");

            migrationBuilder.CreateIndex(
                name: "ix_fills_processed_at",
                table: "fills",
                column: "processed_at");

            migrationBuilder.CreateIndex(
                name: "ix_fills_symbol",
                table: "fills",
                column: "symbol");

            migrationBuilder.CreateIndex(
                name: "ix_lots_symbol",
                table: "lots",
                column: "symbol");

            migrationBuilder.CreateIndex(
                name: "ix_lots_symbol_opened_at",
                table: "lots",
                columns: new[] { "symbol", "opened_at" });

            migrationBuilder.CreateIndex(
                name: "ix_lots_symbol_remaining_quantity",
                table: "lots",
                columns: new[] { "symbol", "remaining_quantity" });

            migrationBuilder.CreateIndex(
                name: "ix_realised_pnl_entries_symbol_realised_at",
                table: "realised_pnl_entries",
                columns: new[] { "symbol", "realised_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fills");

            migrationBuilder.DropTable(
                name: "lots");

            migrationBuilder.DropTable(
                name: "realised_pnl_entries");
        }
    }
}
