# HealthHelper

HealthHelper is a .NET MAUI companion app that lets you capture day-to-day health data while keeping full control over your information. Large Language Models (LLMs) generate insights, summaries, and coaching suggestions locally on your device or through user-supplied APIs—HealthHelper never ships sensitive data to a managed cloud.

## Vision

- **User-owned intelligence** – You choose which supported LLM to call and provide your own API key. Future releases may support optional, user-configured storage providers, but there is no first-party cloud backend.
- **Private by default** – Health data, prompts, and responses stay on the device. Secure storage and local databases hold the information, with opt-in redaction for diagnostics.
- **AI-assisted health** – Start with a diet tracker: snap photos of meals throughout the day and let the LLM classify, log, and recommend nutritional adjustments. Additional fitness modules will build on the same privacy-first foundation.

## Current Focus

1. **Diet Tracker MVP** – Capture meal photos, persist them locally, and surface LLM-generated meal summaries and suggestions.
2. **Bring Your Own Model** – Support a curated list of LLM providers with user-supplied credentials and configurable inference parameters.
3. **Offline-first Data** – Maintain health logs in a local database with export options under the user’s control.

## Getting Started

1. Install the .NET 10 preview workload for MAUI (see `.config/dotnet-tools.json` once available).
2. Restore dependencies:
   ```bash
   dotnet restore HealthHelper.slnx
   ```
3. Build the solution:
   ```bash
   dotnet build HealthHelper.slnx
   ```
4. Run the MAUI app on your preferred target (example for Android):
   ```bash
   dotnet build HealthHelper/HealthHelper.csproj -t:Run -f net10.0-android
   ```

## Privacy Notes

- API keys and sensitive tokens should be stored via platform secure storage (`SecureStorage`, Keychain, Keystore).
- Telemetry is disabled by default; any future analytics or debugging hooks must be explicit opt-in and redact user content.
- When integrating new LLMs or storage providers, document capabilities and ensure compliance with the project’s privacy guidelines in `docs/`.

## Roadmap Highlights

- Meal photo capture pipeline with local preprocessing and handoff to the selected LLM.
- Configurable meal summaries (macros, dietary restrictions, coaching tone).
- Export/import flows so users can move their data between devices or private storage.
- Future modules: activity tracking, habit nudges, and goal planning built atop the same privacy-centric design.

Contributions are welcome—please review the repository guidelines in `docs/` before opening a pull request.
