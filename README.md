# Bergen-Belsen AR Guide (ThesisAR)

An augmented reality (AR) memorial navigation and historical learning application developed for the **Bergen-Belsen Memorial Site** (*Gedenkstätte Bergen-Belsen*) as a Master's Thesis project at the **University of Southern Denmark (SDU)** in collaboration with the **University of Udine**.

The application evaluates three distinct levels of virtual embodiment in cultural heritage AR navigation:
1. **Personal Guidance (Condition C)**: A 3D companion avatar walks along paths ahead of the user with real-time tethering, procedural footstep audio, and inverse kinematics (IK) head-gaze tracking.
2. **Intermediate Guidance (Condition B)**: An on-demand 3D holographic avatar appears next to historical memorial stones upon interaction, with topic selection and smooth alpha dissolve.
3. **Impersonal Guidance (Condition A)**: Autonomous exploration supported by a dynamic 2D interactive UI map, path lines, and localized historical database sheets.

> 📖 **Deep-Dive Technical Pipeline**: For in-depth documentation on the data architecture, python audio generation tools, 3D GLB release pipelines, and Google Sheets/Forms telemetry, see [**`DATA_AND_PIPELINE_ARCHITECTURE.md`**](file:///DATA_AND_PIPELINE_ARCHITECTURE.md).

---

## 🛠️ Tech Stack & Requirements

- **Unity Engine**: `Unity 6 (6000.0.x / 6.4+)`
- **Render Pipeline**: Universal Render Pipeline (`URP 17.4.0`)
- **AR Framework**: Unity AR Foundation (`6.4.2`) + Google ARCore XR Plugin (`6.4.2`)
- **Geospatial Tracking**: Google ARCore Extensions (`1.53.0`)
- **3D Geospatial Tiles**: Cesium for Unity (`1.23.1`)
- **3D Asset Runtime Loading**: Unity `glTFast 6.18.0`
- **Input Framework**: Unity New Input System (`1.19.0`)
- **Target Platform**: Android 10.0+ (API Level 29+), recommended Android 12+ on ARCore-supported hardware with GPS & compass.

---

## 🚀 Getting Started: How to Open the Project

### 1. Prerequisites
- Install **[Unity Hub](https://unity.com/download)**.
- Install **Unity 6 (6000.0.x)** with the **Android Build Support** module (including Android SDK & NDK Tools and OpenJDK).
- Ensure **[Git LFS](https://git-lfs.github.com/)** is installed on your system (`git lfs install`).

### 2. Clone the Repository
```bash
git clone https://github.com/<your-username>/ThesisAR.git
```

### 3. Open in Unity Hub
1. Open **Unity Hub**.
2. Click **Add** → **Add project from disk**.
3. Select the `ThesisAR` root folder.
4. Open the project with Unity 6.
5. Unity will automatically parse `Packages/manifest.json` and resolve all dependencies.

### 4. Load the Main Scene
In the Unity Project window, open:
📁 `Assets/Scenes/emptyy.unity`

---

## 📂 Project Structure & Key Directories

```text
ThesisAR/
├── Assets/
│   ├── Audio/               # Sound effects, UI chimes, and ambient audio
│   ├── Prefabs/             # Avatars, UI prefabs, directional arrows, markers
│   ├── Resources/
│   │   ├── GuidanceAudio/   # Spoken narration clips (EN/DE/HE)
│   │   ├── audio_runtime_catalog.json # Authoritative subtitle & audio transcript catalog
│   │   ├── dialogues_config.json      # Dialogue configuration tree
│   │   └── ui_localization.json       # Trilingual UI labels and button texts
│   ├── Scenes/
│   │   └── emptyy.unity     # Main operational AR exploration scene
│   ├── Scripts/
│   │   ├── AR/              # Geospatial manager, wayfinding line/arrows, stone spawning
│   │   ├── Core/            # ThesisManager, NarrationManager, AppLanguage, Data, Downloader
│   │   ├── Dialogue/        # CompanionIKController, Hologram shaders & effects, sequences
│   │   ├── Guidance/        # Personal, Intermediate, Impersonal guidance systems & registry
│   │   ├── Interaction/     # Geospatial viewport detection, touch & input raycasting
│   │   ├── Map/             # 2D Interactive map controller, pin clustering, GPS tracking
│   │   └── UI/              # UIManager, popups, instant faceted search, audio controllers
│   ├── Settings/            # URP Graphics assets and quality presets
│   └── StreamingAssets/
│       └── Bergen_Belsen_Database.json # Master historical database (Inscriptions, Mass Graves)
├── Packages/
│   ├── manifest.json        # Project package dependencies and version locks
│   ├── packages-lock.json   # Resolved package lockfile
│   ├── com.cesium.unity/    # Embedded Cesium for Unity package
│   └── com.google.ar.core.arfoundation.extensions/ # Embedded ARCore Extensions
├── scripts/                 # Python tools for Neural TTS audio generation & catalog baking
└── ProjectSettings/         # Unity engine, tags, layers, graphics, and Android player settings
```

---

## 🔄 Data & Asset Pipelines Summary

### 1. Historical Database (`Bergen_Belsen_Database.json`)
- Loaded at boot by `MemorialDataManager.cs`.
- Contains all memorial stones, mass grave boundaries, victim estimates, and biographical inscriptions in English, German, and Hebrew.

### 2. Audio & Dialogue Generation (Python Toolchain)
- Audio clips reside in `Assets/Resources/GuidanceAudio/`.
- Automated Python scripts in `scripts/` (`generate_all_audio.py`, `build_audio_runtime_catalog.py`) synthesize multilingual TTS and compile `audio_runtime_catalog.json`.

### 3. 3D Models & GLB Streaming
- Photogrammetry 3D stones are loaded on-demand by `StoneModelSpawner.cs` and `GitHubAssetDownloader.cs` from GitHub Releases (`AleksAntic/thesisar-stone-models`) with local disk caching in `persistentDataPath`.

### 4. Telemetry, Google Sheet Webhook & Post-Visit Survey
- `TelemetryLogger.cs` tracks session duration, guidance mode switches, stone dwell times, and narration playback.
- Session logs are saved locally as JSON and uploaded via HTTP POST to a Google Apps Script Webhook when GDPR consent is enabled.
- `ThesisManager.OpenPostVisitSurvey()` triggers Google Forms pre-populated with anonymous user ID, guidance condition, and visit duration.

---

## 🛠️ Developer Cheatsheet: What to Touch

| If you want to... | Look at / Edit... |
|---|---|
| **Edit Memorial Text & Inscriptions** | `Assets/StreamingAssets/Bergen_Belsen_Database.json` |
| **Add or Update Spoken Audio** | Put `.mp3` into `Assets/Resources/GuidanceAudio/` & run `scripts/build_audio_runtime_catalog.py` |
| **Tweak 3D Companion AI & Navigation** | `Assets/Scripts/Guidance/PersonalGuidance.cs` & `CompanionIKController.cs` |
| **Adjust Hologram Effects & Dissolve** | `Assets/Scripts/Guidance/IntermediateGuidance.cs` & `ActiveGuideAvatarRegistry.cs` |
| **Modify 2D Map Colors & Sizing** | `Assets/Scripts/Map/Map2DController.cs` (or select `Map_2D_Panel` in Inspector) |
| **Change Telemetry Webhook or Survey URL**| `Assets/Scripts/Core/ThesisManager.cs` (`googleAppsScriptWebhookUrl`, `surveyFormBaseUrl`) |

---

## 📱 Building the Android APK

1. In Unity, open **File → Build Settings**.
2. Ensure the platform is set to **Android** (click *Switch Platform* if necessary).
3. Ensure `Assets/Scenes/emptyy.unity` is checked in the *Scenes in Build* list.
4. In **Project Settings → Player → Android**:
   - **Package Name**: `com.AleksAntic.ThesisAR`
   - **Product Name**: `Bergen-Belsen AR Guide`
   - **Minimum API Level**: Android 10.0 (API Level 29)
   - **Target API Level**: Automatic (highest installed)
5. Click **Build** and choose your destination folder.

---

## 📄 License & Attribution

- Developed by Aleks Antic for Master's Thesis research at SDU / UniUD.
- Memorial site data and historical references courtesy of the Bergen-Belsen Memorial archive.
