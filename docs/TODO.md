# TODO

- [x] Fix the Android build issue where the `xml/file_paths` resource is not found.
- [x] Fully implement the "Take Photo" button functionality:
    - [x] Add the button to the main page UI.
    - [x] Implement the `Clicked` event handler to capture a photo using the device's camera.
    - [x] Ensure all necessary permissions are correctly configured for both Android and iOS to allow camera and storage access.
- [ ] Save captured photos into persistent storage so entries survive app restarts.
- [ ] Implement SQLite repositories for tracked entries, analyses, and summaries.
- [ ] Define the LLM provider interface and deliver an initial OpenAI adapter.
- [ ] Persist LLM responses into the new analysis tables via the orchestration pipeline.
- [ ] Generate daily summaries from stored analyses and surface them in the UI.
