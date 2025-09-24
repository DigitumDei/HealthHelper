# Android Keystore Workflow Analysis

## Overview
- A release-ready MAUI Android build must reuse the same signing keystore so that updates reach existing users.
- The current keystore was generated locally and a copy exists alongside the source (but is .gitignored); keeping keystores out of source control avoids exposure and eases rotation.
- Goal: move keystore creation/handling into a GitHub workflow while keeping the certificate consistent and private.

## Key Constraints and Risks
- Android package updates break if the signing key changes; automated builds must use the exact keystore originally published.
- Keystore files and passwords are highly sensitive. Source control or build logs must never expose them.
- GitHub-hosted runners are ephemeral, so the workflow has to reconstruct the keystore on each run.
- Manual rotation still needs a repeatable path (generate new keystore offline, update secrets, redeploy).

## Option Evaluation
1. **Keep keystore in repo (status quo)**
   - ✅ Simple for local releases.
   - ❌ High risk: accidental exposure, harder to rotate, violates repository security guidance.
2. **Generate a fresh keystore per build**
   - ✅ Avoids storing files.
   - ❌ Not viable for production updates because app signing identity would change each time.
3. **Store the canonical keystore in GitHub Secrets (recommended)**
   - ✅ Protects the keystore, runner reconstructs it just-in-time.
   - ✅ Supports both manual and automated releases with the same key material.
   - ⚠️ Requires one-time conversion to base64 and secure secret administration.

## Recommended Approach
- Keep the keystore file out of the repository and .gitignore `*.keystore` / `*.jks`.
- Convert the existing release keystore to a base64 string and store it in a GitHub Actions secret (or an environment-protected secret) alongside the alias/store passwords.
- Update the build workflow to recreate the keystore file at runtime before invoking `dotnet publish`.
- Use a dedicated GitHub Environment for release builds so approvals can gate access to the secrets.

## Implementation Steps
1. **Prepare secrets locally**
   ```bash
   # Run once on a trusted machine
   keytool -list -v -keystore your-release.keystore
   base64 your-release.keystore > keystore.b64
   ```
   - `KEYSTORE_B64`: contents of `keystore.b64` (single line, no newlines).
   - `KEYSTORE_PASSWORD`: value used when generating the store.
   - `KEY_ALIAS`: alias chosen during `keytool -genkeypair`.
   - `KEY_PASSWORD`: password for the alias (often matches `KEYSTORE_PASSWORD`).
2. **Add secrets to GitHub**
   - Navigate to *Settings → Secrets and variables → Actions*.
   - Prefer an environment named `production` (or similar) with required reviewers.
   - Create secrets `ANDROID_KEYSTORE_B64`, `ANDROID_KEYSTORE_PASSWORD`, `ANDROID_KEY_ALIAS`, `ANDROID_KEY_PASSWORD`.
3. **Reference secrets in the workflow**
   - Before building, decode the keystore and write it to a secure path (e.g., `${{ runner.temp }}/release.keystore`).
   - Pass MSBuild properties so `dotnet publish` uses the keystore.
4. **Guardrails**
   - Ensure the workflow removes the keystore after use (runners are cleaned automatically, but explicit deletion is prudent).
   - Avoid `echo`-ing secrets; prefer `::add-mask::` if logging is unavoidable.
   - Document the rotation process next to the workflow.

## Sample GitHub Actions Job Snippet
```yaml
name: android-release

on:
  workflow_dispatch:
    inputs:
      channel:
        description: "Release channel"
        default: production
        required: true

jobs:
  build-android:
    runs-on: windows-latest
    environment: production
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "8.0.x"

      - name: Restore dependencies
        run: dotnet restore HealthHelper.slnx

      - name: Materialize Android keystore
        shell: bash
        run: |
          echo "$ANDROID_KEYSTORE_B64" | base64 --decode > "$RUNNER_TEMP/release.keystore"
        env:
          ANDROID_KEYSTORE_B64: ${{ secrets.ANDROID_KEYSTORE_B64 }}

      - name: Build signed Android package
        run: |
          dotnet publish HealthHelper/HealthHelper.csproj \
            -f net8.0-android \
            -c Release \
            -p:AndroidKeyStore=true \
            -p:AndroidSigningKeyStore="$env:RUNNER_TEMP/release.keystore" \
            -p:AndroidSigningStorePass="${{ secrets.ANDROID_KEYSTORE_PASSWORD }}" \
            -p:AndroidSigningKeyAlias="${{ secrets.ANDROID_KEY_ALIAS }}" \
            -p:AndroidSigningKeyPass="${{ secrets.ANDROID_KEY_PASSWORD }}"

      - name: Clean up keystore
        if: always()
        run: Remove-Item -Path "$env:RUNNER_TEMP/release.keystore" -Force
```
- Use `windows-latest` (or `macos-latest`) so MAUI workloads are on the runner; Linux currently lacks MAUI Android support.
- Add signing parameters only for release jobs; debug builds can skip them.

## Security and Maintenance Checklist
- ✅ `.gitignore` keystore artifacts and audit history to remove any committed copies.
- ✅ Limit access to the GitHub Environment so only release maintainers can trigger the job.
- ✅ Document a rotation playbook: generate new keystore, update secrets, redeploy, then revoke the old certificate if needed.
- ✅ Periodically verify the base64 secret by decoding and checking fingerprints offline.
- ✅ Consider using Dependabot alerts for `actions/checkout`/`setup-dotnet` to ensure workflows stay patched.

## Next Steps
- Confirm the keystore stays `.gitignore`d locally and, if it ever slips into history, purge it before promoting the workflow.
- Stand up the GitHub Actions workflow and run a dry run using a non-production channel to confirm signing works.
- Update team onboarding docs to cover secret management and release approvals.
