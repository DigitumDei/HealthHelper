using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HealthHelper.Data;
using HealthHelper.Models;
using HealthHelper.Services.Analysis;
using HealthHelper.Services.Llm;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HealthHelper.Tests.Services.Analysis;

public class DailySummaryServiceTests
{
    [Fact]
    public async Task GenerateAsync_IncludesAllCompletedEntriesInRequest()
    {
        var baseDate = new DateTime(2024, 9, 5, 6, 0, 0, DateTimeKind.Utc);

        var dayEntries = new List<TrackedEntry>
        {
            new()
            {
                EntryId = 1,
                EntryType = EntryType.Meal,
                CapturedAt = baseDate.AddHours(1),
                Payload = new MealPayload { Description = "Breakfast" },
                ProcessingStatus = ProcessingStatus.Completed
            },
            new()
            {
                EntryId = 2,
                EntryType = EntryType.Exercise,
                CapturedAt = baseDate.AddHours(3),
                Payload = new ExercisePayload { Description = "Morning run", ExerciseType = "Running" },
                ProcessingStatus = ProcessingStatus.Completed
            },
            new()
            {
                EntryId = 3,
                EntryType = EntryType.Other,
                CapturedAt = baseDate.AddHours(5),
                Payload = new PendingEntryPayload { Description = "Meditation" },
                ProcessingStatus = ProcessingStatus.Completed
            },
            new()
            {
                EntryId = 99,
                EntryType = EntryType.DailySummary,
                CapturedAt = baseDate.AddHours(23),
                Payload = new DailySummaryPayload { SchemaVersion = 1, EntryCount = 0 },
                ProcessingStatus = ProcessingStatus.Pending
            }
        };

        var summaryEntry = dayEntries.Single(e => e.EntryType == EntryType.DailySummary);

        var analysisRepo = new InMemoryEntryAnalysisRepository(new List<EntryAnalysis>
        {
            new()
            {
                EntryId = 1,
                CapturedAt = baseDate.AddHours(1),
                InsightsJson = JsonSerializer.Serialize(new UnifiedAnalysisResult
                {
                    EntryType = EntryType.Meal.ToStorageString(),
                    MealAnalysis = new MealAnalysisResult()
                })
            },
            new()
            {
                EntryId = 2,
                CapturedAt = baseDate.AddHours(3),
                InsightsJson = JsonSerializer.Serialize(new UnifiedAnalysisResult
                {
                    EntryType = EntryType.Exercise.ToStorageString(),
                    ExerciseAnalysis = new ExerciseAnalysisResult()
                })
            }
        });

        var trackedRepo = new InMemoryTrackedEntryRepository(dayEntries);
        var settingsRepo = new InMemoryAppSettingsRepository(new AppSettings
        {
            SelectedProvider = LlmProvider.OpenAI,
            ApiKeys = new Dictionary<LlmProvider, string> { [LlmProvider.OpenAI] = "key" }
        });

        var llmClient = new CapturingLlmClient();
        var service = new DailySummaryService(
            settingsRepo,
            analysisRepo,
            trackedRepo,
            llmClient,
            NullLogger<DailySummaryService>.Instance);

        await service.GenerateAsync(summaryEntry);

        Assert.NotNull(llmClient.LastRequest);
        Assert.Equal(3, llmClient.LastRequest!.Entries.Count);

        Assert.Contains(llmClient.LastRequest.Entries, e => e.EntryId == 1 && e.EntryType == EntryType.Meal);
        Assert.Contains(llmClient.LastRequest.Entries, e => e.EntryId == 2 && e.EntryType == EntryType.Exercise);
        Assert.Contains(llmClient.LastRequest.Entries, e => e.EntryId == 3 && e.EntryType == EntryType.Other);
    }

    private sealed class InMemoryAppSettingsRepository : IAppSettingsRepository
    {
        private readonly AppSettings _settings;

        public InMemoryAppSettingsRepository(AppSettings settings)
        {
            _settings = settings;
        }

        public Task<AppSettings> GetAppSettingsAsync() => Task.FromResult(_settings);
        public Task SaveAppSettingsAsync(AppSettings settings)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class InMemoryEntryAnalysisRepository : IEntryAnalysisRepository
    {
        private readonly List<EntryAnalysis> _analyses;
        public EntryAnalysis? LastAdded { get; private set; }

        public InMemoryEntryAnalysisRepository(List<EntryAnalysis> analyses)
        {
            _analyses = analyses;
        }

        public Task AddAsync(EntryAnalysis analysis)
        {
            LastAdded = analysis;
            return Task.CompletedTask;
        }

        public Task<EntryAnalysis?> GetByTrackedEntryIdAsync(int trackedEntryId)
        {
            return Task.FromResult(_analyses.FirstOrDefault(a => a.EntryId == trackedEntryId));
        }

        public Task<IEnumerable<EntryAnalysis>> ListByDayAsync(DateTime date, TimeZoneInfo? timeZone = null)
        {
            return Task.FromResult<IEnumerable<EntryAnalysis>>(_analyses);
        }

        public Task UpdateAsync(EntryAnalysis analysis)
        {
            LastAdded = analysis;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryTrackedEntryRepository : ITrackedEntryRepository
    {
        private readonly List<TrackedEntry> _entries;

        public InMemoryTrackedEntryRepository(List<TrackedEntry> entries)
        {
            _entries = entries;
        }

        public Task AddAsync(TrackedEntry entry)
        {
            _entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(int entryId)
        {
            throw new NotSupportedException();
        }

        public Task<IEnumerable<TrackedEntry>> GetByDayAsync(DateTime date, TimeZoneInfo? timeZone = null)
        {
            return Task.FromResult<IEnumerable<TrackedEntry>>(_entries);
        }

        public Task<IEnumerable<TrackedEntry>> GetByEntryTypeAndDayAsync(EntryType entryType, DateTime date, TimeZoneInfo? timeZone = null)
        {
            throw new NotSupportedException();
        }

        public Task<TrackedEntry?> GetByIdAsync(int entryId)
        {
            return Task.FromResult(_entries.FirstOrDefault(e => e.EntryId == entryId));
        }

        public Task UpdateAsync(TrackedEntry entry)
        {
            return Task.CompletedTask;
        }

        public Task UpdateEntryTypeAsync(int entryId, EntryType entryType)
        {
            return Task.CompletedTask;
        }

        public Task UpdateProcessingStatusAsync(int entryId, ProcessingStatus status)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingLlmClient : ILLmClient
    {
        public DailySummaryRequest? LastRequest { get; private set; }

        public Task<LlmAnalysisResult> InvokeAnalysisAsync(TrackedEntry entry, LlmRequestContext context, string? existingAnalysisJson = null, string? correction = null)
        {
            throw new NotSupportedException();
        }

        public Task<LlmAnalysisResult> InvokeDailySummaryAsync(DailySummaryRequest summaryRequest, LlmRequestContext context, string? existingSummaryJson = null)
        {
            LastRequest = summaryRequest;
            return Task.FromResult(new LlmAnalysisResult
            {
                Analysis = new EntryAnalysis
                {
                    EntryId = summaryRequest.SummaryEntryId,
                    CapturedAt = DateTime.UtcNow,
                    InsightsJson = "{}"
                }
            });
        }
    }
}
