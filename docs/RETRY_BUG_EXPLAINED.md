# The Retry Bug - Simple Explanation

## The Problem

When you tapped a failed meal photo to retry, then navigated to the detail page and back, the photo would show "Failed" again instead of "Processing".

## Why It Happened

**Entity Framework was caching the old data.**

Here's the sequence:

1. You tap retry → code saves `ProcessingStatus = Pending` to database ✅
2. Background service updates database: `Pending → Processing → Completed` ✅
3. You navigate back to main page
4. Code calls `GetByDayAsync()` to reload all meals
5. **EF returns CACHED data instead of querying database** ❌
6. The cached entry still has `ProcessingStatus = Failed`
7. UI shows "Failed" even though database has "Completed"

## The Fix

Added `.AsNoTracking()` to the database query:

```csharp
// Before (broken):
var entries = await _context.TrackedEntries
    .Where(e => e.CapturedAt.Date == date.Date)
    .ToListAsync();
// EF caches these entries!

// After (fixed):
var entries = await _context.TrackedEntries
    .AsNoTracking()  // ← This line!
    .Where(e => e.CapturedAt.Date == date.Date)
    .ToListAsync();
// EF queries the database every time
```

## What `.AsNoTracking()` Does

- Tells EF: "Don't remember these entities, I'm just reading them"
- Forces fresh database reads every time
- Faster (no tracking overhead)
- Perfect for read-only scenarios like displaying a list

## Files Changed

**Modified**:
- `HealthHelper/Data/SqliteTrackedEntryRepository.cs`
  - Added `.AsNoTracking()` to `GetByDayAsync()`
  - Added `.AsNoTracking()` to `GetByIdAsync()`

## Test It

1. Take a photo → wait for it to fail
2. Tap the failed photo to retry
3. **Immediately** navigate to detail page
4. Navigate back to main page
5. **Expected**: Photo shows "Analyzing..." or "✓ Completed"
6. **Before fix**: Would show "Failed ⚠"

## Why This Wasn't Obvious

Entity Framework's change tracking is usually helpful - it makes updates easier. But in this app:

- Multiple DbContext scopes exist (ViewModel scope, Background service scope)
- We reload the entire list on every navigation
- We need FRESH data every time, not cached data

This is a common gotcha in EF Core when working with async background updates!

## Bonus Fixes Also Included

1. **Stale entry recovery** - Entries stuck in "Processing" reset on app restart
2. **Cancellation support** - Background tasks can be cancelled cleanly
3. **Service lifetime fix** - Fixed dependency injection scope issues

See `AsyncProcessingFixes.md` for full details.
