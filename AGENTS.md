# Repository Guidelines

## Project Structure & Module Organization
The solution `HealthHelper.slnx` loads the MAUI app in `HealthHelper/`, with shared defaults in `HealthHelper.ServiceDefaults/`. Within the app, domain data resides under `Data/` and `Models/`, UI logic in `PageModels/`, and XAML pages plus controls in `Pages/` and `Pages/Controls/`. Cross-platform assets live in `Resources/` (`AppIcon/`, `Fonts/`, `Styles/`, etc.), platform heads are in `Platforms/`, and reusable helpers belong in `Utilities/`. Register new services or configuration extensions through `MauiProgram.cs` to keep dependency injection centralized.

## Build, Test, and Development Commands
Run `dotnet restore HealthHelper.slnx` before building. Use `dotnet build HealthHelper.slnx` for a CI-equivalent compile, and `dotnet build HealthHelper/HealthHelper.csproj -t:Run` to launch the MAUI app on the default target. `dotnet maui-check` validates local tooling once per workstation. As test projects spin up, execute `dotnet test` from the repo root so CI remains portable across platforms.

## Coding Style & Naming Conventions
Apply standard .NET style: 4-space indentation, PascalCase for types and public members, camelCase for locals and private fields (prefix with `_` only for property backing). Pair each page (`FooPage.xaml`) with a matching `FooPageModel.cs`, and keep file-scoped namespaces (`namespace ...;`) consistent. Run `dotnet format` prior to PRs to enforce analyzers and nullable warnings.

## AI Configuration & Security
Users bring their own LLM keys; persist secrets with `SecureStorage` or platform keystores, never plain text files. Keep inference strictly on-device—opt out of telemetry, and document any third-party SDK capabilities before inclusion. When introducing caching or logs, redact prompts and health data by default and guard debugging utilities behind compilation symbols.

## Testing Guidelines
Add unit tests under a future `HealthHelper.Tests/` project mirroring production namespaces. Name test classes `<TypeUnderTest>Tests` and use descriptive test method names (e.g., `GeneratePlan_SortsTasksByUrgency`). Mock AI clients so tests run deterministically without external calls, and ensure `dotnet test` passes without platform-specific switches.

## Commit & Pull Request Guidelines
Commits follow short, imperative subjects (e.g., “Remove aspire”). Keep diffs scoped, add body context when touching multiple layers, and note security implications of new AI or storage code. For PRs, supply a summary, linked issue, screenshots or recordings for UI changes, manual test notes per platform, and explicit mention of how sensitive data is safeguarded.
