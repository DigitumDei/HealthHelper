package com.wellnesswingman.di

import android.content.Context
import android.content.SharedPreferences
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKeys
import com.russhwolf.settings.Settings
import com.russhwolf.settings.SharedPreferencesSettings
import com.wellnesswingman.data.db.DriverFactory
import com.wellnesswingman.domain.googleexport.GoogleAuthService
import com.wellnesswingman.platform.AndroidBackgroundExecutionService
import com.wellnesswingman.platform.AudioRecordingService
import com.wellnesswingman.platform.AndroidGoogleAuthService
import com.wellnesswingman.platform.BackgroundExecutionService
import com.wellnesswingman.platform.CameraCaptureOperations
import com.wellnesswingman.platform.CameraCaptureService
import com.wellnesswingman.platform.DiagnosticLogger
import com.wellnesswingman.platform.DiagnosticShare
import com.wellnesswingman.platform.FileSystem
import com.wellnesswingman.platform.FileSystemOperations
import com.wellnesswingman.platform.AndroidOnDeviceTranscriptionService
import com.wellnesswingman.platform.OnDeviceTranscriptionService
import com.wellnesswingman.platform.PhotoResizer
import com.wellnesswingman.platform.ShareUtil
import com.wellnesswingman.platform.ZipOperations
import com.wellnesswingman.platform.ZipUtil
import io.github.aakira.napier.Napier
import org.koin.dsl.bind
import org.koin.dsl.module

private const val OLD_PREFS_NAME = "wellnesswingman_prefs"
private const val SECURE_PREFS_NAME = "wellnesswingman_secure_prefs"
private const val MIGRATION_DONE_KEY = "__secure_migration_done"

/**
 * Android-specific Koin module.
 */
val platformModule = module {
    // DriverFactory - requires Android Context
    single { DriverFactory(get<Context>()) }

    // Settings - using EncryptedSharedPreferences with one-time migration
    single<Settings> {
        val context = get<Context>()
        val encryptedPrefs = createEncryptedPrefs(context)
        migrateFromPlainPrefsIfNeeded(context, encryptedPrefs)
        SharedPreferencesSettings(encryptedPrefs)
    }

    // Platform services
    single { FileSystem(get<Context>()) } bind FileSystemOperations::class
    single { CameraCaptureService(get<Context>()) } bind CameraCaptureOperations::class
    single { AudioRecordingService(get<Context>()) }
    single<OnDeviceTranscriptionService> { AndroidOnDeviceTranscriptionService(get<Context>()) }
    single { PhotoResizer() }
    single { DiagnosticLogger(get<Context>()) }
    single { DiagnosticShare(get<Context>(), get()) }

    // ZIP and sharing
    single { ZipUtil() } bind ZipOperations::class
    single { ShareUtil(get<Context>()) }

    // Background execution service
    single<BackgroundExecutionService> { AndroidBackgroundExecutionService(get()) }

    // Google Docs export authorization (Android-only)
    single<GoogleAuthService> { AndroidGoogleAuthService(get<Context>()) }
}

private fun createEncryptedPrefs(context: Context): SharedPreferences {
    val masterKeyAlias = MasterKeys.getOrCreate(MasterKeys.AES256_GCM_SPEC)
    return EncryptedSharedPreferences.create(
        SECURE_PREFS_NAME,
        masterKeyAlias,
        context,
        EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
        EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM
    )
}

/**
 * One-time migration: copy all entries from the old plain SharedPreferences
 * to the new EncryptedSharedPreferences, then clear the old file.
 */
private fun migrateFromPlainPrefsIfNeeded(
    context: Context,
    encryptedPrefs: SharedPreferences
) {
    if (encryptedPrefs.getBoolean(MIGRATION_DONE_KEY, false)) return

    val oldPrefs = context.getSharedPreferences(OLD_PREFS_NAME, Context.MODE_PRIVATE)
    val oldEntries = oldPrefs.all
    if (oldEntries.isEmpty()) {
        // Nothing to migrate — mark done and return
        encryptedPrefs.edit().putBoolean(MIGRATION_DONE_KEY, true).apply()
        return
    }

    Napier.i("Migrating ${oldEntries.size} settings from plain to encrypted preferences")

    val editor = encryptedPrefs.edit()
    for ((key, value) in oldEntries) {
        when (value) {
            is String -> editor.putString(key, value)
            is Int -> editor.putInt(key, value)
            is Long -> editor.putLong(key, value)
            is Float -> editor.putFloat(key, value)
            is Boolean -> editor.putBoolean(key, value)
            is Set<*> -> {
                @Suppress("UNCHECKED_CAST")
                editor.putStringSet(key, value as Set<String>)
            }
        }
    }
    editor.putBoolean(MIGRATION_DONE_KEY, true)
    editor.apply()

    // Clear old preferences
    oldPrefs.edit().clear().apply()
    Napier.i("Settings migration complete, old preferences cleared")
}
