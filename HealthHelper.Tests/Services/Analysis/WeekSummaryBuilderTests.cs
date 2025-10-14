using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HealthHelper.Data;
using HealthHelper.Models;
using HealthHelper.Services.Analysis;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HealthHelper.Tests.Services.Analysis;

public class WeekSummaryBuilderTests
{
    [Fact]
    public async Task BuildAsync_ComposesHighlightsAndRecommendations()
    {
        var culture = CultureInfo.CurrentCulture;
        var firstDay = culture.DateTimeFormat.FirstDayOfWeek;
        var today = DateTime.Today;
        var offsetToWeekStart = (7 + (today.DayOfWeek - firstDay)) % 7;
        var weekStart = today.AddDays(-offsetToWeekStart).Date;

        var daySummaries = Enumerable.Range(0, 7)
            .Select(offset =>
            {
                var date = weekStart.AddDays(offset);
                return new DaySummary
                {
                    Date = date,
                    MealCount = offset == 0 ? 1 : 0,
                    ExerciseCount = offset == 1 ? 1 : 0,
                    SleepCount = offset == 2 ? 1 : 0,
                    OtherCount = 0,
                    PendingCount = offset == 3 ? 1 : 0,
                    CompletedCount = offset == 0 ? 1 : 0,
                    HasPendingOrFailedAnalysis = offset == 3,
                    DailySummaryStatus = offset == 4 ? ProcessingStatus.Completed : null,
                    DailySummaryEntryId = offset == 4 ? 501 : null,
                    Previews = offset == 0
                        ? new[] { new DayPreview(10, EntryType.Meal, "Entries/Meal/preview.jpg") }
                        : Array.Empty<DayPreview>()
                };
            })
            .ToList();

        var resultPayload = new DailySummaryResult
        {
            Insights = new List<string> { "Met sleep goal" },
            Recommendations = new List<string> { "Earlier bedtime" }
        };

        var repository = new WeekSummaryBuilderTestsEntryAnalysisRepository(new Dictionary<int, EntryAnalysis>
        {
            [501] = new EntryAnalysis
            {
                EntryId = 501,
                InsightsJson = JsonSerializer.Serialize(resultPayload)
            }
        });

        var builder = new WeekSummaryBuilder(repository, NullLogger<WeekSummaryBuilder>.Instance);

        var summary = await builder.BuildAsync(weekStart, daySummaries);

        Assert.NotNull(summary);
        Assert.Contains("Logged 4 entries", summary!.Highlights);
        Assert.Contains("Daily highlights:", summary.Highlights);
        Assert.Contains("Earlier bedtime", summary.Recommendations);
    }

    [Fact]
    public async Task BuildAsync_ReturnsNullWhenNoDays()
    {
        var repository = new WeekSummaryBuilderTestsEntryAnalysisRepository(new Dictionary<int, EntryAnalysis>());
        var builder = new WeekSummaryBuilder(repository, NullLogger<WeekSummaryBuilder>.Instance);

        var summary = await builder.BuildAsync(DateTime.Today, Array.Empty<DaySummary>());

        Assert.Null(summary);
    }

    private sealed class WeekSummaryBuilderTestsEntryAnalysisRepository : IEntryAnalysisRepository
    {
        private readonly IReadOnlyDictionary<int, EntryAnalysis> _entries;

        public WeekSummaryBuilderTestsEntryAnalysisRepository(IReadOnlyDictionary<int, EntryAnalysis> entries)
        {
            _entries = entries;
        }

        public Task<EntryAnalysis?> GetByTrackedEntryIdAsync(int trackedEntryId)
        {
            _entries.TryGetValue(trackedEntryId, out var entry);
            return Task.FromResult<EntryAnalysis?>(entry);
        }

        public Task AddAsync(EntryAnalysis analysis) => throw new NotImplementedException();
        public Task<IEnumerable<EntryAnalysis>> ListByDayAsync(DateTime date, TimeZoneInfo? timeZone = null) => throw new NotImplementedException();
        public Task UpdateAsync(EntryAnalysis analysis) => throw new NotImplementedException();
    }
}
