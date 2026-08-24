package com.wellnesswingman.ui.di

import com.wellnesswingman.ui.screens.calendar.CalendarViewModel
import com.wellnesswingman.ui.screens.checkin.CheckInConversationStarter
import com.wellnesswingman.ui.screens.checkin.CheckInViewModel
import com.wellnesswingman.ui.screens.checkin.HealthChatCheckInConversationStarter
import com.wellnesswingman.ui.screens.settings.CheckInSettingsViewModel
import com.wellnesswingman.ui.screens.calendar.WeekViewModel
import com.wellnesswingman.ui.screens.calendar.YearViewModel
import com.wellnesswingman.ui.screens.calendar.day.DayDetailViewModel
import com.wellnesswingman.ui.screens.chat.HealthChatListViewModel
import com.wellnesswingman.ui.screens.chat.HealthChatThreadViewModel
import com.wellnesswingman.ui.screens.detail.EntryDetailViewModel
import com.wellnesswingman.ui.screens.main.MainViewModel
import com.wellnesswingman.ui.screens.textentry.TextEntryViewModel
import com.wellnesswingman.ui.screens.nutrition.NutritionLabelScanViewModel
import com.wellnesswingman.ui.screens.nutrition.NutritionalProfilesViewModel
import com.wellnesswingman.ui.screens.nutrition.profileIdFromParameter
import com.wellnesswingman.ui.screens.photo.PhotoReviewViewModel
import com.wellnesswingman.ui.screens.settings.PolarSettingsViewModel
import com.wellnesswingman.ui.screens.settings.SettingsViewModel
import com.wellnesswingman.ui.screens.summary.DailySummaryViewModel
import com.wellnesswingman.ui.screens.weighthistory.WeightHistoryViewModel
import com.wellnesswingman.ui.screens.googleexport.GoogleDocsExportViewModel
import org.koin.core.module.dsl.factoryOf
import org.koin.dsl.module

/**
 * Koin module for ViewModels/ScreenModels.
 */
val viewModelModule = module {
    factory {
        MainViewModel(
            trackedEntryRepository = get(),
            entryAnalysisRepository = get(),
            dailySummaryRepository = get(),
            dailySummaryService = get(),
            dailyTotalsCalculator = get(),
            polarInsightService = get(),
            fileSystem = get(),
            pendingCaptureStore = get(),
            polarSyncOrchestrator = get(),
            dayCheckInsProvider = get(),
            checkInAnalysisService = get()
        )
    }
    factory { params ->
        TextEntryViewModel(
            textEntryProcessor = get(),
            audioRecordingService = get(),
            llmClientFactory = get(),
            fileSystem = get(),
            initialText = params.getOrNull<String>().orEmpty()
        )
    }
    factory { params ->
        CheckInViewModel(
            slot = params.get(),
            checkInDate = params.getOrNull(),
            dailyCheckInRepository = get(),
            audioRecordingService = get(),
            llmClientFactory = get(),
            fileSystem = get(),
            conversationStarter = get(),
            checkInAnalysisService = get()
        )
    }
    single<CheckInConversationStarter> {
        HealthChatCheckInConversationStarter(
            healthChatService = get(),
            appSettingsRepository = get()
        )
    }
    factoryOf(::CheckInSettingsViewModel)
    factoryOf(::SettingsViewModel)
    factoryOf(::PolarSettingsViewModel)
    factoryOf(::WeightHistoryViewModel)
    factoryOf(::GoogleDocsExportViewModel)
    factoryOf(::DailySummaryViewModel)
    factoryOf(::PhotoReviewViewModel)
    factoryOf(::NutritionalProfilesViewModel)
    factoryOf(::HealthChatListViewModel)
    factory { params ->
        HealthChatThreadViewModel(
            conversationExternalId = params.get(),
            healthChatService = get(),
            healthChatRepository = get(),
            settingsRepository = get(),
        )
    }
    factory { params ->
        NutritionLabelScanViewModel(
            profileId = profileIdFromParameter(runCatching { params.get<Long>() }.getOrNull()),
            cameraService = get(),
            fileSystem = get(),
            analyzer = get(),
            repository = get()
        )
    }
    factoryOf(::CalendarViewModel)
    factory {
        WeekViewModel(
            trackedEntryRepository = get(),
            weeklySummaryService = get(),
            weeklySummaryRepository = get(),
            polarInsightService = get(),
            audioRecordingService = get(),
            llmClientFactory = get(),
            fileSystem = get()
        )
    }
    factoryOf(::YearViewModel)
    factory {
        DayDetailViewModel(
            trackedEntryRepository = get(),
            entryAnalysisRepository = get(),
            dailySummaryRepository = get(),
            dailySummaryService = get(),
            dailyTotalsCalculator = get(),
            polarInsightService = get(),
            fileSystem = get(),
            dayCheckInsProvider = get(),
            checkInAnalysisService = get()
        )
    }
    factory { params ->
        EntryDetailViewModel(
            entryId = params.get(),
            trackedEntryRepository = get(),
            entryAnalysisRepository = get(),
            fileSystem = get(),
            backgroundAnalysisService = get(),
            statusChangeNotifier = get(),
            audioRecordingService = get(),
            llmClientFactory = get()
        )
    }
}
