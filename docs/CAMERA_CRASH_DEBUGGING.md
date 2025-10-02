# Camera Crash Debugging Guide

## Problem

App closes/crashes when taking a photo, but no exceptions appear in logs. App restarts multiple times.

## Analysis of Provided Logs

Looking at your logs, I can see:

1. **Successful photo capture at 19:20:48** - Entry ID 12 was created and processed
2. **Multiple app restarts** - The app restarted 4-5 times (notice repeated migration checks)
3. **No crash exceptions logged** - This means the crash happens before logging can capture it

## Most Likely Causes

### 1. Camera Activity Lifecycle Issue (Android)

**Symptoms**:
- App closes immediately after camera button
- Works sometimes, fails other times
- No exception in logs

**Root Cause**: When the Android camera activity launches, your app goes to background. If the OS kills your app process for memory, it crashes silently when camera returns.

**What to check in logs**:
Look for this sequence MISSING:
```
[INFO] TakePhotoButton_Clicked: Starting photo capture
[INFO] TakePhotoButton_Clicked: Launching camera
[INFO] TakePhotoButton_Clicked: User cancelled photo capture  <-- Should see this OR...
[INFO] TakePhotoButton_Clicked: Photo captured, saving to disk
```

**If you see**:
```
[INFO] TakePhotoButton_Clicked: Launching camera
<app restarts>
```
= Camera killed your app process

### 2. Memory Pressure

**Symptoms**:
- Crashes more often after using app for a while
- Multiple photos work, then suddenly crashes
- More common on lower-end devices

**Root Cause**: Camera bitmaps use lots of memory. Android kills background apps to free RAM.

### 3. Photo File I/O Exception

**Symptoms**:
- Crash right after photo is taken
- Log shows "Photo captured, saving to disk" then stops

**What to check**:
```
[INFO] TakePhotoButton_Clicked: Photo captured, saving to disk
[INFO] TakePhotoButton_Clicked: Photo saved, creating database entry  <-- Missing = file I/O crash
```

### 4. Background Thread Exception

**Symptoms**:
- Crash during "Processing"
- StatusChanged event throws exception

**Root Cause**: Unobserved task exception in background analysis service.

## New Logging Added

I've added comprehensive logging to `TakePhotoButton_Clicked`:

```csharp
[INFO] TakePhotoButton_Clicked: Starting photo capture
[INFO] TakePhotoButton_Clicked: Launching camera
[INFO] TakePhotoButton_Clicked: Photo captured, saving to disk
[INFO] TakePhotoButton_Clicked: Photo saved, creating database entry
[INFO] TakePhotoButton_Clicked: Database entry created with ID 12
[INFO] TakePhotoButton_Clicked: Adding entry to UI
[INFO] TakePhotoButton_Clicked: Queueing background analysis
[INFO] TakePhotoButton_Clicked: Photo capture completed successfully
```

## How to Debug with New Logs

### Test 1: Identify Crash Point

1. Rebuild and deploy app
2. Take a photo
3. Check logs immediately
4. Find the LAST log message before crash

**If last message is**:
- `"Launching camera"` → Camera activity killed your app (see Fix 1)
- `"Photo captured, saving to disk"` → File I/O issue (see Fix 2)
- `"Adding entry to UI"` → UI threading issue (see Fix 3)
- `"Queueing background analysis"` → Background service crash (see Fix 4)
- `"Photo capture completed successfully"` → Crash happens AFTER photo flow (see Fix 5)

### Test 2: Check for Exceptions

Look for this pattern:
```
[ERROR] TakePhotoButton_Clicked: FATAL ERROR during photo capture
  Exception: [exception details]
```

**If you see this** → The crash is happening inside `TakePhotoButton_Clicked` and we have the exception!

### Test 3: Check Background Service

Look for:
```
[ERROR] Background analysis failed for entry 12
  Exception: [exception details]
```

**If you see this** → The background LLM call is crashing

### Test 4: Check Unobserved Task Exceptions

Look for:
```
[ERROR] Unobserved task exception
  Exception: [exception details]
```

**If you see this** → A background task crashed without being awaited

## Fixes Based on Test Results

### Fix 1: Camera Activity Killed App

**Problem**: Android killed your app when camera launched.

**Solution**: Add state preservation in `AndroidManifest.xml`:

```xml
<application android:allowBackup="true">
  <activity
    android:name="com.companyname.healthhelper.MainActivity"
    android:configChanges="orientation|screenSize|keyboardHidden"
    android:launchMode="singleTask">
  </activity>
</application>
```

### Fix 2: File I/O Exception

**Problem**: Can't write to app data directory.

**Check permissions in logs**:
```
[WARN] TakePhotoButton_Clicked: Camera permissions denied
```

**Solution**: Verify storage permissions on Android API < 33.

### Fix 3: UI Threading Exception

**Problem**: Trying to update UI from background thread.

**Already fixed**: Added `MainThread.InvokeOnMainThreadAsync` for error alerts.

### Fix 4: Background Service Crash

**Check BackgroundAnalysisService logs**:
```
[ERROR] Background analysis failed for entry X
```

**Solution**: Add try-catch in `QueueEntryAsync` (already done).

### Fix 5: Post-Capture Crash

**If crash happens AFTER "Photo capture completed successfully"**:

Look for:
```
[INFO] Photo capture completed successfully
[INFO] Analysis cancelled before starting for entry 12  <-- Background starting
<crash>
```

**Likely cause**: StatusChanged event handler crashing.

**Check `OnEntryStatusChanged` in MainPage.xaml.cs**:
- Verify `BindingContext` isn't null
- Verify page isn't disposed

## Additional Debugging

### Enable Verbose EF Logging

Add to `MauiProgram.cs`:

```csharp
builder.Logging.SetMinimumLevel(LogLevel.Trace);
```

This will show ALL SQL queries and help identify database locks.

### Enable MAUI Verbose Logging

Add to project file:

```xml
<PropertyGroup>
  <MauiEnablePlatformUsings>true</MauiEnablePlatformUsings>
  <AndroidEnableProfiledAot>false</AndroidEnableProfiledAot>
</PropertyGroup>
```

### Android Logcat

If still no logs, check Android logcat for native crashes:

```bash
adb logcat | grep -i "healthhelper\|crash\|fatal"
```

Look for:
- `FATAL EXCEPTION`
- `AndroidRuntime`
- `Process: com.companyname.healthhelper`

## What to Report Back

After testing with new logging, provide:

1. **Last log message before crash**:
   ```
   [INFO] TakePhotoButton_Clicked: <what was the last message?>
   ```

2. **Any exceptions logged**:
   ```
   [ERROR] <full exception with stack trace>
   ```

3. **Frequency**:
   - Does it crash every time?
   - Only after X photos?
   - Only on specific device?

4. **Device info**:
   - Android version
   - Device model
   - Available RAM

5. **Full log from app start to crash** (even if it looks empty!)

## Known MAUI Issues

### MediaPicker Crash on Android 13+

**Symptoms**: Crash when returning from camera on Android 13+

**Fix**: Ensure you're using latest MAUI version:
```xml
<PackageReference Include="Microsoft.Maui.Controls" Version="8.0.82" />
```

### Camera Permission Regression

**Symptoms**: Works on first run, crashes on subsequent runs

**Fix**: Add to `AndroidManifest.xml`:
```xml
<uses-permission android:name="android.permission.CAMERA" />
<uses-permission android:name="android.permission.WRITE_EXTERNAL_STORAGE" android:maxSdkVersion="32" />
```

## Summary

The new logging will pinpoint exactly where the crash occurs. Without exceptions in the logs, it's likely a:
1. Process kill by Android OS (camera activity)
2. Native crash (MAUI/Android layer)
3. Unobserved task exception

Run the app again and check which log message is LAST before crash. That tells us exactly where to fix it.
