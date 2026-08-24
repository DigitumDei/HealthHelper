package com.wellnesswingman.di

import com.russhwolf.settings.Settings
import com.wellnesswingman.data.db.DriverFactory
import com.wellnesswingman.data.model.PolarOAuthConfig
import com.wellnesswingman.data.repository.*
import com.wellnesswingman.db.WellnessWingmanDatabase
import org.koin.core.module.dsl.singleOf
import org.koin.dsl.module

/**
 * Koin module for data layer dependencies.
 */
val dataModule = module {
    // Database
    single<WellnessWingmanDatabase> {
        val driver = get<DriverFactory>().createDriver()
        WellnessWingmanDatabase(driver)
    }

    // Repositories
    single<TrackedEntryRepository> {
        SqlDelightTrackedEntryRepository(get())
    }

    single<EntryAnalysisRepository> {
        SqlDelightEntryAnalysisRepository(get())
    }

    single<DailySummaryRepository> {
        SqlDelightDailySummaryRepository(get())
    }

    single<WeeklySummaryRepository> {
        SqlDelightWeeklySummaryRepository(get())
    }

    single<DailyCheckInRepository> {
        SqlDelightDailyCheckInRepository(get())
    }

    single<CheckInAnalysisRepository> {
        SqlDelightCheckInAnalysisRepository(get())
    }

    single<AppSettingsRepository> {
        SettingsAppSettingsRepository(get())
    }

    single<WeightHistoryRepository> {
        SqlDelightWeightHistoryRepository(get())
    }

    single<NutritionalProfileRepository> {
        SqlDelightNutritionalProfileRepository(get())
    }

    single<PolarSyncRepository> {
        SqlDelightPolarSyncRepository(get())
    }

    // Health Chat
    single<HealthChatRepository> {
        SqlDelightHealthChatRepository(get())
    }

    // Polar OAuth
    single { PolarOAuthRepository(get(), get()) }

    // Polar API
    single { PolarApiClient() }

    // Google Docs export
    single { com.wellnesswingman.data.googleexport.GoogleDocsApiClient() }
    single { com.wellnesswingman.data.googleexport.GoogleDocsBatchBuilder() }

    // Settings - platform-specific implementation will be provided
    // via platformModule in each platform's source set
}
