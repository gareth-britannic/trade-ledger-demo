using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeLedger.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionalFillLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_lots_symbol_opened_at",
                table: "lots");

            migrationBuilder.DropIndex(
                name: "ix_fills_executed_at",
                table: "fills");

            migrationBuilder.DropIndex(
                name: "ix_fills_processed_at",
                table: "fills");

            migrationBuilder.DropIndex(
                name: "ix_fills_symbol",
                table: "fills");

            migrationBuilder.CreateTable(
                name: "positions",
                columns: table => new
                {
                    symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    open_quantity = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    realised_pnl = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    last_applied_executed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_applied_fill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_positions", x => x.symbol);
                });

            migrationBuilder.Sql(
                """
                WITH ledger_symbols AS (
                    SELECT symbol FROM lots
                    UNION
                    SELECT symbol FROM realised_pnl_entries
                ),
                lot_totals AS (
                    SELECT symbol, SUM(remaining_quantity) AS open_quantity
                    FROM lots
                    WHERE remaining_quantity > 0
                    GROUP BY symbol
                ),
                pnl_totals AS (
                    SELECT symbol, SUM(amount) AS realised_pnl
                    FROM realised_pnl_entries
                    GROUP BY symbol
                ),
                watermarks AS (
                    SELECT DISTINCT ON (symbol) symbol, executed_at, id, processed_at
                    FROM fills
                    WHERE processed_at IS NOT NULL
                    ORDER BY symbol, executed_at DESC, id DESC
                )
                INSERT INTO positions (
                    symbol,
                    open_quantity,
                    realised_pnl,
                    last_applied_executed_at,
                    last_applied_fill_id,
                    updated_at)
                SELECT
                    symbols.symbol,
                    COALESCE(lots.open_quantity, 0),
                    COALESCE(pnl.realised_pnl, 0),
                    COALESCE(watermarks.executed_at, TIMESTAMPTZ '1970-01-01 00:00:00+00'),
                    COALESCE(watermarks.id, UUID '00000000-0000-0000-0000-000000000000'),
                    COALESCE(watermarks.processed_at, CURRENT_TIMESTAMP)
                FROM ledger_symbols AS symbols
                LEFT JOIN lot_totals AS lots ON lots.symbol = symbols.symbol
                LEFT JOIN pnl_totals AS pnl ON pnl.symbol = symbols.symbol
                LEFT JOIN watermarks ON watermarks.symbol = symbols.symbol;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_lots_symbol_opened_at_id",
                table: "lots",
                columns: new[] { "symbol", "opened_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_fills_symbol_executed_at_id",
                table: "fills",
                columns: new[] { "symbol", "executed_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_fills_symbol_processed_at",
                table: "fills",
                columns: new[] { "symbol", "processed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_positions_ordering_watermark",
                table: "positions",
                columns: new[] { "last_applied_executed_at", "last_applied_fill_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_lots_fills_id",
                table: "lots",
                column: "id",
                principalTable: "fills",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_realised_pnl_entries_fills_fill_id",
                table: "realised_pnl_entries",
                column: "fill_id",
                principalTable: "fills",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_lots_fills_id",
                table: "lots");

            migrationBuilder.DropForeignKey(
                name: "fk_realised_pnl_entries_fills_fill_id",
                table: "realised_pnl_entries");

            migrationBuilder.DropTable(
                name: "positions");

            migrationBuilder.DropIndex(
                name: "ix_lots_symbol_opened_at_id",
                table: "lots");

            migrationBuilder.DropIndex(
                name: "ix_fills_symbol_executed_at_id",
                table: "fills");

            migrationBuilder.DropIndex(
                name: "ix_fills_symbol_processed_at",
                table: "fills");

            migrationBuilder.CreateIndex(
                name: "ix_lots_symbol_opened_at",
                table: "lots",
                columns: new[] { "symbol", "opened_at" });

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
        }
    }
}
