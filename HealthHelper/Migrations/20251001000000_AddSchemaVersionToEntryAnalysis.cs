using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthHelper.Migrations
{
    /// <inheritdoc />
    public partial class AddSchemaVersionToEntryAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SchemaVersion",
                table: "EntryAnalyses",
                type: "TEXT",
                nullable: false,
                defaultValue: "1.0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SchemaVersion",
                table: "EntryAnalyses");
        }
    }
}
