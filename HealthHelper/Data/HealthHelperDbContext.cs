
using HealthHelper.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace HealthHelper.Data;

public class HealthHelperDbContext : DbContext
{
    public DbSet<TrackedEntry> TrackedEntries { get; set; }
    public DbSet<EntryAnalysis> EntryAnalyses { get; set; }
    public DbSet<DailySummary> DailySummaries { get; set; }

    public HealthHelperDbContext(DbContextOptions<HealthHelperDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TrackedEntry>().HasKey(e => e.EntryId);
        modelBuilder.Entity<EntryAnalysis>().HasKey(e => e.AnalysisId);
        modelBuilder.Entity<DailySummary>().HasKey(e => e.SummaryId);

        modelBuilder.Entity<DailySummaryEntryAnalyses>()
            .HasKey(de => new { de.SummaryId, de.AnalysisId });

        // Configure the relationship between TrackedEntry and EntryAnalysis
        modelBuilder.Entity<TrackedEntry>()
            .HasMany<EntryAnalysis>()
            .WithOne()
            .HasForeignKey(a => a.EntryId);
    }
}
