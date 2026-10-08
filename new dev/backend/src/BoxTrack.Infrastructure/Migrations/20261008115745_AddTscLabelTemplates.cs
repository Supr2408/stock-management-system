using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BoxTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTscLabelTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "active_template_id",
                table: "printer_configuration",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "dpi",
                table: "printer_configuration",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "mode",
                table: "printer_configuration",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "model",
                table: "printer_configuration",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "label_template",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    printer_type = table.Column<int>(type: "integer", nullable: false),
                    width_mm = table.Column<double>(type: "double precision", nullable: false),
                    height_mm = table.Column<double>(type: "double precision", nullable: false),
                    gap_mm = table.Column<double>(type: "double precision", nullable: false),
                    orientation = table.Column<int>(type: "integer", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_label_template", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "label_template_element",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    template_id = table.Column<int>(type: "integer", nullable: false),
                    element_type = table.Column<int>(type: "integer", nullable: false),
                    xmm = table.Column<double>(type: "double precision", nullable: false),
                    ymm = table.Column<double>(type: "double precision", nullable: false),
                    width_mm = table.Column<double>(type: "double precision", nullable: false),
                    height_mm = table.Column<double>(type: "double precision", nullable: false),
                    rotation = table.Column<int>(type: "integer", nullable: false),
                    z_index = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "text", nullable: true),
                    font_name = table.Column<string>(type: "text", nullable: true),
                    font_size = table.Column<int>(type: "integer", nullable: false),
                    barcode_type = table.Column<string>(type: "text", nullable: true),
                    human_readable = table.Column<bool>(type: "boolean", nullable: false),
                    fit_mode = table.Column<int>(type: "integer", nullable: false),
                    logo_id = table.Column<int>(type: "integer", nullable: true),
                    border_thickness_dots = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_label_template_element", x => x.id);
                    table.ForeignKey(
                        name: "FK_label_template_element_label_logo_logo_id",
                        column: x => x.logo_id,
                        principalTable: "label_logo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_label_template_element_label_template_template_id",
                        column: x => x.template_id,
                        principalTable: "label_template",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_printer_configuration_active_template_id",
                table: "printer_configuration",
                column: "active_template_id");

            migrationBuilder.CreateIndex(
                name: "IX_label_template_name",
                table: "label_template",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_label_template_element_logo_id",
                table: "label_template_element",
                column: "logo_id");

            migrationBuilder.CreateIndex(
                name: "IX_label_template_element_template_id",
                table: "label_template_element",
                column: "template_id");

            migrationBuilder.AddForeignKey(
                name: "FK_printer_configuration_label_template_active_template_id",
                table: "printer_configuration",
                column: "active_template_id",
                principalTable: "label_template",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_printer_configuration_label_template_active_template_id",
                table: "printer_configuration");

            migrationBuilder.DropTable(
                name: "label_template_element");

            migrationBuilder.DropTable(
                name: "label_template");

            migrationBuilder.DropIndex(
                name: "IX_printer_configuration_active_template_id",
                table: "printer_configuration");

            migrationBuilder.DropColumn(
                name: "active_template_id",
                table: "printer_configuration");

            migrationBuilder.DropColumn(
                name: "dpi",
                table: "printer_configuration");

            migrationBuilder.DropColumn(
                name: "mode",
                table: "printer_configuration");

            migrationBuilder.DropColumn(
                name: "model",
                table: "printer_configuration");
        }
    }
}
