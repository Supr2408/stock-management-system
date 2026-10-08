using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoxTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillStockReceiptLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE barcode_label AS label
                SET stock_receipt_id = receipt.id
                FROM stock_receipt AS receipt
                WHERE label.stock_receipt_id IS NULL
                  AND label.item_id = receipt.item_id
                  AND label.barcode_value >= receipt.from_barcode
                  AND label.barcode_value <= receipt.to_barcode
                  AND label.production_status IN ('AddedToStock', 'Dispatched');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
