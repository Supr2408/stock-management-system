using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoxTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillPendingProductionStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "production_status",
                table: "barcode_label",
                type: "text",
                nullable: false,
                defaultValue: "PendingProduction",
                oldClrType: typeof(string),
                oldType: "text");
            migrationBuilder.Sql("UPDATE barcode_label SET production_status = 'PendingProduction' WHERE production_status = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "production_status",
                table: "barcode_label",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "PendingProduction");
        }
    }
}
