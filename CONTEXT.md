# 🚀 ThesisAR — Complete Technical Context & Project Master Document
> **For AI Agents (Cline / DeepSeek / Claude / Antigravity)**  
> **Project**: AR Wayfinding & Historical Guidance System (Bergen-Belsen Memorial Site)  
> **Tech Stack**: Unity 6.4 (6000.4.2f1), Universal Render Pipeline (URP), Google ARCore Geospatial SDK (1.53.0), Cesium for Unity, UI Toolkit & Canvas UI.

---

## 📌 1. Project Overview & Core Architecture

ThesisAR is an Augmented Reality guidance application designed for field testing at the Bergen-Belsen Memorial site. It compares three experimental guidance conditions to evaluate visitor engagement and spatial navigation:

1. **Condition A (Personal Guidance — `PersonalGuidance.cs`)**:
   - Dynamic 3D avatar companion (`MaleCharacter`) that physically walks ahead of the visitor along a NavMesh path.
   - Uses an **Elastic Pacing algorithm** to adjust walking speed dynamically based on user proximity.
   - Triggers vocal narration, speech gesture animations (`IsTalking` animator state), and UI subtitles.

2. **Condition B (Intermediate Guidance — `IntermediateGuidance.cs`)**:
   - Static contextual 3D hologram avatar that spawns on-demand next to targeted historical memorial stones or mass graves.
   - Applies custom URP Hologram shader (`ThesisAR/HologramURP`) with rim lighting and scanlines.
   - Provides interactive **Ask-More topic chips** (`AskMoreButtonController`) populated dynamically from `stone_symbols_map.json`.

3. **Condition C (Impersonal Guidance — `ImpersonalGuidance.cs`)**:
   - 2D UI navigation only (minimap and directional compass arrow, no 3D avatar).

---

## 🏗️ 2. Key Codebase Components & File Map

### Core Orchestrators (`Assets/Scripts/Core/`)
- [`ThesisManager.cs`](file:///d:/uni/ERASMUS/TESI%20SDU/progetto/ThesisAR/Assets/Scripts/Core/ThesisManager.cs): Central orchestrator. Manages `GuidanceMode` (Personal, Intermediate, Impersonal), auto-binds scene references in `Awake()` and `OnValidate()`, logs session events for research analysis.
- [`NarrationManager.cs`](file:///d:/uni/ERASMUS/TESI%20SDU/progetto/ThesisAR/Assets/Scripts/Core/NarrationManager.cs): Global audio narration manager. Synchronizes voice clips (`Resources/GuidanceAudio/`) with UI subtitles.
- [`MemorialSpawner.cs`](file:///d:/uni/ERASMUS/TESI%20SDU/progetto/ThesisAR/Assets/Scripts/Core/MemorialSpawner.cs): Handles 168 physical memorial stones and mass graves layout pins.

### Guidance Systems (`Assets/Scripts/Guidance/`)
- [`GuidanceSystemBase.cs`](file:///d:/uni/ERASMUS/TESI%20SDU/progetto/ThesisAR/Assets/Scripts/Guidance/GuidanceSystemBase.cs): Abstract base class for all 3 guidance modes. Enforces `Initialize()`, `OnMemorialSelected()`, `OnMemorialDeselected()`, `OnMemorialReached()`.
- [`PersonalGuidance.cs`](file:///d:/uni/ERASMUS/TESI%20SDU/progetto/ThesisAR/Assets/Scripts/Guidance/PersonalGuidance.cs): Implements `IAvatarOwner`. NavMesh pathfinding, avatar spawning, elastic pacing.
- [`IntermediateGuidance.cs`](file:///d:/uni/ERASMUS/TESI%20SDU/progetto/ThesisAR/Assets/Scripts/Guidance/IntermediateGuidance.cs): Implements `IAvatarOwner`. Hologram avatar spawning, URP hologram shader, Ask-More topic chips.
- [`ActiveGuideAvatarRegistry.cs`](file:///d:/uni/ERASMUS/TESI%20SDU/progetto/ThesisAR/Assets/Scripts/Guidance/ActiveGuideAvatarRegistry.cs): **Central single-avatar exclusivity registry**. Guarantees that **only ONE 3D avatar exists physically in the scene at any time** via `ClaimOwnership(IAvatarOwner)`.

### User Interface & Navigation (`Assets/Scripts/UI/`)
- [`UIManager.cs`](file:///d:/uni/ERASMUS/TESI%20SDU/progetto/ThesisAR/Assets/Scripts/UI/UIManager.cs): Master UI controller. Manages `navigationStack` (`Stack<GameObject>`), modal panels, language switching (EN/DE/HE), and `SummonGuideAvatar()`.
- [`ModelInspectorUI.cs`](file:///d:/uni/ERASMUS/TESI%20SDU/progetto/ThesisAR/Assets/Scripts/UI/ModelInspectorUI.cs): Interactive offscreen 3D model viewer for inspecting stone epigraphs. Includes re-entrancy guard `isClosing` to prevent stack skipping.
- [`PopupAudioController.cs`](file:///d:/uni/ERASMUS/TESI%20SDU/progetto/ThesisAR/Assets/Scripts/UI/PopupAudioController.cs): Controls audio playback toggles on memorial stone detail panels.

### Map & Spatial Navigation (`Assets/Scripts/Map/`)
- [`Map2DController.cs`](file:///d:/uni/ERASMUS/TESI%20SDU/progetto/ThesisAR/Assets/Scripts/Map/Map2DController.cs): 2D interactive minimap, pinch zoom, user GPS tracking marker. Optimized with cached manager references in `Start()`.

### Editor Utilities (`Assets/Scripts/Editor/`)
- [`AutoConnectThesisManagerTool.cs`](file:///d:/uni/ERASMUS/TESI%20SDU/progetto/ThesisAR/Assets/Scripts/Editor/AutoConnectThesisManagerTool.cs): Unity Editor menu tool (`ThesisAR -> Auto-Connect All ThesisManager Inspector References`) to auto-wire Inspector fields in Edit Mode.

---

## 🛠️ 3. Solved Issues & Applied Fixes Summary

1. **Elimination of Runtime Instance Duplication (`ThesisManager.cs`)**:
   - Removed legacy `new GameObject($"GuidanceSystem_{mode}")`. The app strictly reuses fixed scene GameObjects (`GuidanceSystem_Personal`, `AR_Core_Controller/GuidanceSystem_Intermediate`, `GuidanceSystem_Impersonal`).

2. **Single-Avatar Exclusivity (`ActiveGuideAvatarRegistry.cs`)**:
   - Centralized registry enforcing `IAvatarOwner` interface. Calling `ClaimOwnership(this)` immediately forces despawn of any previously active avatar.

3. **Intermediate Welcome Hologram Spawn (`ThesisManager.cs`)**:
   - `SetGuidanceMode(GuidanceMode.Intermediate)` automatically triggers `currentGuidanceSystem.OnMemorialSelected("WELCOME_INTERMEDIATE")` if no memorial is currently selected, ensuring an avatar spawns immediately upon mode switch.

4. **3D Inspector Navigation Stack Fix (`ModelInspectorUI.cs`)**:
   - Added `isClosing` re-entrancy guard in `CloseInspector()` to prevent double-invocation from persistent + C# button events, ensuring closing the 3D Inspector returns to `MemorialDetailPanel` instead of jumping back to `DatabaseSearchPanel`.

5. **Self-Healing Inspector Auto-Binding (`ThesisManager.cs` & `AutoConnectThesisManagerTool.cs`)**:
   - Added `OnValidate()` and `ResolveGuidanceInstances()` to auto-bind scene references in Edit Mode and runtime.

6. **Minimap Performance Optimization (`Map2DController.cs`)**:
   - Cached `TourManager`, `GeospatialManager`, `RouteManager`, and `UIManager` in `Start()` to eliminate per-frame `FindAnyObjectByType` queries in `LateUpdate()`.

---

## 📋 4. Rules & Guidelines for AI Coding Agents

When writing or modifying code in this codebase:

1. **The Ponytail Rules (Anti-Bloat)**:
   - Use Unity native APIs or standard C# features.
   - Touch ONLY the specific lines or methods required by the task. Never rewrite an entire C# class.
   - Do not add unrequested abstractions or speculative features.

2. **Unity & Performance Best Practices**:
   - Avoid memory allocations (`GC.Alloc`) in `Update()`, `FixedUpdate()`, or `LateUpdate()`. Cache references in `Awake()` or `Start()`.
   - Use `[SerializeField] private` instead of public fields.
   - Ensure proper brace balance and syntax validation after edits.

3. **Language Localization & Strings**:
   - App supports English (`"english"`) and German (`"german"`). Keep UI strings localized using `SelectedLanguage` / `AppLanguage`.

---

## 🟢 5. Current System Health & Compilation Status

- **C# Compilation Status**: Clean build, 0 compilation errors, 100% brace balance verified across all `.cs` files.
- **Scene Setup**: Scene GameObjects (`GuidanceSystem_Personal`, `AR_Core_Controller/GuidanceSystem_Intermediate`) are stable and auto-connected.
