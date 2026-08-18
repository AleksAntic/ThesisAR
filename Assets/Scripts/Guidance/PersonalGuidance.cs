using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// Condition C (Personal): Fully dynamic embodied companion navigation.
/// Implements real-time companion front-leading tethering, injects dynamic IK components,
/// and executes asynchronous proximity tracking to trigger narrative detour switches seamlessly.
/// All variables, comments, and structure are strictly maintained in English.
/// </summary>
public class PersonalGuidance : GuidanceSystemBase, ActiveGuideAvatarRegistry.IAvatarOwner
{
    public static PersonalGuidance Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[PersonalGuidance] Duplicate instance detected on GameObject '{gameObject.name}' - disabling duplicate component.");
            enabled = false;
            return;
        }
        Instance = this;

        Debug.Log($"[INSTANCE TRACKER] PersonalGuidance Awake() on GameObject '{gameObject.name}', path: {GetFullHierarchyPath(transform)}");
    }

    private static string GetFullHierarchyPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    // Implementation of ActiveGuideAvatarRegistry.IAvatarOwner interface
    public void ForceDespawnImmediate()
    {
        DespawnAvatar();
    }

    [Header("🏃 Companion Spawn Settings")]
    [SerializeField] private GameObject guideCharacterPrefab;
    [SerializeField] private Vector3 characterSpawnOffset = new Vector3(0f, 0f, 3.5f);
    [Tooltip("Offset rotation angle (in degrees) to compensate for custom 3D model pivots (e.g. -90 for Blender/Mixamo exports).")]

    [Header("⚡ Elastic Pacing Configuration")]
    [SerializeField] private float stopDistanceThreshold = 5.0f;
    [SerializeField] private float resumeDistanceThreshold = 2.5f;

    [Header("🕵️‍♂️ Detour Tracking Configurations")]
    [SerializeField] private float detourTriggerRadius = 4.0f;     // Meters within an unscheduled stone to trigger detour

    [Header("👣 Footstep Audio")]
    [Tooltip("One or more footstep sounds; a random one plays each step for natural variation.")]
    [SerializeField] private AudioClip[] footstepClips;
    [SerializeField] private AudioSource footstepAudioSource;
    [Tooltip("Distance in meters the character must travel between two footstep sounds.")]
    [SerializeField] private float stepDistanceInterval = 0.75f;
    [Tooltip("Below this speed the character is considered stationary; no footsteps play.")]
    [SerializeField] private float minSpeedToStep = 0.05f;
    [SerializeField] private Vector2 pitchRandomRange = new Vector2(0.95f, 1.05f);

    private Vector3 lastFootstepPosition;
    private bool footstepTrackerInitialized = false;

    private GameObject guideCharacterInstance;
    private NavMeshAgent agentEngine;
    private Animator characterAnimator;
    private AudioSource characterAudioSource;
    private Transform userCameraTransform;
    private Transform userMovementTransform;
    private Vector3 previousUserPosition;
    private bool hasPreviousUserPosition;
    private float observedUserSpeed;
    private CompanionIKController ikController;

    private string activeTargetID;
    private string previousTargetID;
    private GameObject targetNodeObject;
    private bool isTrackingMovement = false;
    private bool isWaitingForUser = false;
    private bool pendingDestinationSet = false; // Deferred destination flag for NavMesh initialization
    private bool hasArrivedAtActiveTarget = false;
    private float detourScanTimer = 0f;
    private float walkNarrationTimer = 0f;
    private const float detourScanInterval = 0.5f; // Distributed processing constraint

#if UNITY_EDITOR
    private void OnValidate()
    {
        memorialStandOffDistance = 0.5f;
    }
#endif
    private void ConfigureAgentNavigationMask()
    {
        if (agentEngine != null) agentEngine.areaMask = NavigationAreaMask.VisitorWalkable;
    }

    public GameObject GetAvatarInstance()
    {
        return guideCharacterInstance;
    }

    public bool IsTrackingMovement => isTrackingMovement;

    public void DespawnAvatar()
    {
        if (guideCharacterInstance != null)
        {
            guideCharacterInstance.SetActive(false);
        }

        ActiveGuideAvatarRegistry.ReleaseOwnership(this);
    }

    /// <summary>
    /// Teleports 3D companion avatar 2.0 meters directly in front of camera facing visitor.
    /// Cancels active path movement so avatar remains stationary by visitor.
    /// </summary>
    public void SummonAvatarToUser()
    {
        if (ThesisManager.Instance != null && ThesisManager.Instance.CurrentMode != ThesisManager.GuidanceMode.Personal)
        {
            Debug.Log("[PersonalGuidance] Ignoring SummonAvatarToUser because current mode is not Personal.");
            return;
        }

        ActiveGuideAvatarRegistry.ClaimOwnership(this);

        if (userCameraTransform == null && Camera.main != null)
            userCameraTransform = Camera.main.transform;
        if (userCameraTransform == null) return;

        Vector3 targetSpawnPos = userCameraTransform.position + (userCameraTransform.forward * 2.0f);
        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetSpawnPos, out hit, 3.0f, NavigationAreaMask.VisitorWalkable))
        {
            targetSpawnPos = hit.position;
        }

        if (guideCharacterInstance == null)
        {
            guideCharacterInstance = (ThesisManager.Instance != null ? ThesisManager.Instance.GuideAvatarInstance : null)
                ?? ActiveGuideAvatarRegistry.SingleAvatarInstance;
        }

        if (guideCharacterInstance == null)
        {
            if (guideCharacterPrefab != null)
            {
                guideCharacterInstance = Instantiate(guideCharacterPrefab, targetSpawnPos, Quaternion.identity);
                guideCharacterInstance.name = "SingleGuideAvatarInstance";
                agentEngine = guideCharacterInstance.GetComponent<NavMeshAgent>();
                if (agentEngine == null) agentEngine = guideCharacterInstance.AddComponent<NavMeshAgent>();
                characterAnimator = guideCharacterInstance.GetComponentInChildren<Animator>();
                characterAudioSource = guideCharacterInstance.GetComponentInChildren<AudioSource>();
                if (characterAudioSource == null) characterAudioSource = guideCharacterInstance.AddComponent<AudioSource>();
                if (characterAnimator != null) characterAnimator.applyRootMotion = false;
                LogAnimatorTalkingParameterDiagnostics();
            }
            else
            {
                Debug.LogWarning("[PersonalGuidance] Cannot summon avatar: guideCharacterPrefab is null.");
                return;
            }
        }

        ActiveGuideAvatarRegistry.RegisterSingleAvatarInstance(guideCharacterInstance);
        ActiveGuideAvatarRegistry.ApplyPersonalPBRVisuals();
        guideCharacterInstance.SetActive(true);

        if (agentEngine == null) agentEngine = guideCharacterInstance.GetComponent<NavMeshAgent>();
        if (characterAnimator == null) characterAnimator = guideCharacterInstance.GetComponentInChildren<Animator>();
        if (characterAudioSource == null) characterAudioSource = guideCharacterInstance.GetComponentInChildren<AudioSource>();

        // GUARANTEED MOVEMENT: Always move the single physical transform position first
        guideCharacterInstance.transform.position = targetSpawnPos;

        if (agentEngine != null && agentEngine.enabled)
        {
            ConfigureAgentNavigationMask();
            agentEngine.ResetPath();
            if (agentEngine.isOnNavMesh)
            {
                agentEngine.Warp(targetSpawnPos);
            }
            agentEngine.isStopped = true;
        }

        // Face visitor
        Vector3 directionToUser = (userCameraTransform.position - targetSpawnPos).normalized;
        directionToUser.y = 0f;
        if (directionToUser.sqrMagnitude > 0.001f && guideCharacterInstance != null)
        {
            guideCharacterInstance.transform.rotation = Quaternion.LookRotation(directionToUser);
        }

        isTrackingMovement = false;
        isWaitingForUser = true;
        if (characterAnimator != null) characterAnimator.SetFloat("Speed", 0f);

        if (uiManager != null)
        {
            bool isGerman = uiManager.SelectedLanguage == "german";
            string title = isGerman ? "Begleiter bereit" : "Guide ready";
            string msg = isGerman ? "Der Begleiter ist an deiner Seite. Setze den Rundgang fort." : "The guide companion is beside you. Continue walking to proceed.";
            uiManager.ShowNotificationToast(title, msg);
        }

        Debug.Log("[PersonalGuidance] Summoned 3D Guide avatar directly to user location with Personal PBR visuals.");
    }

    protected override void OnInitialize()
    {
        stopDistanceThreshold = 8.0f;     // Increased to prevent premature snapping lockouts
        resumeDistanceThreshold = 4.0f;   // Increased to resume leading more easily
        memorialStandOffDistance = 0.5f;

        if (Camera.main != null)
        {
            userCameraTransform = Camera.main.transform;
        }
        if (userMovementTransform == null)
        {
            GameObject simulatedPlayer = GameObject.Find("Simulated_GPS_Player");
            if (simulatedPlayer != null) userMovementTransform = simulatedPlayer.transform;
        }

        if (memorialSpawner == null)
        {
            memorialSpawner = UnityEngine.Object.FindAnyObjectByType<MemorialSpawner>(FindObjectsInactive.Include);
        }

        if (NarrationManager.Instance != null)
        {
            NarrationManager.Instance.OnNarrationFinished -= HandleNarrationFinished;
            NarrationManager.Instance.OnNarrationFinished += HandleNarrationFinished;
        }
    }

    private void OnEnable()
    {
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
    }

    public void ConfigureAvatarPrefab(GameObject prefab)
    {
        guideCharacterPrefab = prefab;
    }

    private static readonly int IsTalkingHash = Animator.StringToHash("IsTalking");

    void Update()
    {
        if (guideCharacterInstance == null || agentEngine == null) return;

        // Deferred destination: if SetDestination failed during spawn because NavMesh wasn't ready,
        // retry exactly once when the agent registers on the mesh
        if (pendingDestinationSet && targetNodeObject != null && agentEngine.enabled && agentEngine.isOnNavMesh)
        {
            ConfigureAgentNavigationMask();
            if (!TryGetMemorialApproachPosition(out Vector3 approachPosition))
            {
                pendingDestinationSet = false;
                isWaitingForUser = true;
                agentEngine.isStopped = true;
                Debug.LogWarning($"[PersonalGuidance] No respectful NavMesh position is available for '{activeTargetID}'.");
                return;
            }

            agentEngine.SetDestination(approachPosition);
            agentEngine.isStopped = false;
            isTrackingMovement = true;
            isWaitingForUser = false;
            if (characterAnimator != null) characterAnimator.SetFloat("Speed", 1f);
            pendingDestinationSet = false;
            Debug.Log($"[PersonalGuidance] Deferred destination set to {targetNodeObject.name} at {targetNodeObject.transform.position}");
        }

        UpdateAnimationState();
        HandleFrontLeadingTethering();
        HandleLiveProximityDetourScanning();
        HandleFootstepAudio();
        HandleContinuousWalkingNarration();
        UpdateIKControllerTargets();
    }

    private bool isCurrentlyMarkedTalking = false;

    private void SetTalkingState(bool talking)
    {
        isCurrentlyMarkedTalking = talking;
        if (characterAnimator == null) return;

        if (talking) characterAnimator.speed = 1f;

        characterAnimator.SetBool("IsTalking", talking);
        if (characterAnimator.layerCount > 1)
        {
            characterAnimator.SetLayerWeight(1, talking ? 1.0f : 0.0f);
        }
    }

    private void UpdateAnimationState()
    {
        if (characterAnimator != null)
        {
            characterAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            bool audioActuallyPlaying = (characterAudioSource != null && characterAudioSource.isPlaying) ||
                                        (NarrationManager.Instance != null && NarrationManager.Instance.IsSpeaking);
            bool shouldAnimateTalking = audioActuallyPlaying && !isTrackingMovement;
            if (shouldAnimateTalking && !isCurrentlyMarkedTalking)
            {
                SetTalkingState(true);
            }
            else if (!shouldAnimateTalking && isCurrentlyMarkedTalking)
            {
                SetTalkingState(false);
            }
        }
    }

    /// <summary>
    /// Plays a footstep sound every time the companion has physically covered stepDistanceInterval
    /// meters, based on real NavMeshAgent displacement rather than fixed timers or animation events.
    /// This stays correct regardless of animation speed, pauses, or elastic pacing changes.
    /// </summary>
    private void HandleFootstepAudio()
    {
        if (footstepAudioSource == null || footstepClips == null || footstepClips.Length == 0) return;

        Vector3 currentPosition = guideCharacterInstance.transform.position;

        if (!footstepTrackerInitialized)
        {
            lastFootstepPosition = currentPosition;
            footstepTrackerInitialized = true;
            return;
        }

        // Standing still or waiting for the user: reset the tracker so no "catch-up" step fires on resume
        if (isWaitingForUser || agentEngine.velocity.magnitude < minSpeedToStep)
        {
            lastFootstepPosition = currentPosition;
            return;
        }

        float distanceCovered = Vector3.Distance(currentPosition, lastFootstepPosition);
        if (distanceCovered >= stepDistanceInterval)
        {
            AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];
            footstepAudioSource.pitch = Random.Range(pitchRandomRange.x, pitchRandomRange.y);
            footstepAudioSource.PlayOneShot(clip);
            lastFootstepPosition = currentPosition;
        }
    }

    private string[] historicalWalkChapters = new string[] {
        "CampOrigins", "ExchangeCampAnomaly", "JosefKramer", "CampPyres", 
        "DeathMarches", "RegistryDestruction", "PowCemetery", "CrematoriumDiaries", 
        "LiberationDrama", "BarracksBurning", "DisplacedPersons", "BelsenTrials",
        "Seybold_ExchangeCamp", "Seybold_MensCamp", "Seybold_WomensCamp", "Seybold_TentTheater", "Seybold_NamesReturned"
    };
    private int currentWalkChapterIndex = 0;
    /// <summary>
    /// Plays continuous historical audio clips whose duration strictly fits within the remaining
    /// walking time to the target stone, ensuring narration finishes 5s before arrival.
    /// </summary>
    private void HandleContinuousWalkingNarration()
    {
        if (!isTrackingMovement || isWaitingForUser || characterAudioSource == null || agentEngine == null) return;
        if (characterAudioSource.isPlaying) return;

        // Calculate remaining walking distance and estimated walking time (human walk speed: 1.2 m/s)
        float remainingDist = agentEngine.remainingDistance;
        if (remainingDist <= 0.1f && targetNodeObject != null)
        {
            remainingDist = Vector3.Distance(guideCharacterInstance.transform.position, targetNodeObject.transform.position);
        }

        float estimatedWalkingTimeSec = remainingDist / 1.2f; // seconds
        float safetyBuffer = 5.0f;                           // Finish speaking 5 seconds before arrival
        float maxAllowedAudioDuration = estimatedWalkingTimeSec - safetyBuffer;

        // If remaining walk time is less than 8 seconds, do not start new long narration to avoid getting cut off!
        if (maxAllowedAudioDuration < 8.0f) return;

        walkNarrationTimer += Time.deltaTime;
        if (walkNarrationTimer < 3.0f) return; // 3s pause between clips

        if (NarrationManager.Instance != null)
        {
            bool isGerman = (uiManager != null && uiManager.SelectedLanguage != null && uiManager.SelectedLanguage.ToLower() == "german");
            string langSuffix = isGerman ? "_DE" : "_EN";
            string selectedChapterId = null;

            // Search for a historical chapter that fits strictly within maxAllowedAudioDuration
            for (int k = 0; k < historicalWalkChapters.Length; k++)
            {
                string candidate = historicalWalkChapters[(currentWalkChapterIndex + k) % historicalWalkChapters.Length];
                string fullId = candidate.EndsWith("_EN") || candidate.EndsWith("_DE") ? candidate : candidate + langSuffix;

                AudioClip clip = Resources.Load<AudioClip>($"GuidanceAudio/{fullId}");
                if (clip != null)
                {
                    if (clip.length <= maxAllowedAudioDuration)
                    {
                        selectedChapterId = fullId;
                        currentWalkChapterIndex = (currentWalkChapterIndex + k + 1) % historicalWalkChapters.Length;
                        break;
                    }
                }
            }

            // Fallback: If no single long chapter fits, use a short guidance phrase
            if (selectedChapterId != null)
            {
                NarrationManager.Instance.PlayNarration(selectedChapterId, characterAudioSource);
                walkNarrationTimer = 0f;
                Debug.Log($"[PersonalGuidance Duration-Match] Distance: {remainingDist:F1}m (Est. {estimatedWalkingTimeSec:F0}s walk). Selected clip '{selectedChapterId}' ({characterAudioSource.clip.length:F1}s <= {maxAllowedAudioDuration:F1}s max).");
            }
        }
    }
    private const float baseWalkSpeed = 2.5f;
    private const float maximumGuideWalkSpeed = 8.0f;
    private const float desiredLeadDistance = 3.5f;
    [Header("Respectful Memorial Approach")]
    [Tooltip("Preferred lateral distance from the selected memorial while the guide speaks.")]
    [SerializeField] private float memorialStandOffDistance = 0.5f;
    [Tooltip("Minimum horizontal clearance from every other memorial anchor; matches the 0.5 m NavMesh exclusion diameter.")]
    [SerializeField] private float nearbyMemorialClearance = 0.25f;
    private readonly List<Transform> memorialAnchorCache = new List<Transform>();

    private void HandleFrontLeadingTethering()
    {
        if (!isTrackingMovement || userCameraTransform == null || targetNodeObject == null) return;

        // If detail panel is open on screen, avatar ALWAYS waits until user closes it!
        if (uiManager != null && uiManager.IsMemorialDetailOpen)
        {
            isWaitingForUser = true;
            if (agentEngine != null && agentEngine.enabled && agentEngine.isOnNavMesh) agentEngine.isStopped = true;
            if (characterAnimator != null) characterAnimator.SetFloat("Speed", 0f);
            return;
        }

        Vector3 userPositionForPacing = userMovementTransform != null ? userMovementTransform.position : userCameraTransform.position;
        Vector3 leadOffset = guideCharacterInstance.transform.position - userPositionForPacing;
        leadOffset.y = 0f;
        float currentDistanceToUser = leadOffset.magnitude;
        if (hasPreviousUserPosition)
        {
            float measuredSpeed = Vector3.Distance(userPositionForPacing, previousUserPosition) / Mathf.Max(Time.deltaTime, 0.001f);
            observedUserSpeed = Mathf.Lerp(observedUserSpeed, Mathf.Min(measuredSpeed, maximumGuideWalkSpeed), 0.7f);
        }
        previousUserPosition = userPositionForPacing;
        hasPreviousUserPosition = true;
        float matchingSpeed = observedUserSpeed;
        if (currentDistanceToUser < desiredLeadDistance)
            matchingSpeed += (desiredLeadDistance - currentDistanceToUser) * 1.5f;
        else
            matchingSpeed -= (currentDistanceToUser - desiredLeadDistance) * 0.6f;
        matchingSpeed = Mathf.Clamp(matchingSpeed, 0f, maximumGuideWalkSpeed);
        float maxFollowDistance = 6f;

        float guideRemainingDistance = agentEngine.remainingDistance;
        float userRemainingDistance = Vector3.Distance(userPositionForPacing, targetNodeObject.transform.position);
        bool guideIsAheadOfUser = guideRemainingDistance < userRemainingDistance;

        if (currentDistanceToUser >= maxFollowDistance && guideIsAheadOfUser)
        {
            isWaitingForUser = true;
            agentEngine.isStopped = true;
            agentEngine.speed = 0f;
            if (characterAnimator != null) characterAnimator.SetFloat("Speed", 0f);
        }
        else
        {
            bool shouldWaitAtLeadDistance = matchingSpeed < 0.1f;
            isWaitingForUser = shouldWaitAtLeadDistance;
            if (agentEngine.enabled && agentEngine.isOnNavMesh)
            {
                agentEngine.isStopped = shouldWaitAtLeadDistance;
                agentEngine.speed = matchingSpeed;
                if (characterAnimator != null)
                {
                    characterAnimator.SetFloat("Speed", shouldWaitAtLeadDistance ? 0f : Mathf.Clamp(matchingSpeed / baseWalkSpeed, 0.35f, 2.5f));
                    characterAnimator.speed = shouldWaitAtLeadDistance ? 1f : Mathf.Clamp(matchingSpeed / baseWalkSpeed, 0.8f, 2.5f);
                }
            }
        }

        // Face player horizontally smoothly when waiting or talking at a destination
        bool isStationary = !isTrackingMovement && guideCharacterInstance != null && userCameraTransform != null;
        if (isStationary || isWaitingForUser)
        {
            Vector3 lookDirection = (userCameraTransform.position - guideCharacterInstance.transform.position).normalized;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                guideCharacterInstance.transform.rotation = Quaternion.Slerp(
                    guideCharacterInstance.transform.rotation, 
                    Quaternion.LookRotation(lookDirection), 
                    Time.deltaTime * 6.0f
                );
            }
        }

        // Update animation: Speed parameter normalized to 0-1 based on actual velocity to prevent foot sliding
        if (characterAnimator != null)
        {
            float animSpeed = 0f;
            if (!isWaitingForUser && agentEngine.velocity.magnitude >= minSpeedToStep)
            {
                float speedRatio = Mathf.Clamp01(agentEngine.velocity.magnitude / 1.5f);
                animSpeed = Mathf.Lerp(0.3f, 1.0f, speedRatio);
            }
            characterAnimator.SetFloat("Speed", animSpeed);
        }

        // Target arrival verification sequence
        if (isTrackingMovement && !isWaitingForUser && !agentEngine.pathPending && agentEngine.remainingDistance <= agentEngine.stoppingDistance)
        {
            if (!agentEngine.hasPath || agentEngine.velocity.sqrMagnitude == 0f)
            {
                OnDestinationTargetReached();
            }
        }
    }

    private InteractiveMapPin[] cachedMapPins;
    private float pinCacheRefreshTimer = 0f;

    private void HandleLiveProximityDetourScanning()
    {
        if (userCameraTransform == null || memorialSpawner == null) return;

        detourScanTimer += Time.deltaTime;
        if (detourScanTimer < detourScanInterval) return;
        detourScanTimer = 0f;

        pinCacheRefreshTimer += detourScanInterval;
        if (cachedMapPins == null || pinCacheRefreshTimer >= 10.0f)
        {
            cachedMapPins = UnityEngine.Object.FindObjectsByType<InteractiveMapPin>(FindObjectsInactive.Include);
            pinCacheRefreshTimer = 0f;
        }

        // Scan cached layout pins to capture unsolicited approach indicators
        if (cachedMapPins == null) return;
        foreach (var pin in cachedMapPins)
        {
            if (pin == null) continue;
            Transform pointTr = pin.transform.name.StartsWith("point_") ? pin.transform : pin.transform.parent;
            if (pointTr == null) continue;

            string stoneID = pointTr.name.Replace("point_", "").Trim();
            if (string.Equals(stoneID, activeTargetID, System.StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(stoneID, previousTargetID, System.StringComparison.OrdinalIgnoreCase)) continue;

            float distanceToUser = Vector3.Distance(userCameraTransform.position, pointTr.position);

            // DETOUR INTERCEPTION CONSTRAINT: Trigger high-priority interrupt sequence if user deviates to explore an unscheduled node
            if (distanceToUser <= detourTriggerRadius)
            {
                Debug.Log($"[Detour Engine] Unscheduled exploration detected matching Stone ID: '{stoneID}'. Diverting companion avatar.");
                ExecuteDetourRerouteSequence(stoneID, pointTr.gameObject);
                break;
            }
        }
    }

    private void ExecuteDetourRerouteSequence(string newStoneID, GameObject newTargetNode)
    {
        activeTargetID = newStoneID;
        targetNodeObject = newTargetNode;
        isWaitingForUser = false;

        // Query the centralized DialogueManager to execute soft fadeouts and launch transition phrases
        if (DialogueManager.Instance != null)
        {
            DialogueSequence detourSequence = Resources.Load<DialogueSequence>($"Narrative/Detours/{newStoneID}");
            if (detourSequence != null)
            {
                DialogueManager.Instance.PlayNarrativeSequence(detourSequence);
            }
            else
            {
                // Dynamic fallback if custom standalone detour assets aren't pre-compiled on disk
                AudioClip clip = Resources.Load<AudioClip>($"GuidanceAudio/{newStoneID}");
                if (clip != null && characterAudioSource != null)
                {
                    characterAudioSource.Stop();
                    characterAudioSource.clip = clip;
                    characterAudioSource.Play();
                }
            }
        }

        // Route to the same respectful lateral position used for scheduled stops.
        if (agentEngine != null && agentEngine.isOnNavMesh)
        {
            ConfigureAgentNavigationMask();
            if (TryGetMemorialApproachPosition(out Vector3 approachPosition))
            {
                agentEngine.SetDestination(approachPosition);
                isTrackingMovement = true;
            }
            else
            {
                agentEngine.isStopped = true;
                isWaitingForUser = true;
                Debug.LogWarning($"[PersonalGuidance] Detour to '{newStoneID}' has no respectful NavMesh position.");
            }
        }
    }

    private void UpdateIKControllerTargets()
    {
        if (ikController == null) return;

        bool isCurrentlyTalking = false;
        if (characterAnimator != null)
        {
            // Read active layer indices to verify if speech animations are driving the skeletal layers
            isCurrentlyTalking = characterAnimator.GetBool("IsTalking");
        }

        // The arm is reserved for an on-site explanation, never for background narration while walking.
        bool shouldPointAtMemorial = isCurrentlyMarkedTalking && !isTrackingMovement && !isWaitingForUser;
        ikController.SetTrackingTargets(userCameraTransform, targetNodeObject != null ? targetNodeObject.transform : null, shouldPointAtMemorial);
    }

    private bool TryGetMemorialApproachPosition(out Vector3 approachPosition)
    {
        approachPosition = Vector3.zero;
        if (targetNodeObject == null) return false;

        Vector3 origin = userMovementTransform != null
            ? userMovementTransform.position
            : userCameraTransform != null
                ? userCameraTransform.position
            : (guideCharacterInstance != null ? guideCharacterInstance.transform.position : transform.position);
        return TryFindRespectfulApproachPosition(targetNodeObject.transform, origin, out approachPosition);
    }

    /// <summary>Uses the same non-mutating placement rule for editor safety audits.</summary>
    public bool TryFindRespectfulApproachPosition(Transform memorialAnchor, Vector3 visitorPosition, out Vector3 approachPosition)
    {
        approachPosition = Vector3.zero;
        return TryFindRespectfulApproachPosition(memorialAnchor, visitorPosition, nearbyMemorialClearance, out approachPosition);
    }

    /// <summary>Allows editor audits to compare clearance thresholds without changing scene data.</summary>
    public bool TryFindRespectfulApproachPosition(Transform memorialAnchor, Vector3 visitorPosition, float clearance, out Vector3 approachPosition)
    {
        approachPosition = Vector3.zero;
        if (memorialAnchor == null) return false;

        Vector3 targetPosition = memorialAnchor.position;
        Vector3 origin = visitorPosition;
        Vector3 userToMemorial = targetPosition - origin;
        userToMemorial.y = 0f;
        if (userToMemorial.sqrMagnitude < 0.01f) userToMemorial = Vector3.forward;
        userToMemorial.Normalize();

        // Place the guide to the visitor's right and just beyond the memorial,
        // preserving a clear triangular sightline rather than blocking the stone.
        Vector3 side = new Vector3(-userToMemorial.z, 0f, userToMemorial.x);
        float sideDistance = 1.0f;
        Vector3 behindOffset = userToMemorial * 0.5f;
        if (TrySampleRespectfulPosition(targetPosition + side * sideDistance + behindOffset, memorialAnchor, clearance, out approachPosition)) return true;
        if (TrySampleRespectfulPosition(targetPosition + side * sideDistance, memorialAnchor, clearance, out approachPosition)) return true;
        if (TrySampleRespectfulPosition(targetPosition - side * sideDistance + behindOffset, memorialAnchor, clearance, out approachPosition)) return true;

        // Dense groups can leave no navigable space beside a small stone. Keep
        // the same lateral geometry, but allow the guide to use the outer path.
        float outerStandOffDistance = sideDistance + 1.0f;
        if (TrySampleRespectfulPosition(targetPosition + side * outerStandOffDistance, memorialAnchor, clearance, out approachPosition)) return true;
        if (TrySampleRespectfulPosition(targetPosition - side * outerStandOffDistance, memorialAnchor, clearance, out approachPosition)) return true;

        // In tight clusters, remain behind the memorial relative to the visitor,
        // but never fall back to the memorial's own anchor.
        return TrySampleRespectfulPosition(targetPosition + userToMemorial, memorialAnchor, clearance, out approachPosition);
    }

    private bool TrySampleRespectfulPosition(Vector3 desiredPosition, Transform selectedMemorial, float clearance, out Vector3 sampledPosition)
    {
        sampledPosition = Vector3.zero;
        if (!NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit, 1.5f, NavigationAreaMask.VisitorWalkable)) return false;
        if (Vector3.Distance(hit.position, desiredPosition) > 1.5f) return false;
        if (!HasMemorialClearance(hit.position, selectedMemorial, clearance)) return false;

        sampledPosition = hit.position;
        return true;
    }

    private bool HasMemorialClearance(Vector3 candidatePosition, Transform selectedMemorial, float clearance)
    {
        if (memorialAnchorCache.Count == 0) CacheMemorialAnchors();

        foreach (Transform anchor in memorialAnchorCache)
        {
            if (anchor == null) continue;

            Vector3 offset = candidatePosition - anchor.position;
            offset.y = 0f;
            float requiredClearance = anchor == selectedMemorial ? Mathf.Max(clearance, 0.9f) : clearance;
            if (offset.sqrMagnitude < requiredClearance * requiredClearance) return false;
        }
        return true;
    }

    private void CacheMemorialAnchors()
    {
        memorialAnchorCache.Clear();
        Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        foreach (Transform candidate in transforms)
        {
            if (candidate != null && candidate.name.StartsWith("point_", System.StringComparison.OrdinalIgnoreCase))
                memorialAnchorCache.Add(candidate);
        }
    }

    public override void OnMemorialSelected(string memorialID)
    {
        hasArrivedAtActiveTarget = false;
        ActiveGuideAvatarRegistry.ClaimOwnership(this);
        UIManager.DespawnAllGuidanceAvatarsExcept(this);

        bool isWelcome = string.IsNullOrEmpty(memorialID) || memorialID.StartsWith("WELCOME", System.StringComparison.OrdinalIgnoreCase);
        if (isWelcome)
        {
            memorialID = "WELCOME_PERSONAL";
        }

        if (thesisManager != null)
            thesisManager.LogEvent("memorial_selected", memorialID, "personal");

        if (memorialSpawner == null) memorialSpawner = UnityEngine.Object.FindAnyObjectByType<MemorialSpawner>(FindObjectsInactive.Include);
        if (memorialSpawner == null || guideCharacterPrefab == null)
        {
            Debug.LogError($"[PersonalGuidance] Cannot select memorial '{memorialID}': spawner={memorialSpawner != null}, prefab={guideCharacterPrefab != null}");
            return;
        }

        bool isSystemicAudio = memorialID.StartsWith("WELCOME") || memorialID.StartsWith("GOODBYE") || memorialID.StartsWith("LOGISTICS");
        targetNodeObject = null;
        pendingDestinationSet = false;

        if (!isSystemicAudio)
        {
            targetNodeObject = memorialSpawner.GetSpawnedMemorial(memorialID);
            if (targetNodeObject == null)
            {
                Debug.LogWarning($"[PersonalGuidance] GetSpawnedMemorial returned NULL for '{memorialID}'. Avatar will spawn but cannot navigate to target.");
            }
            else
            {
                Debug.Log($"[PersonalGuidance] Target resolved: '{memorialID}' -> {targetNodeObject.name} at {targetNodeObject.transform.position}");
            }
        }

        if (!string.IsNullOrEmpty(activeTargetID) && !string.Equals(activeTargetID, memorialID, System.StringComparison.OrdinalIgnoreCase))
        {
            previousTargetID = activeTargetID;
        }

        activeTargetID = memorialID;
        isWaitingForUser = false;

        // Force Warp to NavMesh if the existing agent has lost its NavMesh alignment
        if (guideCharacterInstance != null && agentEngine != null)
        {
            if (!agentEngine.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(guideCharacterInstance.transform.position, out NavMeshHit wideHit, 15.0f, NavigationAreaMask.VisitorWalkable))
                {
                    agentEngine.Warp(wideHit.position);
                    Debug.Log($"[PersonalGuidance] Warped existing agent to nearest NavMesh at {wideHit.position}");
                }
            }
        }

        if (guideCharacterInstance == null)
        {
            guideCharacterInstance = ActiveGuideAvatarRegistry.SingleAvatarInstance ?? GameObject.Find("SingleGuideAvatarInstance") ?? GameObject.Find("GuideCharacterInstance") ?? GameObject.Find("GuideCharacterPrefab(Clone)");
        }

        if (guideCharacterInstance != null)
        {
            ActiveGuideAvatarRegistry.RegisterExistingAvatar(guideCharacterInstance);
            ActiveGuideAvatarRegistry.ApplyPersonalPBRVisuals();
            ActiveGuideAvatarRegistry.AssignComponents(ref characterAnimator, ref characterAudioSource, ref agentEngine);
        }

        // --- SPATIAL INITIALIZATION ---
        if (guideCharacterInstance == null)
        {
            guideCharacterInstance = (ThesisManager.Instance != null ? ThesisManager.Instance.GuideAvatarInstance : null)
                ?? ActiveGuideAvatarRegistry.SingleAvatarInstance;
        }

        Vector3 spawnPosition = transform.position + characterSpawnOffset;
        Quaternion spawnRotation = Quaternion.identity;

        if (userCameraTransform != null)
        {
            Vector3 cameraForwardHorizontal = userCameraTransform.forward;
            cameraForwardHorizontal.y = 0f;
            cameraForwardHorizontal.Normalize();

            float groundApproximationY = userCameraTransform.position.y - 1.6f;
            spawnPosition = userCameraTransform.position + (cameraForwardHorizontal * characterSpawnOffset.z);
            spawnPosition.y = groundApproximationY;

            if (targetNodeObject != null)
            {
                Vector3 toTarget = (targetNodeObject.transform.position - spawnPosition);
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f)
                    spawnRotation = Quaternion.LookRotation(toTarget.normalized);
            }
            else
            {
                Vector3 lookAtUserDirection = (userCameraTransform.position - spawnPosition).normalized;
                lookAtUserDirection.y = 0f;
                if (lookAtUserDirection.sqrMagnitude > 0.01f)
                    spawnRotation = Quaternion.LookRotation(lookAtUserDirection);
            }
        }

        if (NavMesh.SamplePosition(spawnPosition, out NavMeshHit hit, 10.0f, NavigationAreaMask.VisitorWalkable))
        {
            spawnPosition = hit.position;
        }

        if (guideCharacterInstance == null && guideCharacterPrefab != null)
        {
            // Instantiate in World Space (parent: null) to completely prevent local coordinates drift or user camera parent drag
            guideCharacterInstance = Instantiate(guideCharacterPrefab, spawnPosition, spawnRotation, null);
            guideCharacterInstance.name = "SingleGuideAvatarInstance";
        }
        else if (guideCharacterInstance != null)
        {
            guideCharacterInstance.transform.position = spawnPosition;
            guideCharacterInstance.transform.rotation = spawnRotation;
        }

        if (guideCharacterInstance != null)
        {
            ActiveGuideAvatarRegistry.RegisterSingleAvatarInstance(guideCharacterInstance);
            ActiveGuideAvatarRegistry.ApplyPersonalPBRVisuals();

            ikController = guideCharacterInstance.GetComponent<CompanionIKController>();
            if (ikController == null) ikController = guideCharacterInstance.AddComponent<CompanionIKController>();

            agentEngine = guideCharacterInstance.GetComponent<NavMeshAgent>();
            characterAnimator = guideCharacterInstance.GetComponentInChildren<Animator>();
            if (characterAnimator != null)
            {
                characterAnimator.applyRootMotion = false;
                characterAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (characterAnimator.layerCount > 1) characterAnimator.SetLayerWeight(1, 1.0f);
            }
            characterAudioSource = guideCharacterInstance.GetComponentInChildren<AudioSource>();

            if (agentEngine == null) agentEngine = guideCharacterInstance.AddComponent<NavMeshAgent>();
            if (characterAudioSource == null) characterAudioSource = guideCharacterInstance.AddComponent<AudioSource>();

            if (agentEngine.isOnNavMesh) agentEngine.Warp(spawnPosition);

            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.SetCompanionReferences(characterAnimator, characterAudioSource);
            }
        }

        if (guideCharacterInstance != null && agentEngine != null)
        {
            ConfigureAgentNavigationMask();
            Vector3 currentPos = guideCharacterInstance.transform.position;
            if (!agentEngine.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(currentPos, out NavMeshHit wideHit, 50.0f, NavigationAreaMask.VisitorWalkable))
                {
                    agentEngine.Warp(wideHit.position);
                    Debug.Log($"[PersonalGuidance] Warped to nearest NavMesh at {wideHit.position}");
                }
                else
                {
                    Debug.LogError($"[PersonalGuidance] CRITICAL: No NavMesh within 50m of {currentPos}!");
                }
            }

            agentEngine.speed = baseWalkSpeed;
            agentEngine.angularSpeed = 260f;
            agentEngine.acceleration = 12f;
            agentEngine.stoppingDistance = 1.5f;
        }

        if (characterAudioSource != null)
        {
            characterAudioSource.spatialBlend = 0f;
            characterAudioSource.playOnAwake = false;
        }

        if (guideCharacterInstance != null)
        {
            guideCharacterInstance.SetActive(true);
        }

        if (agentEngine != null && agentEngine.isOnNavMesh)
        {
            agentEngine.isStopped = false;
        }
        
        // Stop any active narration when commencing a new journey
        if (NarrationManager.Instance != null)
        {
            NarrationManager.Instance.StopCurrentNarration();
        }

        // Play systemic audio instantly on start (e.g. welcome), as the avatar does not navigate for these
        if (isSystemicAudio && NarrationManager.Instance != null)
        {
            NarrationManager.Instance.PlayNarration(memorialID, characterAudioSource);
        }

        // --- PATHFINDING INITIALIZATION ---
        if (!isSystemicAudio && targetNodeObject != null && agentEngine != null)
        {
            if (agentEngine.isOnNavMesh)
            {
                ConfigureAgentNavigationMask();
                if (!TryGetMemorialApproachPosition(out Vector3 approachPosition))
                {
                    isWaitingForUser = true;
                    agentEngine.isStopped = true;
                    Debug.LogWarning($"[PersonalGuidance] No respectful NavMesh position is available for '{memorialID}'.");
                    return;
                }

                agentEngine.SetDestination(approachPosition);
                agentEngine.isStopped = false;
                isTrackingMovement = true;
                if (characterAnimator != null) characterAnimator.SetFloat("Speed", 1f);
                Debug.Log($"[PersonalGuidance] Navigation started to {targetNodeObject.name}, distance={Vector3.Distance(guideCharacterInstance.transform.position, targetNodeObject.transform.position):F1}m");
            }
            else
            {
                // Agent not on NavMesh yet — defer to Update
                pendingDestinationSet = true;
                isTrackingMovement = false;
                Debug.LogWarning($"[PersonalGuidance] Agent not on NavMesh yet, deferring destination to Update.");
            }
        }
        else
        {
            isTrackingMovement = false;
            if (!isSystemicAudio)
                Debug.LogWarning($"[PersonalGuidance] Pathfinding NOT started for '{memorialID}': targetNode={targetNodeObject != null}, agent={agentEngine != null}");
        }
    }

    public override void OnMemorialDeselected()
    {
        isTrackingMovement = false;
        if (agentEngine != null && agentEngine.isOnNavMesh) agentEngine.ResetPath();
        ActiveGuideAvatarRegistry.ReleaseOwnership(this);
    }

    /// <summary>Resumes the current tour destination after the visitor has summoned the guide.</summary>
    public void ResumeCurrentTarget()
    {
        if (string.IsNullOrEmpty(activeTargetID) || targetNodeObject == null || agentEngine == null || !agentEngine.isOnNavMesh)
            return;

        if (!TryGetMemorialApproachPosition(out Vector3 approachPosition))
            return;

        agentEngine.SetDestination(approachPosition);
        agentEngine.isStopped = false;
        isWaitingForUser = false;
        isTrackingMovement = true;
        hasArrivedAtActiveTarget = false;
        SetTalkingState(false);
        if (characterAnimator != null) characterAnimator.SetFloat("Speed", 1f);
    }

    public override void OnMemorialReached(string memorialID)
    {
        // ARWayfinding reports the visitor's proximity. In Personal mode, narration must wait
        // for the physical guide to have reached its respectful stand-off position.
        if (isTrackingMovement || !string.Equals(memorialID, activeTargetID, System.StringComparison.OrdinalIgnoreCase)) return;
        if (hasArrivedAtActiveTarget) return;

        hasArrivedAtActiveTarget = true;
        if (thesisManager != null)
            thesisManager.LogEvent("memorial_reached", memorialID, "personal");

        // Delegate spatial 3D audio playback and subtitles completely to the centralized NarrationManager
        if (NarrationManager.Instance != null)
        {
            NarrationManager.Instance.PlayNarration(memorialID, characterAudioSource);
            SetTalkingState(true);
        }
    }

#if UNITY_EDITOR
    /// <summary>Runs the normal arrival-and-narration sequence after the editor-only T shortcut.</summary>
    public void ForceArrivalForEditorTesting(string memorialID)
    {
        if (string.IsNullOrEmpty(memorialID)) return;

        activeTargetID = memorialID;
        isTrackingMovement = false;
        hasArrivedAtActiveTarget = false;
        OnMemorialReached(memorialID);
    }
#endif

    private void OnDestinationTargetReached()
    {
        isTrackingMovement = false;
        if (characterAnimator != null) characterAnimator.speed = 1f;

        if (guideCharacterInstance != null && targetNodeObject != null)
        {
            // Keep the memorial on the guide's right side so the existing right-arm
            // pointing pose reads naturally instead of indicating behind their back.
            Vector3 guideToMemorial = targetNodeObject.transform.position - guideCharacterInstance.transform.position;
            guideToMemorial.y = 0f;
            if (guideToMemorial.sqrMagnitude > 0.001f)
            {
                guideCharacterInstance.transform.rotation = Quaternion.LookRotation(Vector3.Cross(guideToMemorial.normalized, Vector3.up));
            }
        }

        if (characterAnimator != null)
        {
            characterAnimator.SetFloat("Speed", 0f);
        }
        if (!hasArrivedAtActiveTarget && !string.IsNullOrEmpty(activeTargetID) && NarrationManager.Instance != null)
        {
            hasArrivedAtActiveTarget = true;
            NarrationManager.Instance.PlayNarration(activeTargetID, characterAudioSource);
            SetTalkingState(true);
        }
        else
        {
            SetTalkingState(false);
        }
    }

    private void HandleNarrationFinished()
    {
        if (this == null || !gameObject.activeInHierarchy || !enabled || guideCharacterInstance == null) return;
        if (NarrationManager.Instance == null || NarrationManager.Instance.CurrentAudioSource != characterAudioSource) return;
        if (characterAudioSource != null && characterAudioSource.isPlaying)
        {
            characterAudioSource.Stop();
        }
        SetTalkingState(false);
        Debug.Log($"[PersonalGuidance] Narration finished event received. Switching animator '{guideCharacterInstance.name}' to Idle.");

        if (!string.IsNullOrEmpty(activeTargetID) && activeTargetID.StartsWith("WELCOME", System.StringComparison.OrdinalIgnoreCase))
        {
            var tourMgr = UnityEngine.Object.FindAnyObjectByType<TourManager>(FindObjectsInactive.Include);
            if (tourMgr != null && tourMgr.IsTourActiveAndRunning)
            {
                string tourStoneID = tourMgr.GetCurrentTargetStoneID();
                if (!string.IsNullOrEmpty(tourStoneID))
                {
                    OnMemorialSelected(tourStoneID);
                    return;
                }
            }

            if (uiManager != null)
            {
                bool isGerman = uiManager.SelectedLanguage == "german";
                string title = isGerman ? "Persönlicher Begleiter" : "Personal guide";
                string msg = isGerman ? "Wähle einen Gedenkstein oder einen Rundgang, um zu starten." : "Select a memorial stone or tour to begin walking.";
                uiManager.ShowNotificationToast(title, msg);
            }
        }
        else if (hasArrivedAtActiveTarget)
        {
            var tourMgr = UnityEngine.Object.FindAnyObjectByType<TourManager>(FindObjectsInactive.Include);
            if (tourMgr != null && tourMgr.IsTourActiveAndRunning)
            {
                tourMgr.WaitForVisitorToContinue();
            }
        }
    }

    private void LogAnimatorTalkingParameterDiagnostics()
    {
        if (characterAnimator == null || characterAnimator.runtimeAnimatorController == null)
        {
            Debug.LogError("[PersonalGuidance] DIAGNOSTIC: characterAnimator or its Controller is null - no Animator Controller assigned!");
            return;
        }

        var parameters = characterAnimator.parameters;
        Debug.Log($"[PersonalGuidance] DIAGNOSTIC: Animator Controller '{characterAnimator.runtimeAnimatorController.name}' has {parameters.Length} parameters total:");

        bool foundAnyTalkLikeParam = false;
        foreach (var p in parameters)
        {
            string nameLower = p.name.ToLowerInvariant();
            bool looksRelevant = nameLower.Contains("talk") || nameLower.Contains("speak");
            Debug.Log($"[PersonalGuidance] DIAGNOSTIC:   - '{p.name}' (type: {p.type}){(looksRelevant ? "  <-- RELEVANT" : "")}");
            if (looksRelevant) foundAnyTalkLikeParam = true;
        }

        if (!foundAnyTalkLikeParam)
        {
            Debug.LogError("[PersonalGuidance] DIAGNOSTIC: NO parameter with 'talk' or 'speak' in name found! SetBool calls have NO EFFECT on this Controller. Check parameter name in Unity Editor.");
        }

        Debug.Log($"[PersonalGuidance] DIAGNOSTIC: Controller has {characterAnimator.layerCount} layers total:");
        for (int i = 0; i < characterAnimator.layerCount; i++)
        {
            Debug.Log($"[PersonalGuidance] DIAGNOSTIC:   Layer {i}: '{characterAnimator.GetLayerName(i)}'");
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (NarrationManager.Instance != null)
        {
            NarrationManager.Instance.OnNarrationFinished -= HandleNarrationFinished;
        }
        // Do NOT destroy the persistent avatar here. The registry owns the single persistent instance.
        ActiveGuideAvatarRegistry.ReleaseOwnership(this);
    }
}



