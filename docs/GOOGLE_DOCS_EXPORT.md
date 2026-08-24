# Google Docs export setup and manual test

The export is Android-only. It obtains the `drive.file` scope immediately before an export, keeps the access token only in memory, and sends the selected report directly from the device to Google Docs.

## One-time Google Cloud setup

1. In the Google Cloud project used for WellnessWingman, enable both the **Google Docs API** and the **Google Drive API**.
2. Configure the OAuth consent screen, including the `https://www.googleapis.com/auth/drive.file` scope.
3. Create an **Android OAuth client** for the app package ID and add the SHA-1 certificate fingerprints for every signing key used to test or release the app. Google Play services identifies this client from the installed app, so no client secret or OAuth client ID belongs in the app source.
4. Add intended testers while the consent screen is in testing mode.

## Android smoke test

1. Install a build signed with a certificate registered in the Android OAuth client.
2. Open **Settings → Share health diary**, choose a date range and included sections, then prepare the local preview.
3. Confirm the data disclosure, choose the intended Google account in the system consent experience, and create the document.
4. Open the resulting document, confirm its range and selected sections match the preview, and verify that it is visible in the selected account's Drive.
5. Deny or cancel consent and simulate offline mode; ensure the app presents a retryable error and does not create a document for the empty selection case.

No OAuth credential, access token, report contents, or Google Doc URL should be written to application logs.
