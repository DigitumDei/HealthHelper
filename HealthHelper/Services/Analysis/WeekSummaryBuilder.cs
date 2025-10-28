using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HealthHelper.Data;
using HealthHelper.Models;
using Microsoft.Extensions.Logging;

namespace HealthHelper.Services.Analysis;

/// <summary>
/// Aggregates daily summaries into a weekly summary view.
/// </summary>
public class WeekSummaryBuilder
{
    private readonly IEntryAnalysisRepository _entryAnalysisRepository;
    private readonly ILogger<WeekSummaryBuilder> _logger;

    public WeekSummaryBuilder(
        IEntryAnalysisRepository entryAnalysisRepository,
        ILogger<WeekSummaryBuilder> logger)
    {
        _entryAnalysisRepository = entryAnalysisRepository;
        _logger = logger;
    }

    public async Task<DailySummary?> BuildAsync(
        DateTime weekStart,
        IReadOnlyList<DaySummary> daySummaries,
        CancellationToken cancellationToken = default)
    {
        if (daySummaries is null || daySummaries.Count == 0)
        {
            return null;
        }

        var totalEntries = daySummaries.Sum(d => d.TotalCount);
        var mealCount = daySummaries.Sum(d => d.MealCount);
        var exerciseCount = daySummaries.Sum(d => d.ExerciseCount);
        var sleepCount = daySummaries.Sum(d => d.SleepCount);
        var otherCount = daySummaries.Sum(d => d.OtherCount);
        var pendingCount = daySummaries.Sum(d => d.PendingCount);

        var highlightLines = new List<string>();

        if (totalEntries == 0)
        {
            highlightLines.Add("No entries were captured this week.");
        }
        else
        {
            highlightLines.Add($"Logged {totalEntries} entries this week.");

            var typeHighlights = new List<string>();
            if (mealCount > 0)
            {
                typeHighlights.Add($"{mealCount} meals");
            }

            if (exerciseCount > 0)
            {
                typeHighlights.Add($"{exerciseCount} workouts");
            }

            if (sleepCount > 0)
            {
                typeHighlights.Add($"{sleepCount} sleep logs");
            }

            if (otherCount > 0)
            {
                typeHighlights.Add($"{otherCount} other entries");
            }

            if (typeHighlights.Count > 0)
            {
                highlightLines.Add(string.Join(", ", typeHighlights));
            }

            var busiestDay = daySummaries
                .Where(d => d.TotalCount > 0)
                .OrderByDescending(d => d.TotalCount)
                .ThenByDescending(d => d.Date)
                .FirstOrDefault();

            if (busiestDay is not null)
            {
                highlightLines.Add($"Busiest day: {busiestDay.Date:dddd} with {busiestDay.TotalCount} entries.");
            }
        }

        if (pendingCount > 0)
        {
            highlightLines.Add($"{pendingCount} entries are still pending analysis.");
        }

        var insightLines = new List<string>();
        var recommendationLines = new List<string>();

        foreach (var day in daySummaries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (day.DailySummaryEntryId is not int summaryEntryId || day.DailySummaryStatus != ProcessingStatus.Completed)
            {
                continue;
            }

            EntryAnalysis? analysis;
            try
            {
                analysis = await _entryAnalysisRepository
                    .GetByTrackedEntryIdAsync(summaryEntryId)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load entry analysis for summary entry {EntryId}.", summaryEntryId);
                continue;
            }

            if (analysis is null || string.IsNullOrWhiteSpace(analysis.InsightsJson))
            {
                continue;
            }

            DailySummaryResult? result = null;
            try
            {
                result = JsonSerializer.Deserialize<DailySummaryResult>(analysis.InsightsJson);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse daily summary JSON for entry {EntryId}.", summaryEntryId);
            }

            if (result is null)
            {
                continue;
            }

            var dayPrefix = day.Date.ToString("ddd", CultureInfo.CurrentCulture);

            if (result.Insights?.FirstOrDefault() is string insight && !string.IsNullOrWhiteSpace(insight))
            {
                insightLines.Add($"{dayPrefix}: {insight}");
            }

            if (result.Recommendations?.FirstOrDefault() is string recommendation && !string.IsNullOrWhiteSpace(recommendation))
            {
                recommendationLines.Add($"{dayPrefix}: {recommendation}");
            }
        }

        if (insightLines.Count > 0)
        {
            highlightLines.Add(string.Empty);
            highlightLines.Add("Daily highlights:");
            highlightLines.AddRange(insightLines);
        }

        var weeklySummary = new DailySummary
        {
            SummaryDate = weekStart,
            Highlights = string.Join(Environment.NewLine, highlightLines),
            Recommendations = recommendationLines.Count == 0
                ? string.Empty
                : string.Join(Environment.NewLine, recommendationLines)
        };

        return weeklySummary;
    }
}
