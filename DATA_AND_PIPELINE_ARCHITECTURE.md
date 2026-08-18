# 🏛️ ThesisAR — Data, Audio, 3D Assets & Telemetry Pipeline Guide

This document is a technical reference guide explaining the data architecture, asset pipelines, audio generation toolchain, and telemetry/evaluation systems of **ThesisAR**. It serves as an onboarding guide for researchers, developers, or examiners who want to inspect, maintain, or extend the project.

---

## 1. 🗺️ High-Level System Architecture & Component Mapping

```text
                               ┌──────────────────────────────────────────────┐
                               │               ThesisManager                  │
                               │  (Central Orchestrator & Mode Controller)    │
                               └───────┬──────────────┬──────────────┬────────┘
                                       │              │              │
                    ┌──────────────────┘              │              └──────────────────┐
                    ▼                                 ▼                                 ▼
      ┌───────────────────────────┐     ┌───────────────────────────┐     ┌───────────────────────────┐
      │   PersonalGuidance (C)    │     │  IntermediateGuidance (B) │     │   ImpersonalGuidance (A)  │
      │ • Embodied 3D Companion   │     │ • On-Demand 3D Hologram   │     │ • 2D Map & Pin Clustering │
      │ • NavMesh Front-Leading   │     │ • Topic / Ask-More Button │     │ • Direct Wayfinding Line  │
      │ • IK Head-Gaze Tracking   │     │ • Alpha Dissolve Shader   │     │ • Sheet Detail Popups     │
      └─────────────┬─────────────┘     └─────────────┬─────────────┘     └─────────────┬─────────────┘
                    │                                 │                                 │
                    └─────────────────┬───────────────┴─────────────────────────────────┘
                                      ▼
                        ┌───────────────────────────┐
                        │      NarrationManager     │
                        │ • Multilingual Audio Play │
                        │ • Subtitle Cache & Synch  │
                        │ • AppLanguage (EN/DE/HE)  │
                        └─────────────┬─────────────┘
                                      ▼
                        ┌───────────────────────────┐
                        │      TelemetryLogger      │
                        │ • GDPR Opt-In Logging     │
                        │ • Google Apps Script Post │
                        │ • Post-Visit Survey Link  │
                        └───────────────────────────┘
```

---

## 2. 📊 Historical Database Pipeline (JSON)

### Primary Source of Truth
- **File Location**: [`Assets/StreamingAssets/Bergen_Belsen_Database.json`](file:///Assets/StreamingAssets/Bergen_Belsen_Database.json)
- **Controller Class**: [`Assets/Scripts/Core/MemorialDataManager.cs`](file:///Assets/Scripts/Core/MemorialDataManager.cs)

### How It Works:
1. At application launch, `MemorialDataManager.Awake()` reads `Bergen_Belsen_Database.json` from `Application.streamingAssetsPath`.
2. Data is parsed into memory structures:
   - **Memorial Stones (`MemorialStone`)**: Inscription (EN/DE/HE), biographical data (names, birth/death dates, inmate IDs), symbol categories.
   - **Mass Graves (`MassGrave`)**: Description, estimated victim counts, geographic bounds.
   - **Other Memorials (`OtherMemorial`)**: House of Silence, Obelisk, Polish Memorial, etc.
3. **Where to Edit**:
   - To add or modify historical entries/translations, edit `Bergen_Belsen_Database.json`.
   - To change UI rendering of the data, edit `UIManager.cs` (`UpdateDetailDisplay()`).

---

## 3. 🎙️ Audio Narration & Dialogue Generation Pipeline

### Audio Asset Directory
- **Folder**: [`Assets/Resources/GuidanceAudio/`](file:///Assets/Resources/GuidanceAudio/)
- **Runtime Subtitle Catalog**: [`Assets/Resources/audio_runtime_catalog.json`](file:///Assets/Resources/audio_runtime_catalog.json)
- **Dialogue Configurations**: [`Assets/Resources/dialogues_config.json`](file:///Assets/Resources/dialogues_config.json)
- **UI Localization**: [`Assets/Resources/ui_localization.json`](file:///Assets/Resources/ui_localization.json)

### Python Generation Toolchain
All audio narration clips were synthesized using neural TTS (Text-to-Speech) pipelines and baked directly into Unity Resource files.

The Python generation scripts are included in the repository:
1. **[`scripts/generate_all_audio.py`](file:///scripts/generate_all_audio.py)**: Batch generates MP3 audio files from the dialogue scripts for English, German, and Hebrew.
2. **[`scripts/build_audio_runtime_catalog.py`](file:///scripts/build_audio_runtime_catalog.py)**: Scans generated audio assets, aligns subtitle transcripts, and builds `audio_runtime_catalog.json`.
3. **[`Assets/Scripts/Audio/bake_historical_dialogues.py`](file:///Assets/Scripts/Audio/bake_historical_dialogues.py)**: Generates walking dialogue chapters (`historicalWalkChapters`) used by `PersonalGuidance.cs`.

### How Narration Plays at Runtime:
1. When a stone or topic is selected, `NarrationManager.PlayNarration(string clipID, AudioSource source)` is invoked.
2. The manager determines the current language via `AppLanguage` (`EN`, `DE`, `HE`), appends the suffix (e.g. `_EN`, `_DE`, `_HE`), and loads the corresponding clip from `Resources/GuidanceAudio/`.
3. Synchronized subtitles are pulled from `audio_runtime_catalog.json` and passed to `UIManager.DisplayGuideSubtitle()`.

---

## 4. 🗿 3D Models & GLB Assets Pipeline

### Hybrid Asset Streaming & Local Fallback
High-fidelity 3D photogrammetry models of memorial stones are decoupled from the base APK to maintain a lightweight app size.

- **Dynamic Downloader**: [`Assets/Scripts/Core/GitHubAssetDownloader.cs`](file:///Assets/Scripts/Core/GitHubAssetDownloader.cs)
- **Model Spawner**: [`Assets/Scripts/AR/StoneModelSpawner.cs`](file:///Assets/Scripts/AR/StoneModelSpawner.cs)

### Workflow:
1. `StoneModelSpawner` checks if a local baked visual exists in `Assets/Resources/Stones/`.
2. If not found locally, `GitHubAssetDownloader` requests the `.glb` file from the remote GitHub Release:
   - **Repository**: `https://github.com/AleksAntic/thesisar-stone-models/releases/tag/v1.0-models/{stoneId}.glb`
3. Downloaded models are cached in `Application.persistentDataPath/StoneModelsCache/` using a `.part` staging verification to prevent corruption.
4. Imported into the scene dynamically at runtime using `glTFast` (`GltfImport`).

---

## 5. 📈 Telemetry, Evaluation & Survey Pipeline

### System Architecture
The application includes a telemetry system designed for field-study evaluation across the 3 guidance conditions.

- **Core Logger**: [`Assets/Scripts/Core/TelemetryLogger.cs`](file:///Assets/Scripts/Core/TelemetryLogger.cs)
- **Session Manager**: [`Assets/Scripts/Core/ThesisManager.cs`](file:///Assets/Scripts/Core/ThesisManager.cs)
- **Survey Prompter**: [`Assets/Scripts/UI/SurveyReminderManager.cs`](file:///Assets/Scripts/UI/SurveyReminderManager.cs)

### Telemetry Events Tracked:
- `session_start` / `session_end`
- `mode_switched` (Impersonal / Intermediate / Personal)
- `stone_entered` / `stone_exited` (Dwell time per memorial)
- `narration_started` / `narration_stopped` (Audio consumption metrics)
- `detour_triggered` (Personal companion spontaneous rerouting events)

### Data Ingestion & Google Apps Script Webhook:
1. **Local Backup**: Session logs are continuously serialized to JSON in `Application.persistentDataPath/session_<mode>_<timestamp>.json`.
2. **GDPR Consent**: If the participant gives consent (`ThesisManager.Instance.UserConsentGDPR = true`), data is uploaded via HTTP POST:
   - **Endpoint**: Google Apps Script Webhook configured in `ThesisManager.googleAppsScriptWebhookUrl`.
   - **Destination**: A connected Google Sheet aggregating participant logs in real time.

### Post-Visit Survey (Google Forms):
When a participant concludes their tour:
1. `ThesisManager.OpenPostVisitSurvey()` triggers the Google Forms questionnaire URL.
2. The URL pre-fills tracking parameters:
   `https://docs.google.com/.../viewform?uid={AnonymousUserID}&mode={GuidanceMode}&duration={Minutes}`
3. This seamlessly correlates quantitative AR telemetry with qualitative participant feedback.

---

## 6. 🛠️ "What to Touch": Guide for Developers & Researchers

| To Change / Extend... | Files to Modify |
|---|---|
| **Add / Edit Memorials & Inscriptions** | `Assets/StreamingAssets/Bergen_Belsen_Database.json` |
| **Add / Edit Narration Audio** | Add `.mp3` to `Assets/Resources/GuidanceAudio/`, update `Assets/Resources/dialogues_config.json`, run `scripts/build_audio_runtime_catalog.py` |
| **Modify 3D Companion Avatar & AI** | `Assets/Scripts/Guidance/PersonalGuidance.cs`, `Assets/Scripts/Dialogue/CompanionIKController.cs` |
| **Modify Hologram Visual Effects** | `Assets/Scripts/Guidance/IntermediateGuidance.cs`, `ActiveGuideAvatarRegistry.cs`, `HologramURP.shader` |
| **Change 2D Map Styling or Clustering** | `Assets/Scripts/Map/Map2DController.cs` (`MapStylingSettings` in Inspector) |
| **Update Webhook or Survey URL** | `Assets/Scripts/Core/ThesisManager.cs` (`googleAppsScriptWebhookUrl`, `surveyFormBaseUrl`) |
| **Configure Android Build & App Icon** | `ProjectSettings/ProjectSettings.asset`, `Assets/app_icon_bb.png` |
