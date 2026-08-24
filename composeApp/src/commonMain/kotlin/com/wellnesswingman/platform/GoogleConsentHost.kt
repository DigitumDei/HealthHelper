package com.wellnesswingman.platform

import androidx.compose.runtime.Composable

/**
 * Hosts the Google consent screen after a consent result is observed.
 * Android launches the stored consent intent via the Activity result API;
 * other platforms are no-ops (Google Docs export is Android-only).
 *
 * [onCompleted] is invoked once the consent screen finishes (granted, denied,
 * or cancelled) so the exporter can resume and pick up the delivered outcome.
 */
@Composable
expect fun GoogleConsentHost(onCompleted: () -> Unit)