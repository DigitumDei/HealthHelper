package com.wellnesswingman.platform

import androidx.compose.runtime.Composable

/** Google Docs authorization is intentionally Android-only. */
@Composable
actual fun GoogleConsentHost(onCompleted: () -> Unit) = Unit
