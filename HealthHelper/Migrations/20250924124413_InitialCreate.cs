using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthHelper.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailySummaries",
                columns: table => new
                {
                    SummaryId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ExternalId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SummaryDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Highlights = table.Column<string>(type: "TEXT", nullable: false),
                    Recommendations = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailySummaries", x => x.SummaryId);
                });

            migrationBuilder.CreateTable(
                name: "DailySummaryEntryAnalyses",
                columns: table => new
                {
                    SummaryId = table.Column<int>(type: "INTEGER", nullable: false),
                    AnalysisId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailySummaryEntryAnalyses", x => new { x.SummaryId, x.AnalysisId });
                });

            migrationBuilder.CreateTable(
                name: "TrackedEntries",
                columns: table => new
                {
                    EntryId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ExternalId = table.Column<Guid>(type: "TEXT", nullable: true),
                    EntryType = table.Column<string>(type: "TEXT", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BlobPath = table.Column<string>(type: "TEXT", nullable: true),
                    DataPayload = table.Column<string>(type: "TEXT", nullable: false),
                    DataSchemaVersion = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackedEntries", x => x.EntryId);
                });

            migrationBuilder.CreateTable(
                name: "EntryAnalyses",
                columns: table => new
                {
                    AnalysisId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EntryId = table.Column<int>(type: "INTEGER", nullable: false),
                    ExternalId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProviderId = table.Column<string>(type: "TEXT", nullable: false),
                    Model = table.Column<string>(type: "TEXT", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    InsightsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntryAnalyses", x => x.AnalysisId);
                    table.ForeignKey(
                        name: "FK_EntryAnalyses_TrackedEntries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "TrackedEntries",
                        principalColumn: "EntryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntryAnalyses_EntryId",
                table: "EntryAnalyses",
                column: "EntryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailySummaries");

            migrationBuilder.DropTable(
                name: "DailySummaryEntryAnalyses");

            migrationBuilder.DropTable(
                name: "EntryAnalyses");

            migrationBuilder.DropTable(
                name: "TrackedEntries");
        }
    }
}
