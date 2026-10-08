using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BoxTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DispatchWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "dispatch_record_id",
                table: "barcode_label",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "dispatch_record",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    customer_id = table.Column<int>(type: "integer", nullable: false),
                    sales_order_number = table.Column<string>(type: "text", nullable: false),
                    invoice_number = table.Column<string>(type: "text", nullable: false),
                    dispatch_date = table.Column<DateOnly>(type: "date", nullable: false),
                    source_file_name = table.Column<string>(type: "text", nullable: false),
                    label_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatch_record", x => x.id);
                    table.ForeignKey(
                        name: "FK_dispatch_record_customer_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_barcode_label_dispatch_record_id",
                table: "barcode_label",
                column: "dispatch_record_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_record_customer_id",
                table: "dispatch_record",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_record_invoice_number",
                table: "dispatch_record",
                column: "invoice_number");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_record_sales_order_number",
                table: "dispatch_record",
                column: "sales_order_number");

            migrationBuilder.AddForeignKey(
                name: "FK_barcode_label_dispatch_record_dispatch_record_id",
                table: "barcode_label",
                column: "dispatch_record_id",
                principalTable: "dispatch_record",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_barcode_label_dispatch_record_dispatch_record_id",
                table: "barcode_label");

            migrationBuilder.DropTable(
                name: "dispatch_record");

            migrationBuilder.DropIndex(
                name: "IX_barcode_label_dispatch_record_id",
                table: "barcode_label");

            migrationBuilder.DropColumn(
                name: "dispatch_record_id",
                table: "barcode_label");
        }
    }
}
