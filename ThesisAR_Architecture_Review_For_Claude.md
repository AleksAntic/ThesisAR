# 📋 ThesisAR Architecture Review & Refactoring Plan (For Peer Review / Claude)

## 📌 Project Context
- **Project**: ThesisAR (Unity 6.4 / 6000.4.2f1 URP, ARCore Geospatial 1.53, Cesium for Unity).
- **Core Architecture**: Experimental AR guidance app comparing three guidance conditions:
  1. **Impersonal Mode**: Map/spatial audio only (no 3D avatar).
  2. **Intermediate Mode**: Static 3D URP Hologram avatar (`ThesisAR/HologramURP` shader) spawned adjacent to memorial stones or summoned near the user.
  3. **Personal Mode**: Embodied 3D companion avatar (PBR materials, NavMesh pathfinding, elastic pacing, head-gaze IK tracking via `CompanionIKController`).
- **Single Persistent Avatar System**: Both `IntermediateGuidance` and `PersonalGuidance` share a single persistent 3D avatar GameObject (`SingleGuideAvatarInstance`), managed centrally by `ActiveGuideAvatarRegistry.cs`.

---

## 🔍 1. Current Architecture & Functional Pipeline

### A. Core Components & Ownership Model
- **`ThesisManager.cs`**: Singleton orchestrator managing `CurrentMode` and active `GuidanceSystemBase` instances (`intermediateGuidanceInstance`, `personalGuidanceInstance`).
- **`ActiveGuideAvatarRegistry.cs`**: Single source of truth for `SingleGuideAvatarInstance`. Handles ownership transfers (`ClaimOwnership`), material caching (`originalPBRMaterialsMap` vs `hologramMaterialsCache`), `NavMeshAgent` toggling, and `CompanionIKController` toggling.
- **`GuidanceSystemBase.cs`**: Abstract base class inherited by `IntermediateGuidance` and `PersonalGuidance`.
- **`UIManager.cs`**: Controls UI screens (`onboardingPanel`, `arExplorationHub`), language settings, and mode buttons.
- **`NarrationManager.cs`**: Plays voice clips and raises `OnNarrationFinished` when audio playback and subtitle timer conclude.

---

## 🐛 2. Identified Bugs, Root Causes & Analysis

### Bug 1: Avatar visible & talking behind Onboarding panel on Play Mode boot
- **Root Cause**: In `ThesisManager.cs` (`Start()`), `SetGuidanceMode(currentMode)` was called at boot time. `SetGuidanceMode` immediately called `OnMemorialSelected("")`, spawning `SingleGuideAvatarInstance` in the 3D world behind the semi-transparent Onboarding panel and playing welcome narration before the user clicked "Start Experience".
- **Fix Applied**: Introduced `isExperienceStarted = false` flag in `ThesisManager.cs`. `SetGuidanceMode` accepts `bool triggerAvatarAndAudio = true`. During boot and Onboarding mode selection, `triggerAvatarAndAudio` is `false`, updating UI highlights silently. Avatar spawning and welcome audio are deferred until `UIManager.StartExperience()` calls `ThesisManager.StartExperienceSession()`.

---

### Bug 2: "Summon Guide" (*Begleiter rufen*) despawns Intermediate Hologram and spawns Personal PBR Companion
- **Root Cause**:
  1. In the Unity Scene file (`emptyy.unity`), the UI button `Btn_SummonGuide` had a hardcoded scene UnityEvent `onClick` listener directly pointing to `PersonalGuidance.SummonAvatarToUser()`.
  2. In `UIManager.cs`, the code attempting to re-wire `Btn_SummonGuide` was placed inside `UpdateLanguageVisuals()`, guarded by `if (arExplorationHub != null)`. Because `arExplorationHub` was inactive (`activeSelf == false`) at boot, `UpdateLanguageVisuals()` skipped `Btn_SummonGuide`. The scene listener remained intact, calling `PersonalGuidance.SummonAvatarToUser()` even when Intermediate Mode was active!
  3. When `PersonalGuidance.SummonAvatarToUser()` ran, `ActiveGuideAvatarRegistry.ClaimOwnership(this)` transferred ownership from `IntermediateGuidance` to `PersonalGuidance`, despawning the hologram and instantiating the Personal PBR companion.
- **Fix Applied**: In `UIManager.Start()`, call `FindGameObjectIncludingInactive("Btn_SummonGuide")`. At boot time, `Btn_SummonGuide.onClick.RemoveAllListeners()` strips the scene hardcoded listener and binds `UIManager.SummonGuideAvatar()`. `SummonGuideAvatar()` inspects `ThesisManager.CurrentMode` and dynamically routes to `IntermediateGuidance.SummonAvatarToUser()` when Intermediate Mode is active.

---

### Bug 3: Intermediate Hologram Avatar does NOT stop talking gesture after welcome narration finishes
- **Root Cause**:
  1. In `IntermediateGuidance.cs`, `OnDisable()` called `NarrationManager.Instance.OnNarrationFinished -= HandleNarrationFinished`. When `ThesisManager.SetGuidanceMode` deactivated inactive guidance objects at boot (`gameObject.SetActive(false)`), `OnDisable()` unsubscribed `HandleNarrationFinished`. When later activated (`gameObject.SetActive(true)`), `IntermediateGuidance` lacked an `OnEnable()` event re-subscription! Thus, `IntermediateGuidance` was unsubscribed from `OnNarrationFinished` and never received the event to call `SetTalkingState(false)`.
  2. Additionally, `SetTalkingState` called `avatarAnimator.SetBool("isTalking", talking)` and `avatarAnimator.SetBool("IsSpeaking", talking)` which threw Unity Console Warnings ("Parameter does not exist").
- **Fix Applied**: Added `OnEnable()` in `IntermediateGuidance.cs` and `PersonalGuidance.cs` with `NarrationManager.Instance.OnNarrationFinished += HandleNarrationFinished`. Cleaned `SetTalkingState()` to set only valid `IsTalking` animator parameter.

---

### Bug 4: Intermediate Hologram floats at eye height (feet not attached to ground)
- **Root Cause**: `IntermediateGuidance.cs` calculated spawn position using `spawnPos.y = cam.position.y - 1.5f` (or `- 1.2f`). This static offset ignored ground elevation and NavMesh topology, causing the avatar to float in mid-air at camera level.
- **Proposed Unified Fix**: Add `CalculateGroundSpawnPosition(Transform userCameraTransform, float forwardDistance)` in `GuidanceSystemBase.cs`. It samples `NavMesh.SamplePosition` and performs a downward `Physics.Raycast` to calculate the exact ground Y coordinate, attaching the avatar's feet to the ground surface.

---

### Bug 5: Intermediate "Summon Guide" restarts welcome narration audio from beginning
- **Root Cause**: `IntermediateGuidance.SummonAvatarToUser()` called `ActivateSharedAvatarAt(spawnPos, spawnRot, narrationId)`, which called `TriggerNarrativeVoiceSynthesis("WELCOME_INTERMEDIATE")` every time.
- **Proposed Unified Fix**: Align `IntermediateGuidance.SummonAvatarToUser()` with `PersonalGuidance.SummonAvatarToUser()`. Reposition the hologram avatar to `CalculateGroundSpawnPosition(cam, 2.0f)` without re-triggering welcome audio synthesis.

---

## 🛠️ 3. Refactoring Code Implementation

### A. Base Class Ground Positioning ([`GuidanceSystemBase.cs`](file:///d:/uni/ERASMUS/TESI%20SDU/progetto/ThesisAR/Assets/Scripts/Guidance/GuidanceSystemBase.cs))

```csharp
using UnityEngine;
using UnityEngine.AI;

public abstract class GuidanceSystemBase : MonoBehaviour
{
    protected UIManager uiManager;
    protected ARWayfindingManager wayfindingManager;
    protected ThesisManager thesisManager;
    protected MemorialSpawner memorialSpawner;

    private bool hasBeenInitializedOnce = false;

    public virtual void Initialize(UIManager ui, ARWayfindingManager wayfinding, ThesisManager thesis)
    {
        uiManager = ui;
        wayfindingManager = wayfinding;
        thesisManager = thesis;
        if (memorialSpawner == null)
            memorialSpawner = UnityEngine.Object.FindAnyObjectByType<MemorialSpawner>(FindObjectsInactive.Include);

        if (hasBeenInitializedOnce) return;
        hasBeenInitializedOnce = true;
        OnInitialize();
    }

    protected virtual void OnInitialize() { }

    /// <summary>
    /// Shared helper to calculate ground-snapped spawn coordinates in front of the user's camera.
    /// Samples NavMesh topology first, then physics raycasts downward to ensure avatar feet attach to ground.
    /// </summary>
    protected Vector3 CalculateGroundSpawnPosition(Transform userCameraTransform, float forwardDistance = 2.5f)
    {
        if (userCameraTransform == null && Camera.main != null)
            userCameraTransform = Camera.main.transform;

        if (userCameraTransform == null) return transform.position;

        Vector3 rayOrigin = userCameraTransform.position + (userCameraTransform.forward * forwardDistance);

        // 1. Try NavMesh sampling first (snaps Y directly to walkable mesh)
        if (NavMesh.SamplePosition(rayOrigin, out NavMeshHit navHit, 4.0f, NavMesh.AllAreas))
        {
            return navHit.position;
        }

        // 2. Try Physics Raycast downward to find physical terrain/ground colliders
        if (Physics.Raycast(rayOrigin + (Vector3.up * 2.0f), Vector3.down, out RaycastHit groundHit, 10.0f))
        {
            return groundHit.point;
        }

        // 3. Fallback: subtract standard human eye height (~1.6m) from camera Y
        rayOrigin.y = userCameraTransform.position.y - 1.6f;
        return rayOrigin;
    }

    public abstract void OnMemorialSelected(string memorialID);
    public abstract void OnMemorialDeselected();
    public abstract void OnMemorialReached(string memorialID);
}
```

---

### B. Intermediate Guidance Implementation ([`IntermediateGuidance.cs`](file:///d:/uni/ERASMUS/TESI%20SDU/progetto/ThesisAR/Assets/Scripts/Guidance/IntermediateGuidance.cs))

```csharp
public override void OnMemorialSelected(string memorialID)
{
    ActiveGuideAvatarRegistry.ClaimOwnership(this);
    UIManager.DespawnAllGuidanceAvatarsExcept(this);

    bool isWelcome = string.IsNullOrEmpty(memorialID) || memorialID.StartsWith("WELCOME", System.StringComparison.OrdinalIgnoreCase);

    Vector3 spawnPos;
    Quaternion spawnRot;

    if (isWelcome)
    {
        memorialID = "WELCOME_INTERMEDIATE";
        activeTargetID = memorialID;

        Transform cam = Camera.main != null ? Camera.main.transform : null;
        spawnPos = CalculateGroundSpawnPosition(cam, 2.5f);

        Vector3 lookDir = cam != null ? (cam.position - spawnPos).normalized : Vector3.back;
        lookDir.y = 0f;
        spawnRot = lookDir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(lookDir) : Quaternion.identity;
    }
    else
    {
        // Stone selection positioning...
    }

    ActivateSharedAvatarAt(spawnPos, spawnRot, memorialID);
}

public void SummonAvatarToUser()
{
    ActiveGuideAvatarRegistry.ClaimOwnership(this);

    Transform cam = Camera.main != null ? Camera.main.transform : null;
    Vector3 spawnPos = CalculateGroundSpawnPosition(cam, 2.0f);

    Vector3 lookDir = cam != null ? (cam.position - spawnPos).normalized : Vector3.back;
    lookDir.y = 0f;
    Quaternion spawnRot = lookDir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(lookDir) : Quaternion.identity;

    if (guideAvatarInstance == null)
    {
        guideAvatarInstance = (ThesisManager.Instance != null ? ThesisManager.Instance.GuideAvatarInstance : null)
            ?? ActiveGuideAvatarRegistry.SingleAvatarInstance;
    }

    if (guideAvatarInstance == null && guideAvatarPrefab != null)
    {
        guideAvatarInstance = Instantiate(guideAvatarPrefab, spawnPos, spawnRot, null);
        guideAvatarInstance.name = "SingleGuideAvatarInstance";
    }

    if (guideAvatarInstance != null)
    {
        ActiveGuideAvatarRegistry.RegisterSingleAvatarInstance(guideAvatarInstance);
        ActiveGuideAvatarRegistry.ApplyIntermediateHologramVisuals();

        NavMeshAgent dummyAgent = null;
        ActiveGuideAvatarRegistry.AssignComponents(ref avatarAnimator, ref avatarAudioSource, ref dummyAgent);

        guideAvatarInstance.transform.position = spawnPos;
        guideAvatarInstance.transform.rotation = spawnRot;

        HologramEffectController hologramEffect = guideAvatarInstance.GetComponent<HologramEffectController>();
        if (hologramEffect != null)
        {
            hologramEffect.RefreshBasePosition();
        }

        guideAvatarInstance.SetActive(true);
    }

    if (uiManager != null)
    {
        bool isGerman = uiManager.SelectedLanguage == "german";
        uiManager.ShowNotificationToast(
            isGerman ? "🙋 Hologramm an deiner Seite" : "🙋 Hologram Guide Beside You",
            isGerman ? "Das Hologramm steht an deiner Seite." : "The hologram guide is beside you."
        );
    }
}
```

---

## ❓ Questions for Review (Claude Verification)

1. Does the proposed `CalculateGroundSpawnPosition` helper in `GuidanceSystemBase` cleanly solve ground snapping for both AR/VR and Unity Editor play testing?
2. Is the re-wiring strategy in `UIManager.Start()` using `FindGameObjectIncludingInactive` optimal for overriding hardcoded scene `Button.onClick` UnityEvents?
3. Does decoupling `SummonAvatarToUser()` from voice clip playback in `IntermediateGuidance.cs` correctly mirror `PersonalGuidance.cs` behavior?
