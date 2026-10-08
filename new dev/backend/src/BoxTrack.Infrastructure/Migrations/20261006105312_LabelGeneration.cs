using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BoxTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LabelGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "label_logo",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    relative_path = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_label_logo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "barcode_label",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    item_id = table.Column<int>(type: "integer", nullable: false),
                    item_code = table.Column<int>(type: "integer", nullable: false),
                    manufacture_date = table.Column<DateOnly>(type: "date", nullable: false),
                    serial_number = table.Column<int>(type: "integer", nullable: false),
                    barcode_value = table.Column<string>(type: "text", nullable: false),
                    logo_mode = table.Column<string>(type: "text", nullable: false),
                    label_logo_id = table.Column<int>(type: "integer", nullable: true),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    relative_path = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_barcode_label", x => x.id);
                    table.ForeignKey(
                        name: "FK_barcode_label_item_item_id",
                        column: x => x.item_id,
                        principalTable: "item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_barcode_label_label_logo_label_logo_id",
                        column: x => x.label_logo_id,
                        principalTable: "label_logo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_barcode_label_barcode_value",
                table: "barcode_label",
                column: "barcode_value",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_barcode_label_item_id",
                table: "barcode_label",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "IX_barcode_label_label_logo_id",
                table: "barcode_label",
                column: "label_logo_id");

            migrationBuilder.CreateIndex(
                name: "IX_label_logo_name",
                table: "label_logo",
                column: "name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "barcode_label");

            migrationBuilder.DropTable(
                name: "label_logo");
        }
    }
}
