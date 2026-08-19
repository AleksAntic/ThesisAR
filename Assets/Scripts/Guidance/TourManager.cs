using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Manages the official academic tours execution pipeline (Minimal, Intermediate, Complete).
/// Controls runtime waypoint progression, populates dropdown interfaces, and supports 
/// advanced Editor-Mode Pre-Baking leveraging real NavMesh polygonal topology constraints.
/// All internal code, variables, and logs are strictly maintained in English.
/// </summary>
public class TourManager : MonoBehaviour
{
    [System.Serializable]
    public class PresetTour
    {
        public string tourName; // Named "Minimal", "Intermediate", or "Complete"
        [Tooltip("If true, this tour will automatically include ALL valid discovered stones.")]
        public bool isCompleteTour;
        [Tooltip("Number of target stones this tour should discover (ignored if Is Complete Tour is true).")]
        public int desiredStopsCount;
        public List<string> orderedStoneIDs = new List<string>();
        [Header("Editor Visualization")]
        public Color tourColor = Color.green;
    }

    [Header("📋 Official Tour Structures")]
    [SerializeField]
    private List<PresetTour> officialTours = new List<PresetTour>
    {
        new PresetTour { tourName = "Minimal", isCompleteTour = false, desiredStopsCount = 3, tourColor = Color.green },
        new PresetTour { tourName = "Intermediate", isCompleteTour = false, desiredStopsCount = 6, tourColor = Color.yellow },
        new PresetTour { tourName = "Complete", isCompleteTour = true, desiredStopsCount = 0, tourColor = Color.cyan }
    };

    [Header("🔍 Filtering Rules (Editor Pre-Bake)")]
    [Tooltip("Any GameObject starting with 'point_' that contains these keywords will be ignored during pre-bake.")]
    [SerializeField] private List<string> excludedKeywords = new List<string> { "origin", "anchor", "test", "setup", "point_0", "point_1", "point_2", "point_3" };

    [Header("🕹️ Starting Position Reference")]
    [Tooltip("The system will use this object (e.g., Parking Lot) as the absolute starting pivot for path optimization.")]
    [SerializeField] private Transform simulatedGpsPlayer;

    [Header("⚙️ External Core Managers Injection")]
    [SerializeField] private RouteManager routeManager;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private ARWayfindingManager wayfindingManager;
    [SerializeField] private TMP_Dropdown tourDropdown;
    [SerializeField] private TextMeshProUGUI tourProgressUiText;
    [SerializeField] private Button continueTourButton;

    [Header("👁️ Separate Visual Toggles (Editor Gizmos)")]
    [SerializeField] private bool showMinimalTour = true;
    [SerializeField] private bool showIntermediateTour = true;
    [SerializeField] private bool showCompleteTour = true;

    private int activeTourIndex = -1;
    private int currentWaypointIndex = -1;
    private bool isTourActiveAndRunning = false;
    private bool resumeCurrentStopRequested = false;
    private List<string> ActiveStops => routeManager != null && routeManager.GetSelectedStoneIDs().Count > 0
        ? routeManager.GetSelectedStoneIDs()
        : (activeTourIndex >= 0 && activeTourIndex < officialTours.Count ? officialTours[activeTourIndex].orderedStoneIDs : null);
    public bool IsTourActiveAndRunning => isTourActiveAndRunning;
    public bool HasSelectedTour => activeTourIndex >= 0;

    void Start()
    {
        if (routeManager == null) routeManager = UnityEngine.Object.FindAnyObjectByType<RouteManager>(FindObjectsInactive.Include);
        if (uiManager == null) uiManager = UnityEngine.Object.FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
        if (wayfindingManager == null) wayfindingManager = UnityEngine.Object.FindAnyObjectByType<ARWayfindingManager>(FindObjectsInactive.Include);

        if (simulatedGpsPlayer == null)
        {
            GameObject foundPlayer = GameObject.Find("Simulated_GPS_Player");
            if (foundPlayer != null) simulatedGpsPlayer = foundPlayer.transform;
        }

        PopulateTourDropdown();
        ResolveContinueTourButton();
        ResetActiveTourState();
    }

    private void PopulateTourDropdown()
    {
        if (tourDropdown == null) return;

        tourDropdown.ClearOptions();
        List<string> options = new List<string> { "Select a Tour..." };

        foreach (PresetTour tour in officialTours)
        {
            options.Add($"{tour.tourName} ({tour.orderedStoneIDs.Count} Stops)");
        }

        tourDropdown.AddOptions(options);

        tourDropdown.onValueChanged.RemoveAllListeners();
        tourDropdown.onValueChanged.AddListener(OnDropdownValueChanged);
    }

    private void OnDropdownValueChanged(int index)
    {
        if (index == 0)
        {
            ResetActiveTourState();
        }
        else
        {
            StartOfficialTour(index - 1);
        }
    }

    public void StartOfficialTour(int tourIndex)
    {
        if (tourIndex < 0 || tourIndex >= officialTours.Count) return;

        if (routeManager != null)
        {
            routeManager.ClearAndResetRoute();
        }

        activeTourIndex = tourIndex;
        currentWaypointIndex = 0;
        isTourActiveAndRunning = false;
        PresetTour selectedTour = officialTours[activeTourIndex];

        if (routeManager != null)
        {
            routeManager.ToggleRoutePlanningMode(true);
            routeManager.ImpostaPercorsoPredefinitoDaID(selectedTour.orderedStoneIDs);
            routeManager.SetRoutePlanningModeWithoutClearing(false);
        }

        if (uiManager != null)
        {
            uiManager.RefreshRouteButtonLabel(true);
            uiManager.RefreshSearchResultsForRouteChange();
        }
        StartCoroutine(RefreshTourRouteNextFrame());

        UpdateTourProgressFeedback();
        // FocusOnCurrentTourStop(); <-- Deferred until Guide Me button click to prevent companion auto-start

        Debug.Log($"[Tour Manager] Tour '{selectedTour.tourName}' initialized. Waypoints displayed, awaiting user manual guide trigger.");
    }

    private System.Collections.IEnumerator RefreshTourRouteNextFrame()
    {
        yield return null;
        if (routeManager != null) routeManager.TriggerRouteUpdate();
    }

    public void AdvanceToNextStop()
    {
        resumeCurrentStopRequested = false;
        SetContinueButtonVisible(false);
        if (activeTourIndex == -1) return;
        PresetTour activeTour = officialTours[activeTourIndex];

        List<string> stops = ActiveStops;
        if (stops == null) return;
        if (currentWaypointIndex < stops.Count - 1)
        {
            currentWaypointIndex++;
            UpdateTourProgressFeedback();
            FocusOnCurrentTourStop();
        }
        else
        {
            Debug.Log($"[Tour Manager] Reached final destination stop for tour '{activeTour.tourName}'. Auto-cleaning configurations.");
            ResetActiveTourState();
        }
    }

    private void FocusOnCurrentTourStop()
    {
        if (activeTourIndex == -1 || currentWaypointIndex == -1) return;
        List<string> stops = ActiveStops;
        if (stops == null || stops.Count == 0) return;

        string activeStoneID = stops[currentWaypointIndex];

        // UX Feedback: In Condition C, the physical avatar guides the user without auto-opening modal details.

        GuidanceSystemBase guidance = ThesisManager.Instance != null ? ThesisManager.Instance.CurrentGuidanceSystem : UnityEngine.Object.FindAnyObjectByType<GuidanceSystemBase>(FindObjectsInactive.Include);
        if (guidance != null)
        {
            guidance.OnMemorialSelected(activeStoneID);
        }

        // CRITICAL: without this, ground arrows never redraw for the new stop
        if (wayfindingManager == null) wayfindingManager = UnityEngine.Object.FindAnyObjectByType<ARWayfindingManager>(FindObjectsInactive.Include);
        if (wayfindingManager != null)
        {
            wayfindingManager.NavigateTo(activeStoneID);
        }
        else
        {
            Debug.LogWarning("[Tour Manager] ARWayfindingManager reference is missing — ground arrows and arrival detection will not update for this stop.");
        }
    }

    private void UpdateTourProgressFeedback()
    {
        if (activeTourIndex == -1 || tourProgressUiText == null) return;
        PresetTour activeTour = officialTours[activeTourIndex];
        List<string> stops = ActiveStops;
        tourProgressUiText.text = $"<b>{activeTour.tourName}</b>: Stop <b>{currentWaypointIndex + 1}</b> of {stops?.Count ?? 0}";
    }

    public void ResetActiveTourState()
    {
        resumeCurrentStopRequested = false;
        SetContinueButtonVisible(false);
        activeTourIndex = -1;
        currentWaypointIndex = -1;
        isTourActiveAndRunning = false;

        if (tourProgressUiText != null)
            tourProgressUiText.text = "No active tour selected.";

        if (routeManager != null)
        {
            routeManager.ToggleRoutePlanningMode(false);
            routeManager.ClearAndResetRoute();
        }

        if (tourDropdown != null && tourDropdown.value != 0)
        {
            tourDropdown.SetValueWithoutNotify(0);
        }

        if (uiManager != null)
        {
            uiManager.RefreshRouteButtonLabel(false);
            uiManager.RefreshSearchResultsForRouteChange();
        }
    }

    public List<string> GetActiveTourOrderedStoneIDs()
    {
        return ActiveStops;
    }

    /// <summary>
    /// Returns the stone ID the tour currently expects the user to be walking towards, or null
    /// if no tour is active. Used by ThesisManager.HandleMemorialReached to confirm that an
    /// arrival event actually matches the CURRENTLY scheduled stop before advancing the tour —
    /// otherwise a coincidental proximity to some other stone could advance the wrong step.
    /// </summary>
    public string GetCurrentTargetStoneID()
    {
        if (activeTourIndex < 0 || activeTourIndex >= officialTours.Count) return null;
        List<string> stops = ActiveStops;
        if (stops == null || currentWaypointIndex < 0 || currentWaypointIndex >= stops.Count) return null;
        return stops[currentWaypointIndex];
    }

    public void StartTourFromGuideMeButton()
    {
        if (activeTourIndex == -1) return;
        if (isTourActiveAndRunning) return;

        isTourActiveAndRunning = true;
        FocusOnCurrentTourStop();
        Debug.Log($"[Tour Manager] Tour sequence guide companion activated for stop index: {currentWaypointIndex}");
    }

    /// <summary>Called after an on-site narration; the visitor alone starts the next stop.</summary>
    public void WaitForVisitorToContinue()
    {
        if (!isTourActiveAndRunning) return;

        resumeCurrentStopRequested = false;

        bool isGerman = uiManager != null && uiManager.SelectedLanguage == "german";
        if (uiManager != null)
            uiManager.DisplayGuideSubtitle(isGerman
                ? "Wenn Sie bereit sind, setzen Sie den Rundgang fort."
                : "When you are ready, continue the tour.");

        SetContinueButtonVisible(true);
    }

    public void WaitForVisitorToResume()
    {
        if (!isTourActiveAndRunning) return;

        resumeCurrentStopRequested = true;
        bool isGerman = uiManager != null && uiManager.SelectedLanguage == "german";
        if (uiManager != null)
            uiManager.DisplayGuideSubtitle(isGerman
                ? "Wenn Sie bereit sind, setzen Sie die Begleitung fort."
                : "When you are ready, resume the guide.");

        SetContinueButtonVisible(true);
    }

    public void ContinueTour()
    {
        if (!isTourActiveAndRunning) return;

        if (resumeCurrentStopRequested)
        {
            resumeCurrentStopRequested = false;
            SetContinueButtonVisible(false);
            PersonalGuidance personalGuide = PersonalGuidance.Instance ?? UnityEngine.Object.FindAnyObjectByType<PersonalGuidance>(FindObjectsInactive.Include);
            if (personalGuide != null) personalGuide.ResumeCurrentTarget();
            return;
        }

        AdvanceToNextStop();
    }

    private void ResolveContinueTourButton()
    {
        if (continueTourButton == null)
        {
            Button[] sceneButtons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include);
            foreach (Button sceneButton in sceneButtons)
            {
                if (sceneButton != null && sceneButton.name == "Btn_Continue_Tour")
                {
                    continueTourButton = sceneButton;
                    break;
                }
            }
        }

        if (continueTourButton == null)
        {
            Button template = null;
            Transform hub = null;
            foreach (Button sceneButton in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include))
            {
                if (sceneButton != null && (sceneButton.name == "Btn_GuideMe" || sceneButton.name == "Btn_Guide_Me"))
                {
                    template = sceneButton;
                    break;
                }
            }
            foreach (Transform candidate in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (candidate != null && candidate.name == "AR_Exploration_Hub")
                {
                    hub = candidate;
                    break;
                }
            }
            if (template != null && hub != null)
            {
                GameObject buttonObject = UnityEngine.Object.Instantiate(template.gameObject, hub);
                buttonObject.name = "Btn_Continue_Tour";
                RectTransform rect = buttonObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, 116f);
                continueTourButton = buttonObject.GetComponent<Button>();
                continueTourButton.onClick = new Button.ButtonClickedEvent();
                buttonObject.SetActive(false);
            }
        }

        if (continueTourButton == null)
        {
            Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
            {
                Debug.LogError("[Tour Manager] Cannot create Continue Tour button: no Canvas was found.");
                return;
            }

            GameObject buttonObject = new GameObject("Btn_Continue_Tour", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(canvas.transform, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 116f);
            rect.sizeDelta = new Vector2(220f, 52f);

            Image background = buttonObject.GetComponent<Image>();
            background.color = new Color(0.12f, 0.22f, 0.29f, 0.96f);
            continueTourButton = buttonObject.GetComponent<Button>();

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(buttonObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            Text label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 18;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            Debug.Log($"[Tour Manager] Created runtime fallback '{buttonObject.name}' under Canvas '{canvas.name}'.");
        }

        continueTourButton.onClick.RemoveListener(ContinueTour);
        continueTourButton.onClick.AddListener(ContinueTour);
        continueTourButton.gameObject.SetActive(false);
    }

    private void SetContinueButtonVisible(bool visible)
    {
        if (continueTourButton == null) ResolveContinueTourButton();
        if (continueTourButton == null)
        {
            Debug.LogError("[Tour Manager] Continue Tour button is unavailable.");
            return;
        }
        bool isGerman = uiManager != null && uiManager.SelectedLanguage == "german";
        string buttonLabel = resumeCurrentStopRequested
            ? (isGerman ? "Begleitung fortsetzen" : "Resume guide")
            : (isGerman ? "Rundgang fortsetzen" : "Continue tour");
        Text label = continueTourButton.GetComponentInChildren<Text>(true);
        if (label != null) label.text = buttonLabel;
        TextMeshProUGUI tmpLabel = continueTourButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmpLabel != null) tmpLabel.text = buttonLabel;
        continueTourButton.gameObject.SetActive(visible);
    }

#if UNITY_EDITOR
    private Dictionary<Vector3, Dictionary<Vector3, float>> preBakeDistanceCache;

    [InitializeOnLoadMethod]
    private static void RegisterEditorContinueButtonCreation()
    {
        EditorApplication.playModeStateChanged -= CreateContinueButtonAfterPlayMode;
        EditorApplication.playModeStateChanged += CreateContinueButtonAfterPlayMode;
        EditorApplication.delayCall += CreateContinueButtonInCurrentEditScene;
    }

    private static void CreateContinueButtonInCurrentEditScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        TourManager manager = UnityEngine.Object.FindAnyObjectByType<TourManager>(FindObjectsInactive.Include);
        manager?.EnsureEditorContinueTourButton();
    }

    private static void CreateContinueButtonAfterPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;

        TourManager manager = UnityEngine.Object.FindAnyObjectByType<TourManager>(FindObjectsInactive.Include);
        manager?.EnsureEditorContinueTourButton();
    }

    /// <summary>
    /// Automated interceptor built into Unity's pipeline. Forces the scene view 
    /// layout to instantly refresh and repaint when inspection booleans toggle.
    /// </summary>
    private void OnValidate()
    {
        if (Application.isPlaying) return;

        EnsureEditorContinueTourButton();
        SceneView.RepaintAll();
    }

    private void EnsureEditorContinueTourButton()
    {
        if (continueTourButton != null) return;

        Button template = null;
        foreach (Button button in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include))
        {
            if (button != null && (button.name == "Btn_GuideMe" || button.name == "Btn_Guide_Me"))
            {
                template = button;
                break;
            }
        }
        if (template == null) return;

        Transform hub = null;
        foreach (Transform candidate in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (candidate != null && candidate.name == "AR_Exploration_Hub")
            {
                hub = candidate;
                break;
            }
        }
        if (hub == null) return;

        GameObject buttonObject = UnityEngine.Object.Instantiate(template.gameObject, hub);
        buttonObject.name = "Btn_Continue_Tour";
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 116f);

        continueTourButton = buttonObject.GetComponent<Button>();
        continueTourButton.onClick = new Button.ButtonClickedEvent();
        buttonObject.SetActive(false);
        EditorUtility.SetDirty(continueTourButton);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    private void OnDrawGizmosSelected()
    {
        if (simulatedGpsPlayer == null)
        {
            GameObject foundPlayer = GameObject.Find("Simulated_GPS_Player");
            if (foundPlayer != null) simulatedGpsPlayer = foundPlayer.transform;
        }

        for (int t = 0; t < officialTours.Count; t++)
        {
            PresetTour tour = officialTours[t];
            if (t == 0 && !showMinimalTour) continue;
            if (t == 1 && !showIntermediateTour) continue;
            if (t >= 2 && !showCompleteTour) continue;

            if (tour.orderedStoneIDs == null || tour.orderedStoneIDs.Count == 0) continue;

            Gizmos.color = tour.tourColor;

            Vector3 lastPosition = simulatedGpsPlayer != null ? simulatedGpsPlayer.position : Vector3.zero;
            bool hasValidLastPos = simulatedGpsPlayer != null;

            for (int i = 0; i < tour.orderedStoneIDs.Count; i++)
            {
                GameObject nodeObj = GameObject.Find("point_" + tour.orderedStoneIDs[i]);
                if (nodeObj == null) continue;

                Vector3 currentPos = nodeObj.transform.position;
                Gizmos.DrawSphere(currentPos, 0.75f);

                Handles.Label(currentPos + Vector3.up * 1.5f, $"{tour.tourName[0]}-{i + 1}", new GUIStyle { normal = { textColor = tour.tourColor }, fontStyle = FontStyle.Bold });

                if (hasValidLastPos)
                {
                    NavMeshPath gizmoPath = new NavMeshPath();
                    Vector3 startSnap = lastPosition;
                    Vector3 endSnap = currentPos;
                    if (UnityEngine.AI.NavMesh.SamplePosition(lastPosition, out UnityEngine.AI.NavMeshHit hStart, 5.0f, UnityEngine.AI.NavMesh.AllAreas)) startSnap = hStart.position;
                    if (UnityEngine.AI.NavMesh.SamplePosition(currentPos, out UnityEngine.AI.NavMeshHit hEnd, 5.0f, UnityEngine.AI.NavMesh.AllAreas)) endSnap = hEnd.position;

                    if (UnityEngine.AI.NavMesh.CalculatePath(startSnap, endSnap, UnityEngine.AI.NavMesh.AllAreas, gizmoPath) && gizmoPath.corners.Length > 1)
                    {
                        for (int c = 0; c < gizmoPath.corners.Length - 1; c++)
                        {
                            Gizmos.DrawLine(gizmoPath.corners[c], gizmoPath.corners[c + 1]);
                        }
                    }
                    else
                    {
                        Gizmos.DrawLine(lastPosition, currentPos);
                    }
                }

                lastPosition = currentPos;
                hasValidLastPos = true;
            }
        }
    }

    [ContextMenu("⚙️ Execute Editor Pre-Bake for All Tours")]
    public void PreBakeAllToursInEditor()
    {
        routeManager = UnityEngine.Object.FindAnyObjectByType<RouteManager>(FindObjectsInactive.Include);

        if (simulatedGpsPlayer == null)
        {
            GameObject foundPlayer = GameObject.Find("Simulated_GPS_Player");
            if (foundPlayer != null) simulatedGpsPlayer = foundPlayer.transform;
        }

        if (simulatedGpsPlayer == null)
        {
            Debug.LogError("[Tour Pre-Bake] CRITICAL: Cannot find 'Simulated_GPS_Player' in the hierarchy! Assign it to set the parking lot origin.");
            return;
        }

        GameObject[] allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        List<GameObject> stoneAnchors = new List<GameObject>();

        foreach (GameObject obj in allObjects)
        {
            if (obj != null && obj.name.StartsWith("point_"))
            {
                bool shouldExclude = false;
                foreach (string keyword in excludedKeywords)
                {
                    if (obj.name.ToLower().Contains(keyword.ToLower()))
                    {
                        shouldExclude = true;
                        break;
                    }
                }

                if (!shouldExclude) stoneAnchors.Add(obj);
            }
        }

        if (stoneAnchors.Count == 0)
        {
            Debug.LogError("[Tour Pre-Bake] Cannot find any valid 'point_' game objects matching criteria.");
            return;
        }

        Vector3 editorStartingPivot = simulatedGpsPlayer.position;
        preBakeDistanceCache = new Dictionary<Vector3, Dictionary<Vector3, float>>();

        foreach (PresetTour tour in officialTours)
        {
            tour.orderedStoneIDs.Clear();
            List<GameObject> candidatePool = new List<GameObject>(stoneAnchors);

            int targetCount = tour.isCompleteTour ? candidatePool.Count : Mathf.Min(tour.desiredStopsCount, candidatePool.Count);

            // Select and order the K stops jointly: a compact cluster beats isolated stops
            // that only happen to be close to the starting point.
            List<GameObject> routeChain = BuildCompactRoute(editorStartingPivot, candidatePool, targetCount);
            float initialDistance = CalculateRouteDistance(editorStartingPivot, routeChain, out int initialUnavailableLegs);

            // Step 3: Apply 2-Opt Local Search to unroll loops and eliminate crossovers
            Apply2OptOptimization(editorStartingPivot, routeChain);
            float optimizedDistance = CalculateRouteDistance(editorStartingPivot, routeChain, out int optimizedUnavailableLegs);

            // Step 4: Populate final ordered IDs
            foreach (GameObject node in routeChain)
            {
                string cleanID = node.name.Replace("point_", "").Trim();
                tour.orderedStoneIDs.Add(cleanID);
            }

            float improvement = initialDistance > 0f ? (initialDistance - optimizedDistance) / initialDistance * 100f : 0f;
            Debug.Log($"[Tour Pre-Bake] '{tour.tourName}': {tour.orderedStoneIDs.Count} stops, {initialDistance:F0}m -> {optimizedDistance:F0}m ({improvement:F1}% improvement), unreachable legs {initialUnavailableLegs} -> {optimizedUnavailableLegs}.");
        }

        EditorUtility.SetDirty(this);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        EditorSceneManager.SaveScene(gameObject.scene);
        preBakeDistanceCache = null;
    }

    private List<GameObject> BuildCompactRoute(Vector3 origin, List<GameObject> pool, int stopCount)
    {
        if (pool.Count == 0 || stopCount == 0) return new List<GameObject>();

        GameObject nearestSeed = pool[0];
        float nearestDistance = CalculateNavMeshPathDistance(origin, nearestSeed.transform.position);
        for (int i = 1; i < pool.Count; i++)
        {
            float distance = CalculateNavMeshPathDistance(origin, pool[i].transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestSeed = pool[i];
            }
        }

        GameObject denseSeed = FindDensestSeed(pool);
        List<GameObject> nearestRoute = BuildGreedyInsertionRoute(origin, pool, stopCount, nearestSeed);
        List<GameObject> denseRoute = denseSeed != nearestSeed
            ? BuildGreedyInsertionRoute(origin, pool, stopCount, denseSeed)
            : nearestRoute;

        float nearestTotal = CalculateRouteDistance(origin, nearestRoute, out _);
        float denseTotal = CalculateRouteDistance(origin, denseRoute, out _);
        return denseTotal < nearestTotal ? denseRoute : nearestRoute;
    }

    private List<GameObject> BuildGreedyInsertionRoute(Vector3 origin, List<GameObject> pool, int stopCount, GameObject seed)
    {
        List<GameObject> remaining = new List<GameObject>(pool);
        List<GameObject> route = new List<GameObject> { seed };
        remaining.Remove(seed);

        while (route.Count < stopCount && remaining.Count > 0)
        {
            int bestNodeIndex = -1;
            int bestInsertionIndex = 0;
            float lowestAddedDistance = float.MaxValue;

            for (int nodeIndex = 0; nodeIndex < remaining.Count; nodeIndex++)
            {
                Vector3 candidate = remaining[nodeIndex].transform.position;
                for (int insertionIndex = 0; insertionIndex <= route.Count; insertionIndex++)
                {
                    Vector3 previous = insertionIndex == 0 ? origin : route[insertionIndex - 1].transform.position;
                    float addedDistance = CalculateNavMeshPathDistance(previous, candidate);
                    if (insertionIndex < route.Count)
                    {
                        Vector3 next = route[insertionIndex].transform.position;
                        addedDistance += CalculateNavMeshPathDistance(candidate, next) - CalculateNavMeshPathDistance(previous, next);
                    }

                    if (addedDistance < lowestAddedDistance)
                    {
                        lowestAddedDistance = addedDistance;
                        bestNodeIndex = nodeIndex;
                        bestInsertionIndex = insertionIndex;
                    }
                }
            }

            if (bestNodeIndex == -1) break;
            route.Insert(bestInsertionIndex, remaining[bestNodeIndex]);
            remaining.RemoveAt(bestNodeIndex);
        }
        return route;
    }

    private static GameObject FindDensestSeed(List<GameObject> pool)
    {
        GameObject densest = pool[0];
        float bestScore = float.MaxValue;

        foreach (GameObject candidate in pool)
        {
            List<float> distances = new List<float>();
            foreach (GameObject other in pool)
            {
                if (other != candidate) distances.Add((candidate.transform.position - other.transform.position).sqrMagnitude);
            }
            distances.Sort();

            float score = 0f;
            int neighbours = Mathf.Min(6, distances.Count);
            for (int i = 0; i < neighbours; i++) score += distances[i];
            if (score < bestScore)
            {
                bestScore = score;
                densest = candidate;
            }
        }
        return densest;
    }

    /// <summary>
    /// Applies 2-Opt local search to unroll crossovers and minimize total path distance.
    /// </summary>
    private void Apply2OptOptimization(Vector3 origin, List<GameObject> route)
    {
        if (route.Count < 4) return;

        bool improved = true;
        int maxPasses = 50;
        int pass = 0;

        while (improved && pass < maxPasses)
        {
            improved = false;
            pass++;

            for (int i = 0; i < route.Count - 1; i++)
            {
                for (int j = i + 2; j < route.Count; j++)
                {
                    Vector3 pA = (i == 0) ? origin : route[i - 1].transform.position;
                    Vector3 pB = route[i].transform.position;
                    Vector3 pC = route[j - 1].transform.position;
                    Vector3 pD = (j < route.Count) ? route[j].transform.position : route[j - 1].transform.position;

                    float currentDist = CalculateNavMeshPathDistance(pA, pB) + CalculateNavMeshPathDistance(pC, pD);
                    float newDist = CalculateNavMeshPathDistance(pA, pC) + CalculateNavMeshPathDistance(pB, pD);

                    if (newDist < currentDist - 0.5f) // Threshold to avoid precision jitter
                    {
                        // Reverse subsegment between i and j-1
                        route.Reverse(i, j - i);
                        improved = true;
                    }
                }
            }
        }
    }

    private float CalculateNavMeshPathDistance(Vector3 start, Vector3 end)
    {
        if (preBakeDistanceCache != null &&
            preBakeDistanceCache.TryGetValue(start, out Dictionary<Vector3, float> cachedFromStart) &&
            cachedFromStart.TryGetValue(end, out float cachedDistance))
        {
            return cachedDistance;
        }

        Vector3 snappedStart = start;
        Vector3 snappedEnd = end;
        float result;

        bool startOnNavMesh = NavMesh.SamplePosition(start, out NavMeshHit hitStart, 50.0f, NavigationAreaMask.VisitorWalkable);
        bool endOnNavMesh = NavMesh.SamplePosition(end, out NavMeshHit hitEnd, 50.0f, NavigationAreaMask.VisitorWalkable);
        if (startOnNavMesh) snappedStart = hitStart.position;
        if (endOnNavMesh) snappedEnd = hitEnd.position;

        if (startOnNavMesh && endOnNavMesh)
        {
            NavMeshPath path = new NavMeshPath();
            if (NavMesh.CalculatePath(snappedStart, snappedEnd, NavigationAreaMask.VisitorWalkable, path) &&
                path.status == NavMeshPathStatus.PathComplete)
            {
                float totalLength = 0f;
                for (int i = 0; i < path.corners.Length - 1; i++)
                {
                    totalLength += Vector3.Distance(path.corners[i], path.corners[i + 1]);
                }
                result = totalLength;
            }
            else result = 1000000f;
        }
        else result = 1000000f;

        CacheNavMeshDistance(start, end, result);
        return result;
    }

    private void CacheNavMeshDistance(Vector3 start, Vector3 end, float distance)
    {
        if (preBakeDistanceCache == null) return;
        if (!preBakeDistanceCache.TryGetValue(start, out Dictionary<Vector3, float> fromStart))
        {
            fromStart = new Dictionary<Vector3, float>();
            preBakeDistanceCache.Add(start, fromStart);
        }
        fromStart[end] = distance;

        if (!preBakeDistanceCache.TryGetValue(end, out Dictionary<Vector3, float> fromEnd))
        {
            fromEnd = new Dictionary<Vector3, float>();
            preBakeDistanceCache.Add(end, fromEnd);
        }
        fromEnd[start] = distance;
    }

    private float CalculateRouteDistance(Vector3 origin, List<GameObject> route, out int unavailableLegs)
    {
        float total = 0f;
        unavailableLegs = 0;
        Vector3 previous = origin;

        foreach (GameObject node in route)
        {
            float distance = CalculateNavMeshPathDistance(previous, node.transform.position);
            if (distance >= 1000000f) unavailableLegs++;
            else total += distance;
            previous = node.transform.position;
        }

        return total;
    }
#endif
}
