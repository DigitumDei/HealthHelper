package com.wellnesswingman.platform

import com.wellnesswingman.domain.googleexport.GoogleAuthResult
import com.wellnesswingman.domain.googleexport.GoogleAuthService

/**
 * Desktop [GoogleAuthService].
 *
 * Google Docs export is Android-only by design: the feature uses the Android
 * Google authorization library and its on-device consent. Desktop therefore
 * returns an explicit [GoogleAuthResult.Unavailable] so the UI can show a
 * clear "not supported on this platform" state instead of pretending to work.
 */
class DesktopGoogleAuthService : GoogleAuthService {

    override val isAvailable: Boolean get() = false

    override suspend fun requestAccess(): GoogleAuthResult = GoogleAuthResult.Unavailable

    override fun deliverConsentResult(intentData: Any?) {
        // No consent screen on desktop; nothing to deliver.
    }

    override fun clearAccessToken() {
        // Nothing is ever held on desktop.
    }
}