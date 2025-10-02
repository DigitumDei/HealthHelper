using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthHelper.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessingStatusToTrackedEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProcessingStatus",
                table: "TrackedEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProcessingStatus",
                table: "TrackedEntries");
        }
    }
}
