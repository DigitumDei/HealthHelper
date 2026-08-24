package com.wellnesswingman.di

import com.russhwolf.settings.PreferencesSettings
import com.russhwolf.settings.Settings
import com.wellnesswingman.data.db.DriverFactory
import com.wellnesswingman.domain.checkin.CheckInScheduling
import com.wellnesswingman.domain.checkin.NoOpCheckInScheduling
import com.wellnesswingman.domain.googleexport.GoogleAuthService
import com.wellnesswingman.platform.AudioRecordingService
import com.wellnesswingman.platform.BackgroundExecutionService
import com.wellnesswingman.platform.CameraCaptureOperations
import com.wellnesswingman.platform.CameraCaptureService
import com.wellnesswingman.platform.DesktopBackgroundExecutionService
import com.wellnesswingman.platform.DesktopGoogleAuthService
import com.wellnesswingman.platform.DiagnosticShare
import com.wellnesswingman.platform.FileSystem
import com.wellnesswingman.platform.FileSystemOperations
import com.wellnesswingman.platform.DesktopOnDeviceTranscriptionService
import com.wellnesswingman.platform.OnDeviceTranscriptionService
import com.wellnesswingman.platform.ShareUtil
import com.wellnesswingman.platform.ZipOperations
import com.wellnesswingman.platform.ZipUtil
import org.koin.dsl.bind
import org.koin.dsl.module
import java.util.prefs.Preferences

/**
 * Desktop (JVM) specific Koin module.
 */
val platformModule = module {
    // DriverFactory - no context needed on Desktop
    single { DriverFactory() }

    // Settings - using Java Preferences
    single<Settings> {
        val preferences = Preferences.userRoot().node("com.wellnesswingman")
        PreferencesSettings(preferences)
    }

    // Platform services
    single { FileSystem() } bind FileSystemOperations::class
    single { CameraCaptureService() } bind CameraCaptureOperations::class
    single { AudioRecordingService() }
    single<OnDeviceTranscriptionService> { DesktopOnDeviceTranscriptionService() }
    single { DiagnosticShare() }
    single { ZipUtil() } bind ZipOperations::class
    single { ShareUtil() }

    // Background execution service (no-op on desktop)
    single<BackgroundExecutionService> { DesktopBackgroundExecutionService() }

    // Check-in scheduling (no alarms on desktop; answers are still capturable in-app)
    single<CheckInScheduling> { NoOpCheckInScheduling() }

    // Google Docs export authorization (unavailable on desktop — UI shows unsupported state)
    single<GoogleAuthService> { DesktopGoogleAuthService() }
}
