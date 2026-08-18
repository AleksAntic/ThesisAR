using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Condition B (Intermediate): Static contextual hologram avatar guidance.
/// Clean-slate rewrite — single activation pipeline, direct coroutine speech monitoring.
/// </summary>
public class IntermediateGuidance : GuidanceSystemBase, ActiveGuideAvatarRegistry.IAvatarOwner
{
    public static IntermediateGuidance Instance { get; private set; }

    [Header("Avatar Spawn Settings")]
    [SerializeField] private GameObject guideAvatarPrefab;
    [SerializeField] private Vector3 spawnOffsetFromMemorial = new Vector3(1.2f, 0f, 1.2f);

    [Header("Ask-More Button")]
    [SerializeField] private AskMoreButtonController askMoreButton;
    [SerializeField] private TextAsset stoneSymbolsMapJson;
    [SerializeField] private AppLanguage currentLanguage = AppLanguage.EN;
    [SerializeField] private float topicSwitchFadeDuration = 0.4f;

    [Header("Dissolve & Fade Out Settings")]
    [Tooltip("Duration in seconds for the avatar to smoothly dissolve when audio finishes.")]
    [SerializeField, Range(0.5f, 5.0f)] private float avatarDissolveDuration = 2.0f;
    [Tooltip("Easing curve for the dissolve opacity (1 = fully opaque, 0 = invisible). Adjust directly in the Inspector!")]
    [SerializeField] private AnimationCurve avatarDissolveCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    private GameObject guideAvatarInstance;
    private Animator avatarAnimator;
    private AudioSource avatarAudioSource;
    private CompanionIKController ikController;
    private Coroutine topicPlaybackRoutine;
    private string activeTargetID;
    private bool isPlayingAuxiliaryTopic;
    private string lastPlayedAudioID;
    private bool isCurrentlyMarkedTalking = false;
    private int nextCampInfoIndex;
    private List<string> activeSymbolKeys;
    private Dictionary<string, List<string>> stoneSymbolsMap;

    // ── Lifecycle ──────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;
    }

    protected override void OnInitialize()
    {
        ParseStoneSymbolsMap();

        if (askMoreButton == null)
            askMoreButton = Object.FindAnyObjectByType<AskMoreButtonController>(FindObjectsInactive.Include);

        if (askMoreButton != null)
        {
            askMoreButton.OnTopicSelected += HandleTopicSelected;
            askMoreButton.SetLanguage(currentLanguage);
            // It belongs to the Intermediate experience, not to a specific stop.
            askMoreButton.Configure(false);
        }

        if (NarrationManager.Instance != null)
        {
            NarrationManager.Instance.OnNarrationFinished -= HandleNarrationFinished;
            NarrationManager.Instance.OnNarrationFinished += HandleNarrationFinished;
        }
    }

    private void OnEnable()
    {
        if (askMoreButton == null)
            askMoreButton = Object.FindAnyObjectByType<AskMoreButtonController>(FindObjectsInactive.Include);
        if (askMoreButton != null)
        {
            askMoreButton.OnTopicSelected -= HandleTopicSelected;
            askMoreButton.OnTopicSelected += HandleTopicSelected;
            askMoreButton.SetLanguage(currentLanguage);
            askMoreButton.Configure(false);
        }
        if (NarrationManager.Instance != null)
        {
            NarrationManager.Instance.OnNarrationFinished -= HandleNarrationFinished;
            NarrationManager.Instance.OnNarrationFinished += HandleNarrationFinished;
        }
    }

    private void OnDisable()
    {
        if (NarrationManager.Instance != null)
        {
            NarrationManager.Instance.OnNarrationFinished -= HandleNarrationFinished;
        }
        if (askMoreButton != null) askMoreButton.Hide();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (askMoreButton != null) askMoreButton.OnTopicSelected -= HandleTopicSelected;
        if (NarrationManager.Instance != null) NarrationManager.Instance.OnNarrationFinished -= HandleNarrationFinished;
        ActiveGuideAvatarRegistry.ReleaseOwnership(this);
    }

    // ── Update: IK head-gaze tracking ─────────────────────────

    private void Update()
    {
        if (guideAvatarInstance == null || !guideAvatarInstance.activeSelf) return;

        // Force fresh references to avoid stale components from despawned avatars
        avatarAnimator = ActiveGuideAvatarRegistry.AvatarAnimator;
        avatarAudioSource = ActiveGuideAvatarRegistry.AvatarAudioSource;
        if (ikController == null) ikController = guideAvatarInstance.GetComponent<CompanionIKController>();

        UpdateAnimationState();

        if (ikController != null)
        {
            Transform cam = Camera.main != null ? Camera.main.transform : null;
            bool isTalking = avatarAnimator != null && avatarAnimator.GetBool("IsTalking");
            ikController.SetTrackingTargets(cam, null, isTalking);

        }
    }

    private Coroutine fadeDespawnRoutine;

    private void EnsureActiveInHierarchy()
    {
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
            if (!gameObject.activeInHierarchy && transform.parent != null)
            {
                Debug.LogWarning("[IntermediateGuidance] Parent is inactive! Detaching to root to ensure coroutines and Update can run.");
                transform.SetParent(null);
                gameObject.SetActive(true);
            }
        }
    }

    private void UpdateAnimationState()
    {
        if (avatarAnimator == null) return;
        avatarAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        isCurrentlyMarkedTalking = avatarAnimator.GetBool("IsTalking");

        bool audioActuallyPlaying = false;
        if (avatarAudioSource != null && avatarAudioSource.isPlaying) audioActuallyPlaying = true;
        if (NarrationManager.Instance != null && NarrationManager.Instance.IsSpeaking) audioActuallyPlaying = true;

        if (audioActuallyPlaying && !isCurrentlyMarkedTalking)
        {
            if (fadeDespawnRoutine != null) { StopCoroutine(fadeDespawnRoutine); fadeDespawnRoutine = null; }
            SetTalkingState(true);
        }
        else if (!audioActuallyPlaying && isCurrentlyMarkedTalking)
        {
            SetTalkingState(false);
            if (guideAvatarInstance != null && guideAvatarInstance.activeSelf && fadeDespawnRoutine == null)
            {
                fadeDespawnRoutine = StartCoroutine(FadeOutAndDespawn(avatarDissolveDuration));
            }
        }
    }

    private void HandleNarrationFinished()
    {
        if (this == null || !gameObject.activeInHierarchy || !enabled || guideAvatarInstance == null) return;
        if (NarrationManager.Instance == null || NarrationManager.Instance.CurrentAudioSource != avatarAudioSource) return;
        if (avatarAudioSource != null && avatarAudioSource.isPlaying)
            avatarAudioSource.Stop();
            
        SetTalkingState(false);
        if (guideAvatarInstance != null && guideAvatarInstance.activeSelf && fadeDespawnRoutine == null)
        {
            fadeDespawnRoutine = StartCoroutine(FadeOutAndDespawn(avatarDissolveDuration));
        }

        TourManager tourManager = UnityEngine.Object.FindAnyObjectByType<TourManager>(FindObjectsInactive.Include);
        if (!isPlayingAuxiliaryTopic && tourManager != null && tourManager.IsTourActiveAndRunning &&
            string.Equals(tourManager.GetCurrentTargetStoneID(), activeTargetID, System.StringComparison.OrdinalIgnoreCase))
        {
            // Intermediate is a waypoint guide, not a walking companion: after the
            // on-site narration, immediately route the visitor to the next stop.
            tourManager.AdvanceToNextStop();
        }
    }

    // ── Public API ─────────────────────────────────────────────

    public void ConfigureAvatarPrefab(GameObject prefab) => guideAvatarPrefab = prefab;

    public void SetLanguage(AppLanguage lang)
    {
        currentLanguage = lang;
        if (askMoreButton != null) askMoreButton.SetLanguage(currentLanguage);
    }

    // ── Memorial selection & activation ────────────────────────

    public override void OnMemorialSelected(string memorialID)
    {
        if (IsActiveTourStop(memorialID))
        {
            // Do not narrate from a distance: route to the waypoint first.
            activeTargetID = memorialID;
            if (wayfindingManager != null) wayfindingManager.NavigateTo(memorialID);
            return;
        }

        BeginMemorialNarration(memorialID);
    }

    private void BeginMemorialNarration(string memorialID)
    {
        isPlayingAuxiliaryTopic = false;
        EnsureActiveInHierarchy();
        ActiveGuideAvatarRegistry.ClaimOwnership(this);
        UIManager.DespawnAllGuidanceAvatarsExcept(this);

        bool isWelcome = string.IsNullOrEmpty(memorialID) || memorialID.StartsWith("WELCOME", System.StringComparison.OrdinalIgnoreCase);
        string narrationId = isWelcome ? "WELCOME_INTERMEDIATE" : memorialID;
        activeTargetID = narrationId;

        Transform cam = Camera.main != null ? Camera.main.transform : null;
        Vector3 spawnPos;
        Quaternion spawnRot;

        if (isWelcome)
        {
            spawnPos = CalculateGroundSpawnPosition(cam, 2.5f);
            spawnRot = FaceCamera(spawnPos, cam);
        }
        else
        {
            GameObject memorialTarget = memorialSpawner != null ? memorialSpawner.GetSpawnedMemorial(memorialID) : null;
            if (memorialTarget != null)
            {
                spawnPos = memorialTarget.transform.position + spawnOffsetFromMemorial;
                Vector3 lookDir = (memorialTarget.transform.position - spawnPos).normalized;
                lookDir.y = 0f;
                spawnRot = lookDir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(lookDir) : Quaternion.identity;
            }
            else
            {
                spawnPos = CalculateGroundSpawnPosition(cam, 3.5f);
                spawnRot = FaceCamera(spawnPos, cam);
            }
        }

        ActivateHologramAt(spawnPos, spawnRot, narrationId, triggerAudio: true);

        if (thesisManager != null)
            thesisManager.LogEvent("memorial_selected", narrationId, "intermediate");
    }

    public override void OnMemorialDeselected()
    {
        if (guideAvatarInstance != null && guideAvatarInstance.activeSelf)
            StartCoroutine(FadeOutAndDespawn(avatarDissolveDuration));

        activeSymbolKeys = null;
        if (askMoreButton != null) askMoreButton.Configure(false);

        if (thesisManager != null)
            thesisManager.LogEvent("memorial_deselected", string.Empty, "intermediate");
    }

    public override void OnMemorialReached(string memorialID)
    {
        if (thesisManager != null)
            thesisManager.LogEvent("memorial_reached", memorialID, "intermediate");
        BeginMemorialNarration(memorialID);
    }

    private static bool IsActiveTourStop(string memorialID)
    {
        TourManager tourManager = UnityEngine.Object.FindAnyObjectByType<TourManager>(FindObjectsInactive.Include);
        return tourManager != null && tourManager.IsTourActiveAndRunning &&
               string.Equals(tourManager.GetCurrentTargetStoneID(), memorialID, System.StringComparison.OrdinalIgnoreCase);
    }

    // ── Summon Guide ───────────────────────────────────────────

    public void SummonAvatarToUser()
    {
        if (ThesisManager.Instance != null && ThesisManager.Instance.CurrentMode != ThesisManager.GuidanceMode.Intermediate)
            return;

        EnsureActiveInHierarchy();
        ActiveGuideAvatarRegistry.ClaimOwnership(this);

        Transform cam = Camera.main != null ? Camera.main.transform : null;
        Vector3 spawnPos = CalculateGroundSpawnPosition(cam, 2.0f);
        Quaternion spawnRot = FaceCamera(spawnPos, cam);

        ActivateHologramAt(spawnPos, spawnRot, activeTargetID, triggerAudio: false);

        if (uiManager != null)
        {
            bool isGerman = uiManager.SelectedLanguage == "german";
            uiManager.ShowNotificationToast("Hologram Guide",
                isGerman ? "Hologramm an deiner Seite." : "Hologram Guide beside you.");
        }
    }

    // ── Despawn ────────────────────────────────────────────────

    public void ForceDespawnImmediate()
    {
        if (topicPlaybackRoutine != null) { StopCoroutine(topicPlaybackRoutine); topicPlaybackRoutine = null; }
        if (avatarAudioSource != null && avatarAudioSource.isPlaying) avatarAudioSource.Stop();
        SetTalkingState(false);
        if (guideAvatarInstance != null) guideAvatarInstance.SetActive(false);
    }

    public void DespawnAvatar()
    {
        ForceDespawnImmediate();
        ActiveGuideAvatarRegistry.ReleaseOwnership(this);
    }

    // ── Single Activation Pipeline ─────────────────────────────

    private void ActivateHologramAt(Vector3 spawnPos, Quaternion spawnRot, string narrationId, bool triggerAudio)
    {
        if (guideAvatarInstance == null)
        {
            guideAvatarInstance = (ThesisManager.Instance != null ? ThesisManager.Instance.GuideAvatarInstance : null)
                ?? ActiveGuideAvatarRegistry.SingleAvatarInstance;
        }
        if (guideAvatarPrefab == null && ThesisManager.Instance != null)
            guideAvatarPrefab = ThesisManager.Instance.GuideAvatarPrefab;

        if (guideAvatarInstance == null && guideAvatarPrefab != null)
        {
            guideAvatarInstance = Instantiate(guideAvatarPrefab, spawnPos, spawnRot, null);
            guideAvatarInstance.name = "SingleGuideAvatarInstance";
        }
        if (guideAvatarInstance == null) return;

        ActiveGuideAvatarRegistry.RegisterSingleAvatarInstance(guideAvatarInstance);
        ActiveGuideAvatarRegistry.ApplyIntermediateHologramVisuals();

        ikController = guideAvatarInstance.GetComponent<CompanionIKController>();
        if (ikController == null) ikController = guideAvatarInstance.AddComponent<CompanionIKController>();

        UnityEngine.AI.NavMeshAgent dummyAgent = null;
        ActiveGuideAvatarRegistry.AssignComponents(ref avatarAnimator, ref avatarAudioSource, ref dummyAgent);

        if (avatarAnimator != null)
        {
            avatarAnimator.applyRootMotion = false;
            avatarAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            avatarAnimator.SetFloat("Speed", 0f);
        }
        if (avatarAudioSource != null)
        {
            avatarAudioSource.loop = false;
        }

        guideAvatarInstance.transform.position = spawnPos;
        guideAvatarInstance.transform.rotation = spawnRot;

        HologramEffectController hologramEffect = guideAvatarInstance.GetComponent<HologramEffectController>();
        if (hologramEffect != null) hologramEffect.RefreshBasePosition();

        guideAvatarInstance.SetActive(true);

        if (triggerAudio && !string.IsNullOrEmpty(narrationId))
        {
            if (NarrationManager.Instance != null)
                NarrationManager.Instance.PlayNarration(narrationId, avatarAudioSource);
            
            lastPlayedAudioID = narrationId;
            SetTalkingState(true);
            ConfigureAskMoreButtonFor(narrationId);
        }
    }

    private void SetTalkingState(bool talking)
    {
        isCurrentlyMarkedTalking = talking;
        if (avatarAnimator == null) return;
        avatarAnimator.SetBool("IsTalking", talking);
        if (avatarAnimator.layerCount > 1)
            avatarAnimator.SetLayerWeight(1, talking ? 1.0f : 0.0f);
    }

    // ── Hologram Fade-Out ──────────────────────────────────────

    private IEnumerator FadeOutAndDespawn(float fadeDuration)
    {
        if (guideAvatarInstance == null || !guideAvatarInstance.activeSelf) yield break;
        SetTalkingState(false);

        Renderer[] renderers = guideAvatarInstance.GetComponentsInChildren<Renderer>(true);
        List<(Material mat, Color baseColor, string propName)> matList = new List<(Material, Color, string)>();

        foreach (Renderer rend in renderers)
        {
            if (rend == null) continue;
            foreach (Material m in rend.materials)
            {
                if (m == null) continue;
                if (m.HasProperty("_BaseColor"))
                {
                    matList.Add((m, m.GetColor("_BaseColor"), "_BaseColor"));
                }
                if (m.HasProperty("_Color"))
                {
                    matList.Add((m, m.GetColor("_Color"), "_Color"));
                }
            }
        }

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(t / fadeDuration);
            // Professional easing via AnimationCurve (falls back to SmoothStep if null)
            float alphaFactor = (avatarDissolveCurve != null && avatarDissolveCurve.length > 0)
                ? avatarDissolveCurve.Evaluate(normalizedTime)
                : Mathf.SmoothStep(1f, 0f, normalizedTime);

            foreach (var (mat, baseColor, propName) in matList)
            {
                Color c = baseColor;
                c.a = baseColor.a * alphaFactor;
                mat.SetColor(propName, c);
            }

            yield return null;
        }

        if (guideAvatarInstance != null)
        {
            guideAvatarInstance.SetActive(false);

            // Restore initial colors for future spawns
            foreach (var (mat, baseColor, propName) in matList)
            {
                mat.SetColor(propName, baseColor);
            }
        }

        DespawnAvatar();
        fadeDespawnRoutine = null;
    }

    // ── Ask-More Button & Topics ───────────────────────────────

    private void ConfigureAskMoreButtonFor(string memorialID)
    {
        if (askMoreButton == null) return;
        bool hasSymbols = stoneSymbolsMap != null && stoneSymbolsMap.TryGetValue(memorialID, out activeSymbolKeys) && activeSymbolKeys.Count > 0;
        if (!hasSymbols) activeSymbolKeys = null;
        askMoreButton.Configure(hasSymbols);
    }

    public void PlaySpecificSymbol(string symbolKey)
    {
        EnsureActiveInHierarchy();
        if (topicPlaybackRoutine != null) StopCoroutine(topicPlaybackRoutine);
        topicPlaybackRoutine = StartCoroutine(PlaySpecificSymbolRoutine(symbolKey));
    }

    private IEnumerator PlaySpecificSymbolRoutine(string symbolKey)
    {
        string langSuffix = currentLanguage.ToFileSuffix();
        string assetPath = $"Symbol_{symbolKey}_{langSuffix}";
        if (NarrationManager.Instance != null)
            NarrationManager.Instance.PlayNarration(assetPath, avatarAudioSource);
        lastPlayedAudioID = assetPath;
        SetTalkingState(true);
        yield break;
    }

    private void HandleTopicSelected(AskMoreButtonController.Topic topic)
    {
        EnsureActiveInHierarchy();
        if (topicPlaybackRoutine != null) StopCoroutine(topicPlaybackRoutine);
        topicPlaybackRoutine = StartCoroutine(SwitchTopicRoutine(topic));
    }

    private IEnumerator SwitchTopicRoutine(AskMoreButtonController.Topic topic)
    {
        string langSuffix = currentLanguage.ToString().ToUpper();
        isPlayingAuxiliaryTopic = true;

        // Extra content is a visitor interaction, so keep the guide beside the
        // visitor instead of leaving it at a later tour waypoint.
        Transform visitorCamera = Camera.main != null ? Camera.main.transform : null;
        if (visitorCamera != null)
        {
            Vector3 visitorPosition = CalculateGroundSpawnPosition(visitorCamera, 2.0f);
            ActivateHologramAt(visitorPosition, FaceCamera(visitorPosition, visitorCamera), activeTargetID, triggerAudio: false);
        }
        
        if (guideAvatarInstance == null || !guideAvatarInstance.activeSelf)
        {
            Transform cam = Camera.main != null ? Camera.main.transform : null;
            Vector3 spawnPos;
            Quaternion spawnRot;
            GameObject memorialTarget = (memorialSpawner != null && !string.IsNullOrEmpty(activeTargetID)) ? memorialSpawner.GetSpawnedMemorial(activeTargetID) : null;
            if (memorialTarget != null)
            {
                spawnPos = memorialTarget.transform.position + spawnOffsetFromMemorial;
                Vector3 lookDir = (memorialTarget.transform.position - spawnPos).normalized;
                lookDir.y = 0f;
                spawnRot = lookDir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(lookDir) : Quaternion.identity;
            }
            else
            {
                spawnPos = CalculateGroundSpawnPosition(cam, 2.5f);
                spawnRot = FaceCamera(spawnPos, cam);
            }
            
            ActivateHologramAt(spawnPos, spawnRot, "", triggerAudio: false);
        }

        if (fadeDespawnRoutine != null) { StopCoroutine(fadeDespawnRoutine); fadeDespawnRoutine = null; }

        if (topic == AskMoreButtonController.Topic.Repeat)
        {
            string clipToPlay = !string.IsNullOrEmpty(lastPlayedAudioID) ? lastPlayedAudioID : activeTargetID;
            if (NarrationManager.Instance != null && !string.IsNullOrEmpty(clipToPlay)) 
                NarrationManager.Instance.PlayNarration(clipToPlay, avatarAudioSource);
            lastPlayedAudioID = clipToPlay;
            SetTalkingState(true);
            yield break;
        }

        if (topic == AskMoreButtonController.Topic.CampInfo)
        {
            // Info and Fact deliberately use distinct pools. Add future Info
            // entries here only after their dedicated clip and subtitle exist.
            string[] campInfoClips = { "Area_WomensCamp", "Area_ExchangeCamp", "Area_MassGraveL" };
            string campInfoClip = campInfoClips[nextCampInfoIndex++ % campInfoClips.Length] + "_" + langSuffix;
            if (NarrationManager.Instance != null) NarrationManager.Instance.PlayNarration(campInfoClip, avatarAudioSource);
            lastPlayedAudioID = campInfoClip;
            SetTalkingState(true);
            yield break;
        }

        if (topic == AskMoreButtonController.Topic.RandomFacts)
        {
            if (NarrationManager.Instance != null)
            {
                string[] historicalWalkChapters = new string[] {
                    "CampOrigins", "ExchangeCampAnomaly", "JosefKramer", "CampPyres", 
                    "DeathMarches", "RegistryDestruction", "PowCemetery", "CrematoriumDiaries", 
                    "LiberationDrama", "BarracksBurning", "DisplacedPersons", "BelsenTrials"
                };
                string candidate = historicalWalkChapters[UnityEngine.Random.Range(0, historicalWalkChapters.Length)];
                string fullId = candidate + "_" + langSuffix;
                
                NarrationManager.Instance.PlayNarration(fullId, avatarAudioSource);
                lastPlayedAudioID = fullId;
                SetTalkingState(true);
            }
            yield break;
        }

        if (activeSymbolKeys == null || activeSymbolKeys.Count == 0) yield break;

        foreach (string symbolKey in activeSymbolKeys)
        {
            string assetPath = $"Symbol_{symbolKey}_{langSuffix}";
            if (NarrationManager.Instance != null)
                NarrationManager.Instance.PlayNarration(assetPath, avatarAudioSource);
            lastPlayedAudioID = assetPath;
            SetTalkingState(true);

            if (avatarAudioSource != null && avatarAudioSource.clip != null)
                yield return new WaitForSeconds(avatarAudioSource.clip.length + 0.3f);
            else
                yield return new WaitForSeconds(4.0f);
        }
        SetTalkingState(false);
    }

    private IEnumerator FadeOutAvatarAudio()
    {
        if (avatarAudioSource == null || !avatarAudioSource.isPlaying) yield break;
        float startVolume = avatarAudioSource.volume;
        float t = 0f;
        while (t < topicSwitchFadeDuration && avatarAudioSource.volume > 0f)
        {
            t += Time.deltaTime;
            avatarAudioSource.volume = Mathf.Lerp(startVolume, 0f, t / topicSwitchFadeDuration);
            yield return null;
        }
        avatarAudioSource.Stop();
        avatarAudioSource.volume = startVolume;
    }



    // ── Stone Symbols Map ──────────────────────────────────────

    private void ParseStoneSymbolsMap()
    {
        stoneSymbolsMap = new Dictionary<string, List<string>>();
        if (stoneSymbolsMapJson == null) return;

        StoneSymbolsMapWrapper wrapper = JsonUtility.FromJson<StoneSymbolsMapWrapper>(stoneSymbolsMapJson.text);
        if (wrapper?.entries == null) return;

        foreach (StoneSymbolsEntry entry in wrapper.entries)
            stoneSymbolsMap[entry.stoneId] = new List<string>(entry.symbols);
    }

    [System.Serializable] private class StoneSymbolsEntry { public string stoneId; public string[] symbols; }
    [System.Serializable] private class StoneSymbolsMapWrapper { public StoneSymbolsEntry[] entries; }

    // ── Helpers ─────────────────────────────────────────────────

    private static Quaternion FaceCamera(Vector3 fromPos, Transform cam)
    {
        if (cam == null) return Quaternion.identity;
        Vector3 lookDir = (cam.position - fromPos).normalized;
        lookDir.y = 0f;
        return lookDir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(lookDir) : Quaternion.identity;
    }
}
