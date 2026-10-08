using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BoxTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAccountsAndA4Settings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "horizontal_gap_mm",
                table: "printer_configuration",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "margin_bottom_mm",
                table: "printer_configuration",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "margin_left_mm",
                table: "printer_configuration",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "margin_right_mm",
                table: "printer_configuration",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "margin_top_mm",
                table: "printer_configuration",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "paper_height_mm",
                table: "printer_configuration",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "paper_size",
                table: "printer_configuration",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "paper_width_mm",
                table: "printer_configuration",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "vertical_gap_mm",
                table: "printer_configuration",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.CreateTable(
                name: "user_account",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    username = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    temporary_dev_password = table.Column<string>(type: "text", nullable: true),
                    department_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_account", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_account_department_department_id",
                        column: x => x.department_id,
                        principalTable: "department",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_account_department_id",
                table: "user_account",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_account_username",
                table: "user_account",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_account");

            migrationBuilder.DropColumn(
                name: "horizontal_gap_mm",
                table: "printer_configuration");

            migrationBuilder.DropColumn(
                name: "margin_bottom_mm",
                table: "printer_configuration");

            migrationBuilder.DropColumn(
                name: "margin_left_mm",
                table: "printer_configuration");

            migrationBuilder.DropColumn(
                name: "margin_right_mm",
                table: "printer_configuration");

            migrationBuilder.DropColumn(
                name: "margin_top_mm",
                table: "printer_configuration");

            migrationBuilder.DropColumn(
                name: "paper_height_mm",
                table: "printer_configuration");

            migrationBuilder.DropColumn(
                name: "paper_size",
                table: "printer_configuration");

            migrationBuilder.DropColumn(
                name: "paper_width_mm",
                table: "printer_configuration");

            migrationBuilder.DropColumn(
                name: "vertical_gap_mm",
                table: "printer_configuration");
        }
    }
}
