using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BoxTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProductionStockWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "production_status",
                table: "barcode_label",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "stock_receipt_id",
                table: "barcode_label",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "stock_receipt",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    item_id = table.Column<int>(type: "integer", nullable: false),
                    batch_number = table.Column<string>(type: "text", nullable: false),
                    from_barcode = table.Column<string>(type: "text", nullable: false),
                    to_barcode = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_receipt", x => x.id);
                    table.ForeignKey(
                        name: "FK_stock_receipt_item_item_id",
                        column: x => x.item_id,
                        principalTable: "item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_barcode_label_production_status",
                table: "barcode_label",
                column: "production_status");

            migrationBuilder.CreateIndex(
                name: "IX_barcode_label_stock_receipt_id",
                table: "barcode_label",
                column: "stock_receipt_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_receipt_batch_number",
                table: "stock_receipt",
                column: "batch_number");

            migrationBuilder.CreateIndex(
                name: "IX_stock_receipt_item_id",
                table: "stock_receipt",
                column: "item_id");

            migrationBuilder.AddForeignKey(
                name: "FK_barcode_label_stock_receipt_stock_receipt_id",
                table: "barcode_label",
                column: "stock_receipt_id",
                principalTable: "stock_receipt",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_barcode_label_stock_receipt_stock_receipt_id",
                table: "barcode_label");

            migrationBuilder.DropTable(
                name: "stock_receipt");

            migrationBuilder.DropIndex(
                name: "IX_barcode_label_production_status",
                table: "barcode_label");

            migrationBuilder.DropIndex(
                name: "IX_barcode_label_stock_receipt_id",
                table: "barcode_label");

            migrationBuilder.DropColumn(
                name: "production_status",
                table: "barcode_label");

            migrationBuilder.DropColumn(
                name: "stock_receipt_id",
                table: "barcode_label");
        }
    }
}
