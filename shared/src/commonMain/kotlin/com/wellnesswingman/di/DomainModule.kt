package com.wellnesswingman.di

import com.wellnesswingman.domain.analysis.AnalysisOrchestrator
import com.wellnesswingman.domain.analysis.BackgroundAnalysisService
import com.wellnesswingman.domain.analysis.DailySummaryService
import com.wellnesswingman.domain.analysis.WeeklySummaryService
import com.wellnesswingman.domain.chat.HealthChatService
import com.wellnesswingman.domain.analysis.DailyTotalsCalculator
import com.wellnesswingman.domain.analysis.DefaultBackgroundAnalysisService
import com.wellnesswingman.domain.analysis.DefaultStaleEntryRecoveryService
import com.wellnesswingman.domain.analysis.NutritionLabelAnalyzer
import com.wellnesswingman.domain.analysis.NutritionLabelAnalyzing
import com.wellnesswingman.domain.analysis.StaleEntryRecoveryService
import com.wellnesswingman.domain.capture.PendingCaptureStore
import com.wellnesswingman.domain.capture.PhotoEntryProcessor
import com.wellnesswingman.domain.capture.TextEntryProcessor
import com.wellnesswingman.domain.events.DefaultStatusChangeNotifier
import com.wellnesswingman.domain.events.StatusChangeNotifier
import com.wellnesswingman.domain.llm.LlmClientFactory
import com.wellnesswingman.domain.llm.ToolRegistry
import com.wellnesswingman.domain.migration.DataMigrationService
import com.wellnesswingman.domain.migration.DefaultDataMigrationService
import com.wellnesswingman.domain.navigation.CalendarNavigationService
import com.wellnesswingman.domain.navigation.HistoricalNavigationContext
import com.wellnesswingman.domain.checkin.CheckInAnalysisService
import com.wellnesswingman.domain.checkin.DayCheckInsProvider
import com.wellnesswingman.domain.checkin.PendingCheckInStore
import com.wellnesswingman.domain.oauth.PendingOAuthResultStore
import com.wellnesswingman.domain.report.HealthReportBuilder
import com.wellnesswingman.domain.polar.PolarInsightService
import com.wellnesswingman.domain.polar.PolarSyncOrchestrator
import org.koin.core.module.dsl.bind
import org.koin.core.module.dsl.singleOf
import org.koin.dsl.module

/**
 * Koin module for domain layer dependencies (business logic).
 */
val domainModule = module {
    // Calculators and utilities
    singleOf(::DailyTotalsCalculator)

    // Capture recovery
    singleOf(::PendingCaptureStore)

    // Idempotent photo entry processing (app-scoped; survives configuration changes)
    singleOf(::PhotoEntryProcessor)

    // Entries described rather than photographed (app-scoped for the same reason)
    single {
        TextEntryProcessor(
            trackedEntryRepository = get(),
            backgroundAnalysisService = get(),
            llmClientFactory = get()
        )
    }

    // Check-ins
    singleOf(::PendingCheckInStore)
    single { DayCheckInsProvider(get(), get(), get()) }
    // Holds its own application-level scope: extraction has to outlive the screen that
    // triggered it, since saving a check-in is normally followed by closing that screen.
    single {
        CheckInAnalysisService(
            checkInAnalysisRepository = get(),
            dailyCheckInRepository = get(),
            trackedEntryRepository = get(),
            llmClientFactory = get(),
            entryAnalysisRepository = get()
        )
    }

    // OAuth
    singleOf(::PendingOAuthResultStore)
    singleOf(::PolarInsightService)
    singleOf(::PolarSyncOrchestrator)

    // LLM
    singleOf(::LlmClientFactory)
    singleOf(::NutritionLabelAnalyzer) { bind<NutritionLabelAnalyzing>() }
    single {
        ToolRegistry(
            trackedEntryRepository = get(),
            entryAnalysisRepository = get(),
            weightHistoryRepository = get(),
            appSettingsRepository = get(),
            nutritionalProfileRepository = get(),
            dailySummaryRepository = get(),
            weeklySummaryRepository = get(),
            dailyCheckInRepository = get(),
            checkInAnalysisRepository = get(),
            polarInsightService = get(),
            dailyTotalsCalculator = get()
        )
    }

    // Event system
    single<StatusChangeNotifier> { DefaultStatusChangeNotifier() }

    // Navigation
    single { HistoricalNavigationContext() }
    single { CalendarNavigationService(get()) }

    // Services
    single {
        AnalysisOrchestrator(
            trackedEntryRepository = get(),
            entryAnalysisRepository = get(),
            llmClientFactory = get(),
            toolRegistry = get(),
            fileSystem = get(),
            appSettingsRepository = get()
        )
    }
    single {
        DailySummaryService(
            trackedEntryRepository = get(),
            entryAnalysisRepository = get(),
            dailySummaryRepository = get(),
            llmClientFactory = get(),
            toolRegistry = get(),
            dailyTotalsCalculator = get(),
            dailyCheckInRepository = get(),
            weightHistoryRepository = get(),
            polarInsightService = get(),
            checkInAnalysisRepository = get(),
            appSettingsRepository = get()
        )
    }
    single {
        WeeklySummaryService(
            trackedEntryRepository = get(),
            weeklySummaryRepository = get(),
            dailySummaryRepository = get(),
            llmClientFactory = get(),
            toolRegistry = get(),
            weightHistoryRepository = get(),
            polarInsightService = get(),
            appSettingsRepository = get()
        )
    }

    // Background services
    single<BackgroundAnalysisService> {
        DefaultBackgroundAnalysisService(
            trackedEntryRepository = get(),
            entryAnalysisRepository = get(),
            analysisOrchestrator = get(),
            backgroundExecutionService = get(),
            statusChangeNotifier = get(),
            weightHistoryRepository = get(),
            appSettingsRepository = get()
        )
    }

    single<StaleEntryRecoveryService> {
        DefaultStaleEntryRecoveryService(
            trackedEntryRepository = get()
        )
    }

    // Data migration
    single<DataMigrationService> {
        DefaultDataMigrationService(
            trackedEntryRepository = get(),
            entryAnalysisRepository = get(),
            nutritionalProfileRepository = get(),
            dailySummaryRepository = get(),
            weeklySummaryRepository = get(),
            appSettingsRepository = get(),
            weightHistoryRepository = get(),
            dailyCheckInRepository = get(),
            fileSystem = get(),
            zipUtil = get(),
            checkInAnalysisRepository = get()
        )
    }

    // Media
    single { com.wellnesswingman.domain.media.ImageRetentionService(get(), get(), get(), get()) }

    // Health Chat
    single {
        HealthChatService(
            healthChatRepository = get(),
            llmClientFactory = get(),
            toolRegistry = get()
        )
    }

    // Health report
    single {
        com.wellnesswingman.domain.report.HealthReportGatherer(
            trackedEntryRepository = get(),
            entryAnalysisRepository = get(),
            dailySummaryRepository = get(),
            weeklySummaryRepository = get(),
            weightHistoryRepository = get(),
            dailyCheckInRepository = get(),
            checkInAnalysisRepository = get(),
            polarSyncRepository = get(),
            appSettingsRepository = get()
        )
    }
    singleOf(::HealthReportBuilder)

    // Google Docs export
    single {
        com.wellnesswingman.domain.googleexport.GoogleDocsExportService(
            apiClient = get(),
            batchBuilder = get()
        )
    }
}
