plugins {
    alias(libs.plugins.kotlin.multiplatform)
    alias(libs.plugins.kotlin.serialization)
    alias(libs.plugins.android.library)
    alias(libs.plugins.sqldelight)
    alias(libs.plugins.kover)
}

kotlin {
    androidTarget {
        compilerOptions {
            jvmTarget.set(org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_11)
        }
    }

    // iOS targets - temporarily disabled due to Gradle 9.3 compatibility issue
    // Error: org.gradle.api.internal.plugins.DefaultArtifactPublicationSet
    // This is a known issue with iOS framework publication in KMP with Gradle 9.x
    // Will be re-enabled when upgrading to compatible Gradle version or Kotlin 2.2+
    // listOf(
    //     iosX64(),
    //     iosArm64(),
    //     iosSimulatorArm64()
    // ).forEach {
    //     it.binaries.framework {
    //         baseName = "shared"
    //         isStatic = true
    //     }
    // }

    jvm("desktop") {
        compilerOptions {
            jvmTarget.set(org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_11)
        }
    }

    sourceSets {
        val commonMain by getting {
            dependencies {
                // Kotlin stdlib
                implementation(libs.kotlinx.datetime)
                implementation(libs.kotlinx.serialization.json)
                implementation(libs.coroutines.core)

                // Koin DI
                implementation(libs.koin.core)

                // SQLDelight
                implementation(libs.sqldelight.coroutines)

                // Ktor HTTP Client
                implementation(libs.ktor.client.core)
                implementation(libs.ktor.client.content.negotiation)
                implementation(libs.ktor.serialization.json)
                implementation(libs.ktor.client.logging)

                // OpenAI
                implementation(libs.openai.kotlin)

                // Logging
                implementation(libs.napier)

                // Settings
                implementation(libs.multiplatform.settings)
                implementation(libs.multiplatform.settings.no.arg)
            }
        }

        val commonTest by getting {
            dependencies {
                implementation(libs.kotlin.test)
                implementation(libs.coroutines.test)
                implementation(libs.mockk)
                implementation(libs.turbine)
                implementation(libs.sqldelight.driver.jdbc)
                implementation(libs.ktor.client.mock)
                implementation(libs.ktor.client.content.negotiation)
                implementation(libs.ktor.serialization.json)
            }
        }

        val androidMain by getting {
            dependencies {
                implementation(libs.sqldelight.driver.android)
                implementation(libs.ktor.client.cio)
                implementation(libs.androidx.core.ktx)
                implementation(libs.androidx.work.runtime)
                implementation(libs.androidx.security.crypto)
                implementation(libs.play.services.auth)
            }
        }

        // iOS source sets - temporarily disabled (see iOS targets comment above)
        // val iosMain by creating {
        //     dependsOn(commonMain)
        //     dependencies {
        //         implementation(libs.sqldelight.driver.native)
        //         implementation(libs.ktor.client.darwin)
        //     }
        // }
        //
        // val iosX64Main by getting {
        //     dependsOn(iosMain)
        // }
        // val iosArm64Main by getting {
        //     dependsOn(iosMain)
        // }
        // val iosSimulatorArm64Main by getting {
        //     dependsOn(iosMain)
        // }

        val desktopMain by getting {
            dependencies {
                implementation(libs.sqldelight.driver.jvm)
                implementation(libs.ktor.client.cio)
            }
        }
    }
}

android {
    namespace = "com.wellnesswingman.shared"
    compileSdk = 34

    defaultConfig {
        minSdk = 26
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_11
        targetCompatibility = JavaVersion.VERSION_11
    }
}

sqldelight {
    databases {
        create("WellnessWingmanDatabase") {
            packageName.set("com.wellnesswingman.db")
            srcDirs.setFrom("src/commonMain/sqldelight")
            schemaOutputDirectory.set(file("src/commonMain/sqldelight/databases"))
            verifyMigrations.set(true)
        }
    }
}

kover {
    reports {
        filters {
            excludes {
                // Exclude generated code
                packages("com.wellnesswingman.db")

                // Exclude platform-specific implementations (tested via integration tests)
                packages("com.wellnesswingman.platform")

                // Exclude DI modules (simple wiring, no logic)
                classes("*Module*")
            }
        }

        verify {
            rule {
                // Current baseline: 25%
                // TODO: Increase threshold as more tests are added
                // Target: 70%+ for production code
                minBound(25)
            }
        }
    }
}
