# Async Photo Processing with UI Status Indicators

## Problem Statement

Currently, when a user captures a meal photo, there is a long period where the UI is frozen with no feedback while:
1. The photo is being saved to disk
2. The entry is written to the database
3. The LLM API call is made and processed (can take several seconds)
4. The analysis is validated and saved
5. The UI refreshes to show the new photo

This creates a poor user experience with no indication of progress. The photo should appear immediately with a "processing" indicator, and become interactive only after the LLM analysis completes.

## Goals

1. **Immediate Feedback**: Photo appears in the UI within milliseconds of capture
2. **Clear Status**: Visual indicator shows the entry is being processed
3. **Non-blocking UI**: User can continue using the app while analysis runs in background
4. **Progressive Enhancement**: Entry becomes clickable when analysis completes
5. **Error Resilience**: Failed analyses are indicated clearly and can be retried

## Scope Decisions (Current Iteration)

- **Platform**: Target Android first; other platforms remain synchronous until follow-up work.
- **Android support window**: Use Jetpack WorkManager so the queue functions reliably down to API 21 (Android 5.0), which matches the .NET MAUI default minimum.
- **Retries**: User-initiated only. Automatic retries/backoff are deferred.
- **Metrics**: Postpone collection of processing-time or failure metrics.
- **Queue depth**: No artificial limit in this iteration; document potential throttling instead.

## Current Flow (Synchronous)

```
User clicks "Take Photo"
  ↓
Camera captures photo
  ↓
Save photo to disk
  ↓
Create TrackedEntry in database
  ↓
[UI BLOCKS HERE]
Call LLM API (3-10 seconds)
  ↓
Validate response
  ↓
Save EntryAnalysis to database
  ↓
[UI UNBLOCKS]
Refresh entire meal list from database
  ↓
Photo appears in UI
```

**File**: `HealthHelper/Pages/MainPage.xaml.cs:35-107` (`TakePhotoButton_Clicked`)

## Desired Flow (Asynchronous)

```
User clicks "Take Photo"
  ↓
Camera captures photo
  ↓
Save photo to disk
  ↓
Create TrackedEntry in database with ProcessingStatus = "Pending"
  ↓
Add entry to UI immediately with "Processing" indicator
  ↓
Queue background task for LLM analysis
  ↓
[User can continue using app]
  ↓
Background: Call LLM API
  ↓
Background: Validate response
  ↓
Background: Save EntryAnalysis to database
  ↓
Background: Update TrackedEntry.ProcessingStatus = "Completed"
  ↓
Background: Notify UI to update specific entry
  ↓
UI: Update entry to show "Completed" state and enable clicking
```

## Implementation Tasks

### 1. Database Schema Changes

#### 1.1 Add ProcessingStatus to TrackedEntry Model
**File**: `HealthHelper/Models/TrackedEntry.cs`

Add a new property to track analysis processing state:
```csharp
public ProcessingStatus ProcessingStatus { get; set; } = ProcessingStatus.Pending;
```

Create enum for processing states:
```csharp
public enum ProcessingStatus
{
    Pending = 0,      // Entry created, analysis not started
    Processing = 1,   // Analysis in progress
    Completed = 2,    // Analysis finished successfully
    Failed = 3,       // Analysis failed (can be retried)
    Skipped = 4       // Analysis skipped (no API key, unsupported provider, etc.)
}
```

**Considerations**:
- Should we store error details for failed analyses?
- Consider adding `ProcessingStartedAt` and `ProcessingCompletedAt` timestamps for monitoring
- Decide how the migration backfills existing data. The TODO assumes historical rows are truly "complete"; if any past analysis failed silently, we may want to log and review before force-marking them as `Completed`.

#### 1.2 Create Database Migration
**File**: `HealthHelper/Migrations/[timestamp]_AddProcessingStatusToTrackedEntry.cs`

- Add `ProcessingStatus` column (integer) with default value 0 (Pending)
- Existing entries should default to `Completed` since they were processed before this feature
- Update `HealthHelperDbContextModelSnapshot.cs`

### 2. UI Model Changes

#### 2.1 Update MealPhoto Model
**File**: `HealthHelper/Models/MealPhoto.cs`

Add processing status to the UI model:
```csharp
public class MealPhoto
{
    public int EntryId { get; init; }
    public string FullPath { get; init; }
    public string Description { get; init; }
    public DateTime CapturedAt { get; init; }
    public ProcessingStatus ProcessingStatus { get; set; } // NEW
    public bool IsClickable => ProcessingStatus == ProcessingStatus.Completed; // NEW

    // Constructor needs updating
}
```

**Considerations**:
- Should we make this implement `INotifyPropertyChanged` for live updates?
- Or use `ObservableObject` from CommunityToolkit.Mvvm?
- If we move to `ObservableObject`, we need to ensure collection updates remain on the UI thread (existing `MainThread` usage will still apply) and update constructors/factory helpers accordingly.

### 3. ViewModel Updates

#### 3.1 Update MealLogViewModel
**File**: `HealthHelper/PageModels/MealLogViewModel.cs`

**Changes needed**:
1. Add method to insert a single entry without full reload:
   ```csharp
   public async Task AddPendingEntryAsync(TrackedEntry entry)
   {
       var mealPayload = (MealPayload)entry.Payload!;
       var fullPath = Path.Combine(FileSystem.AppDataDirectory, entry.BlobPath!);
       var mealPhoto = new MealPhoto(
           entry.EntryId,
           fullPath,
           mealPayload.Description ?? string.Empty,
           entry.CapturedAt,
           entry.ProcessingStatus); // NEW parameter

       await MainThread.InvokeOnMainThreadAsync(() =>
       {
           Meals.Insert(0, mealPhoto); // Add to top of list
       });
   }
   ```

2. Add method to update status of existing entry:
   ```csharp
   public async Task UpdateEntryStatusAsync(int entryId, ProcessingStatus newStatus)
   {
       await MainThread.InvokeOnMainThreadAsync(() =>
       {
           var existingEntry = Meals.FirstOrDefault(m => m.EntryId == entryId);
           if (existingEntry is not null)
           {
               existingEntry.ProcessingStatus = newStatus;
               // If using ObservableObject, this will auto-update UI
               // Otherwise may need to remove and re-add to ObservableCollection
           }
       });
   }
   ```

3. Update `LoadEntriesAsync` to include ProcessingStatus:
   ```csharp
   return new MealPhoto(
       entry.EntryId,
       fullPath,
       mealPayload.Description ?? string.Empty,
       entry.CapturedAt,
       entry.ProcessingStatus); // NEW
   ```

4. Update `GoToMealDetail` to check IsClickable:
   ```csharp
   private async Task GoToMealDetail(MealPhoto meal)
   {
       if (!meal.IsClickable)
       {
           _logger.LogInformation("Entry {EntryId} is not yet ready for viewing.", meal.EntryId);
           await Shell.Current.DisplayAlertAsync(
               "Still Processing",
               "This meal is still being analyzed. Please wait a moment.",
               "OK");
           return;
       }
       // ... existing navigation code
   }
   ```

**Considerations**:
- Should we poll for status updates, or use events/callbacks?
- How do we handle the case where app is closed during processing?

### 4. Background Processing

#### 4.1 Create Background Analysis Service
**File**: `HealthHelper/Services/Analysis/BackgroundAnalysisService.cs`

Create a service to manage background analysis tasks:

```csharp
public interface IBackgroundAnalysisService
{
    /// <summary>
    /// Queue an entry for background analysis
    /// </summary>
    Task QueueEntryAsync(int entryId);

    /// <summary>
    /// Event raised when an entry's processing status changes
    /// </summary>
    event EventHandler<EntryStatusChangedEventArgs>? StatusChanged;
}

public class BackgroundAnalysisService : IBackgroundAnalysisService
{
    private readonly ITrackedEntryRepository _entryRepository;
    private readonly IAnalysisOrchestrator _orchestrator;
    private readonly ILogger<BackgroundAnalysisService> _logger;

    public event EventHandler<EntryStatusChangedEventArgs>? StatusChanged;

    public async Task QueueEntryAsync(int entryId)
    {
        // Run on background thread
        _ = Task.Run(async () =>
        {
            try
            {
                // Update status to Processing
                await UpdateStatusAsync(entryId, ProcessingStatus.Processing);

                // Get entry from database
                var entry = await _entryRepository.GetByIdAsync(entryId);
                if (entry is null)
                {
                    _logger.LogWarning("Entry {EntryId} not found for analysis.", entryId);
                    return;
                }

                // Run analysis
                var result = await _orchestrator.ProcessEntryAsync(entry);

                // Update status based on result
                var finalStatus = result.IsQueued
                    ? ProcessingStatus.Completed
                    : (result.RequiresCredentials
                        ? ProcessingStatus.Skipped
                        : ProcessingStatus.Failed);

                await UpdateStatusAsync(entryId, finalStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background analysis failed for entry {EntryId}.", entryId);
                await UpdateStatusAsync(entryId, ProcessingStatus.Failed);
            }
        });
    }

    private async Task UpdateStatusAsync(int entryId, ProcessingStatus status)
    {
        // Update database
        await _entryRepository.UpdateProcessingStatusAsync(entryId, status);

        // Raise event for UI
        StatusChanged?.Invoke(this, new EntryStatusChangedEventArgs(entryId, status));
    }
}

public class EntryStatusChangedEventArgs : EventArgs
{
    public int EntryId { get; }
    public ProcessingStatus Status { get; }

    public EntryStatusChangedEventArgs(int entryId, ProcessingStatus status)
    {
        EntryId = entryId;
        Status = status;
    }
}
```

**Considerations**:
- Determine whether `StatusChanged` should marshal back to the UI thread or expose context info so the subscriber can decide. Today the sample event fires on a ThreadPool thread.
- Confirm the service lifetime (likely `Singleton`) and ensure we unsubscribe from the event in `MainPage` to avoid leaks across page recreations.
- Use a proper background task queue (like `IHostedService` with `Channel<T>`) for production.
- Rate limiting and queue depth controls are out of scope for this iteration but should be revisited if API throttling surfaces in testing.
- Handle app lifecycle (what if app is closed during processing?).
- Persist queue to disk so tasks survive app restarts?

#### 4.2 Update ITrackedEntryRepository
**File**: `HealthHelper/Data/ITrackedEntryRepository.cs` and implementations

Add methods to update processing status:
```csharp
Task UpdateProcessingStatusAsync(int entryId, ProcessingStatus status);
Task<TrackedEntry?> GetByIdAsync(int entryId);
```

Implementation in `SqliteTrackedEntryRepository.cs`:
```csharp
public async Task UpdateProcessingStatusAsync(int entryId, ProcessingStatus status)
{
    var entry = await _context.TrackedEntries.FindAsync(entryId);
    if (entry is not null)
    {
        entry.ProcessingStatus = status;
        await _context.SaveChangesAsync();
    }
}

public async Task<TrackedEntry?> GetByIdAsync(int entryId)
{
    var entry = await _context.TrackedEntries.FindAsync(entryId);
    if (entry is not null)
    {
        DeserializePayload(entry);
    }
    return entry;
}
```

#### 4.3 Service Registration & Lifetime

- Register `IBackgroundAnalysisService` as a singleton in `MauiProgram`. Scoped lifetimes can lead to multiple queues and duplicate work if the page is recreated.
- Ensure any dependencies used inside the background work (e.g., repositories, orchestrator) are resolved from a scope that is created per queued item so DbContext disposal is deterministic. One option is to inject an `IServiceScopeFactory` into the service and create scopes inside `QueueEntryAsync`.
- Capture a `CancellationToken` tied to app shutdown so we can stop accepting new work when the host is disposing.

### 5. Wire Up Background Processing

#### 5.1 Update MainPage Photo Capture Flow
**File**: `HealthHelper/Pages/MainPage.xaml.cs`

Refactor `TakePhotoButton_Clicked` to be non-blocking:

```csharp
private async void TakePhotoButton_Clicked(object sender, EventArgs e)
{
    try
    {
        if (!await EnsureCameraPermissionsAsync())
        {
            return;
        }

        if (!MediaPicker.Default.IsCaptureSupported)
        {
            await DisplayAlertAsync("Not Supported", "Camera is not available on this device.", "OK");
            return;
        }

        FileResult? photo = await MediaPicker.Default.CapturePhotoAsync();

        if (photo is null)
        {
            return;
        }

        // Save photo to disk
        string mealPhotosDir = Path.Combine(FileSystem.AppDataDirectory, "Entries", "Meal");
        Directory.CreateDirectory(mealPhotosDir);
        string uniqueFileName = $"{Guid.NewGuid()}.jpg";
        string persistentFilePath = Path.Combine(mealPhotosDir, uniqueFileName);

        await using (Stream sourceStream = await photo.OpenReadAsync())
        {
            await using (FileStream localFileStream = File.Create(persistentFilePath))
            {
                await sourceStream.CopyToAsync(localFileStream);
            }
        }

        // Create entry with Pending status
        var newEntry = new Models.TrackedEntry
        {
            EntryType = "Meal",
            CapturedAt = DateTime.UtcNow,
            BlobPath = Path.Combine("Entries", "Meal", uniqueFileName),
            Payload = new Models.MealPayload { Description = "New meal photo" },
            DataSchemaVersion = 1,
            ProcessingStatus = ProcessingStatus.Pending // NEW
        };

        // Save to database
        await _trackedEntryRepository.AddAsync(newEntry);

        // Add to UI immediately
        if (BindingContext is MealLogViewModel vm)
        {
            await vm.AddPendingEntryAsync(newEntry);
        }

        // Queue for background processing (non-blocking)
        await _backgroundAnalysisService.QueueEntryAsync(newEntry.EntryId);

        // No longer wait for analysis or reload entire list!
    }
    catch (Exception ex)
    {
        await DisplayAlertAsync("Error", $"An error occurred: {ex.Message}", "OK");
    }
}
```

**Key changes**:
- Set `ProcessingStatus = ProcessingStatus.Pending` on new entry
- Call `vm.AddPendingEntryAsync(newEntry)` instead of `vm.LoadEntriesAsync()`
- Call `_backgroundAnalysisService.QueueEntryAsync()` to start background work
- Remove synchronous wait for analysis result
- Consider making the handler return `Task` (e.g., `private async Task TakePhotoAsync(...)`) and wiring it with `Command` to avoid `async void` swallow-on-crash behavior and to simplify testing.
- Pass a `CancellationToken` from a page-level source into `_backgroundAnalysisService.QueueEntryAsync` so pending work cancels cleanly when the UI is torn down.

#### 5.2 Subscribe to Status Updates
**File**: `HealthHelper/Pages/MainPage.xaml.cs`

Subscribe to background service events in constructor:

```csharp
private readonly IBackgroundAnalysisService _backgroundAnalysisService;

public MainPage(
    MealLogViewModel viewModel,
    ITrackedEntryRepository trackedEntryRepository,
    IAnalysisOrchestrator analysisOrchestrator,
    IBackgroundAnalysisService backgroundAnalysisService) // NEW
{
    InitializeComponent();
    BindingContext = viewModel;
    _trackedEntryRepository = trackedEntryRepository;
    _analysisOrchestrator = analysisOrchestrator;
    _backgroundAnalysisService = backgroundAnalysisService;

    // Subscribe to status changes
    _backgroundAnalysisService.StatusChanged += OnEntryStatusChanged;
}

private async void OnEntryStatusChanged(object? sender, EntryStatusChangedEventArgs e)
{
    if (BindingContext is MealLogViewModel vm)
    {
        await vm.UpdateEntryStatusAsync(e.EntryId, e.Status);
    }
}
```

Don't forget to unsubscribe in page disposal/cleanup.

#### 5.3 Register Background Service
**File**: `HealthHelper/MauiProgram.cs`

```csharp
builder.Services.AddSingleton<IBackgroundAnalysisService, BackgroundAnalysisService>();
```

**Note**: Using `Singleton` so events persist across page navigations.

### 6. UI Visual Updates

#### 6.1 Update XAML Template
**File**: `HealthHelper/Pages/MainPage.xaml`

Update the `CollectionView.ItemTemplate` to show processing status:

```xaml
<DataTemplate x:DataType="models:MealPhoto">
    <Border StrokeThickness="1"
            Stroke="{AppThemeBinding Light={StaticResource Gray200}, Dark={StaticResource Gray600}}"
            Padding="0"
            Margin="0,0,0,16"
            BackgroundColor="Transparent"
            StrokeShape="RoundRectangle 12,12,12,12">

        <Grid RowDefinitions="Auto,Auto" BackgroundColor="Transparent">
            <!-- Existing image -->
            <Image Source="{Binding FullPath}"
                   Aspect="AspectFill"
                   HeightRequest="220"
                   SemanticProperties.Description="Meal photo" />

            <!-- Processing overlay (only visible when not completed) -->
            <Grid Grid.Row="0"
                  BackgroundColor="#80000000"
                  IsVisible="{Binding IsClickable, Converter={StaticResource InvertedBoolConverter}}">
                <VerticalStackLayout HorizontalOptions="Center"
                                     VerticalOptions="Center"
                                     Spacing="8">
                    <ActivityIndicator IsRunning="True"
                                       Color="White"
                                       IsVisible="{Binding ProcessingStatus, Converter={StaticResource IsProcessingConverter}}" />
                    <Label Text="Analyzing..."
                           TextColor="White"
                           FontAttributes="Bold"
                           IsVisible="{Binding ProcessingStatus, Converter={StaticResource IsProcessingConverter}}" />
                    <Label Text="Analysis Failed - Tap to Retry"
                           TextColor="Orange"
                           FontAttributes="Bold"
                           IsVisible="{Binding ProcessingStatus, Converter={StaticResource IsFailedConverter}}" />
                    <Label Text="Analysis Skipped"
                           TextColor="Gray"
                           FontAttributes="Bold"
                           IsVisible="{Binding ProcessingStatus, Converter={StaticResource IsSkippedConverter}}" />
                </VerticalStackLayout>
            </Grid>

            <!-- Existing bottom info panel -->
            <VerticalStackLayout Padding="16" VerticalOptions="Start" Grid.Row="1" Spacing="4">
                <Label Text="Meal captured"
                       Style="{StaticResource Body2Strong}" />
                <Label Text="{Binding CapturedAt, StringFormat='Captured {0:MMM d, h:mm tt}'}"
                       Style="{StaticResource Caption1}" />
            </VerticalStackLayout>
        </Grid>
    </Border>
</DataTemplate>
```

#### 6.2 Create Value Converters
**File**: `HealthHelper/Converters/ProcessingStatusConverters.cs`

```csharp
public class InvertedBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return !(bool)value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return !(bool)value;
    }
}

public class IsProcessingConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is ProcessingStatus status &&
               (status == ProcessingStatus.Pending || status == ProcessingStatus.Processing);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class IsFailedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is ProcessingStatus status && status == ProcessingStatus.Failed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class IsSkippedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is ProcessingStatus status && status == ProcessingStatus.Skipped;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
```

Register converters in App.xaml resources.

### 7. Retry Failed Analyses

#### 7.1 Add Retry Command to MealLogViewModel
**File**: `HealthHelper/PageModels/MealLogViewModel.cs`

```csharp
[RelayCommand]
private async Task RetryAnalysis(MealPhoto meal)
{
    if (meal.ProcessingStatus != ProcessingStatus.Failed)
    {
        return;
    }

    _logger.LogInformation("Retrying analysis for entry {EntryId}.", meal.EntryId);

    // Update to pending status
    meal.ProcessingStatus = ProcessingStatus.Pending;

    // Queue for processing
    await _backgroundAnalysisService.QueueEntryAsync(meal.EntryId);
}
```

Retries remain user initiated only; if we later add automated retries we will need additional state (attempt counts, backoff timers).

#### 7.2 Update CollectionView SelectionChanged
**File**: `HealthHelper/Pages/MainPage.xaml.cs`

```csharp
private async void MealsCollection_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    if (BindingContext is not MealLogViewModel vm)
    {
        return;
    }

    if (e.CurrentSelection.FirstOrDefault() is not MealPhoto selectedMeal)
    {
        return;
    }

    // Check if this is a failed entry that can be retried
    if (selectedMeal.ProcessingStatus == ProcessingStatus.Failed)
    {
        await vm.RetryAnalysisCommand.ExecuteAsync(selectedMeal);
    }
    else if (selectedMeal.IsClickable)
    {
        await vm.GoToMealDetailCommand.ExecuteAsync(selectedMeal);
    }
    // else: do nothing for pending/processing entries

    if (sender is CollectionView collectionView)
    {
        collectionView.SelectedItem = null;
    }
}
```

## Edge Cases & Considerations

### App Lifecycle
- **App closed during processing**: Entry remains in "Processing" state
  - Solution: On app startup, check for stale "Processing" entries and reset to "Pending", then re-queue
  - Add `ProcessingStartedAt` timestamp to detect stale entries

### Network Failures
- **LLM API timeout or network error**: Entry marked as "Failed"
  - User can tap to retry
  - Consider exponential backoff for automatic retries

### Rapid Photo Capture
- **User takes multiple photos quickly**: All should appear immediately with processing indicators
  - Queue should handle multiple concurrent tasks
  - Consider rate limiting to avoid API throttling

### Database Consistency
- **Entry exists but no analysis**: This is now normal (Pending state)
  - MealDetailPage should handle entries without analysis gracefully
  - Show "Analysis in progress" message instead of "No analysis available"

### Migration of Existing Data
- **Existing entries in database**: Should be marked as "Completed" by default
  - Migration should set `ProcessingStatus = 2` (Completed) for all existing rows
  - New entries default to `ProcessingStatus = 0` (Pending)

## Platform Constraints & Deployment Notes

- **Android (current focus)**: Integrate with Jetpack WorkManager so queued items finish even if the activity is backgrounded. WorkManager supports API 21+, aligning with our scope decision. Document the need to initialize WorkManager from the MAUI head project.
- **iOS**: Out of scope for this iteration; current synchronous flow remains. Keep a note that `BGProcessingTaskRequest` is the likely future path to resume work when suspended.
- **Desktop (Win/Mac)**: No special scheduling APIs required, but confirm `Share.RequestAsync` behaves the same and handle file-system permissions (especially on Mac sandbox).
- **Permissions**: Camera/storage permissions can be revoked while processing; background tasks should surface a friendly message and mark the entry as `Failed` or `Skipped`.
- **Telemetry & PII**: Reuse existing logging guardrails so queued work never persists raw photos or prompts in plain text logs.

## Testing Checklist

- [ ] Take photo - appears immediately in UI with processing indicator
- [ ] Processing indicator shows activity spinner
- [ ] After analysis completes, indicator disappears and entry becomes clickable
- [ ] Clicking during processing shows "Still Processing" alert
- [ ] Clicking after completion navigates to detail page
- [ ] Failed analysis shows error state
- [ ] Clicking failed entry retries analysis
- [ ] Take multiple photos rapidly - all appear immediately
- [ ] Close app during processing - entry still completes when app reopens
- [ ] No API key configured - entry marked as "Skipped"
- [ ] Network failure - entry marked as "Failed"
- [ ] Existing entries (pre-migration) still work and are clickable

## Performance Considerations

- **Memory**: ObservableCollection updates should be efficient (single item updates vs. full reload)
- **Database**: Add index on `ProcessingStatus` if querying by status becomes common
- **UI Thread**: All status updates must use `MainThread.InvokeOnMainThreadAsync`
- **Background Tasks**: Ensure tasks are properly cancelled if app is disposed

## Future Enhancements

1. **Push Notifications**: Notify user when analysis completes if app is backgrounded
2. **Batch Processing**: Process multiple pending entries in queue order
3. **Priority Queue**: Allow user to "bump" an entry to front of queue
4. **Analytics**: Track processing times and failure rates
5. **Offline Support**: Queue entries for analysis when network is restored
6. **Background Fetch**: Use platform background fetch APIs for iOS/Android

## Open Questions

- If we later sync entries across devices, how will processing state propagate and resolve conflicts?
- When we bring this flow to additional platforms, can we reuse the WorkManager-centric abstraction or do we need separate service implementations?

## Files to Create/Modify Summary

### New Files
- `HealthHelper/Models/ProcessingStatus.cs` - Enum
- `HealthHelper/Services/Analysis/BackgroundAnalysisService.cs` - Background service
- `HealthHelper/Services/Analysis/EntryStatusChangedEventArgs.cs` - Event args
- `HealthHelper/Converters/ProcessingStatusConverters.cs` - UI converters
- `HealthHelper/Migrations/[timestamp]_AddProcessingStatusToTrackedEntry.cs` - Migration
- `HealthHelper/Migrations/[timestamp]_AddProcessingStatusToTrackedEntry.Designer.cs` - Migration designer

### Modified Files
- `HealthHelper/Models/TrackedEntry.cs` - Add ProcessingStatus property
- `HealthHelper/Models/MealPhoto.cs` - Add ProcessingStatus and IsClickable
- `HealthHelper/PageModels/MealLogViewModel.cs` - Add AddPendingEntryAsync, UpdateEntryStatusAsync, RetryAnalysisCommand
- `HealthHelper/Data/ITrackedEntryRepository.cs` - Add UpdateProcessingStatusAsync, GetByIdAsync
- `HealthHelper/Data/SqliteTrackedEntryRepository.cs` - Implement new methods
- `HealthHelper/Pages/MainPage.xaml.cs` - Refactor photo capture, subscribe to events
- `HealthHelper/Pages/MainPage.xaml` - Update UI template with processing indicators
- `HealthHelper/MauiProgram.cs` - Register BackgroundAnalysisService
- `HealthHelper/App.xaml` - Register value converters
- `HealthHelper/Migrations/HealthHelperDbContextModelSnapshot.cs` - Update snapshot

## Estimated Effort

- **Schema & Models**: 1-2 hours
- **Background Service**: 2-3 hours
- **ViewModel Updates**: 2-3 hours
- **UI Updates**: 2-3 hours
- **Testing & Refinement**: 3-4 hours
- **Total**: ~10-15 hours

## Dependencies

- CommunityToolkit.Mvvm (already in project)
- System.Threading.Tasks
- System.Collections.ObjectModel
- Entity Framework Core (already in project)
