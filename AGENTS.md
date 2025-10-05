# Repository Guidelines

## Project Structure & Module Organization
- `HealthHelper.slnx` coordinates the MAUI app in `HealthHelper/` and shared defaults in `HealthHelper.ServiceDefaults/`.
- Domain entities live under `Data/` and `Models/`; presentation logic sits in `PageModels/`; XAML pages and controls reside in `Pages/` and `Pages/Controls/`.
- Cross-platform assets (icons, fonts, styles) are in `Resources/`; platform-specific heads stay within `Platforms/`.
- Reusable helpers belong in `Utilities/`, and new services should be registered centrally through `MauiProgram.cs`.

## Build, Test, and Development Commands
- `dotnet restore HealthHelper.slnx` pulls all NuGet dependencies prior to any build.
- `dotnet build HealthHelper.slnx` validates the full solution as CI would.
- `dotnet build HealthHelper/HealthHelper.csproj -t:Run` launches the MAUI app on the default target.
- `dotnet test` (run from the repo root) executes all test projects as they are added.
- `dotnet maui-check` is a one-time tooling sanity check per developer machine.
- Always invoke the .NET CLI as `dotnet.exe` (without an explicit path) to ensure the shim on the PATH is used.

## Coding Style & Naming Conventions
- Follow standard .NET conventions: 4-space indentation, PascalCase for types and public members, camelCase for locals and private fields (use `_` prefixes only for property backing fields).
- Match each page (`FooPage.xaml`) with `FooPageModel.cs`, keeping file-scoped namespaces aligned with directory structure.
- Run `dotnet format` before publishing changes to satisfy analyzers and nullable warnings.

## Testing Guidelines
- Place unit tests in the future `HealthHelper.Tests/` project, mirroring production namespaces.
- Name test classes `<TypeUnderTest>Tests` and methods with scenario-driven verbs (e.g., `GeneratePlan_SortsTasksByUrgency`).
- Mock AI integrations so `dotnet test` remains deterministic and offline-friendly.

## Security & Configuration
- Store user-provided LLM keys with `SecureStorage` or platform keystores—never in plaintext files.
- Keep inference and prompting on-device, opt out of telemetry, and document any third-party SDK data handling.
- When adding caching or logs, redact prompts and health data by default and guard debugging hooks behind compilation symbols.

## Commit & Pull Request Guidelines
- Write commits with short, imperative subjects (e.g., “Remove aspire”) and include context when touching multiple layers.
- Scope diffs narrowly and call out security implications of storage or AI changes in commit bodies or PR notes.
- For PRs, provide a summary, linked issue, UI screenshots or recordings if applicable, per-platform manual test notes, and clarity on how sensitive data stays protected.
