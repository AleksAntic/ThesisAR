using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Collections;

using TMP_Text = TMPro.TMP_Text;

/// <summary>
/// Manages runtime route planning and official tour visualizations.
/// Optimized to leverage Unity's native NavMesh pathfinding engine asynchronously via Coroutines,
/// incorporating dynamic real-time user position tracking, performance-friendly rerouting loops,
/// and automated time-slice calculations to trigger synchronized narrative dialogue systems.
/// All internal code, variables, and logs are strictly maintained in English.
/// </summary>
public class RouteManager : MonoBehaviour
{
    [Header("🎯 Route Geometry Configuration")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private TMP_Text routeInfoText;
    [SerializeField] private float pathLineWidth = 2f;
    [SerializeField] private string targetMapLayer = "MapMarkers";
    [SerializeField] private float lineVerticalOffset = 0.5f;

    [Header("🕹️ Simulation & Dynamic Rerouting")]
    [Tooltip("Reference to the simulation player object used for editor/runtime position testing.")]
    [SerializeField] private Transform simulatedGpsPlayer;
    [Tooltip("How often (in seconds) the system checks the player's distance to trigger a path recalculation.")]
    [SerializeField] private float rerouteCheckInterval = 0.5f;
    [Tooltip("Minimum distance threshold (in meters) the player must move before recalculating the NavMesh path.")]
    [SerializeField] private float rerouteDistanceThreshold = 1.5f;

    private List<string> selectedStoneIDs = new List<string>();
    private List<InteractiveMapPin> routeWaypoints = new List<InteractiveMapPin>();
    private List<InteractiveMapPin> highlightedPins = new List<InteractiveMapPin>();
    private bool isRoutePlanningActive = false;

    // Rerouting engine internal trackers
    private Vector3 lastRecalculatedPlayerPos;
    private float rerouteTimer;
    private Coroutine activeRouteCoroutine;

    void Start()
    {
        if (lineRenderer == null) lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null) lineRenderer = gameObject.AddComponent<LineRenderer>();

        if (lineRenderer != null)
        {
            GameObject lineHost = new GameObject("Runtime_Line_Host");
            lineHost.transform.position = Vector3.zero;
            int layerIdx = LayerMask.NameToLayer(targetMapLayer);
            if (layerIdx != -1)
            {
                lineHost.layer = layerIdx;
                lineRenderer.gameObject.layer = layerIdx;
            }
            lineRenderer.transform.SetParent(lineHost.transform, false);

            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = pathLineWidth;
            lineRenderer.endWidth = pathLineWidth;
            Color routeColor = Color.yellow;
            Map2DController map2D = UnityEngine.Object.FindAnyObjectByType<Map2DController>(FindObjectsInactive.Include);
            if (map2D != null) routeColor = map2D.GetRouteLineColor();
            lineRenderer.startColor = routeColor;
            lineRenderer.endColor = routeColor;
            lineRenderer.alignment = LineAlignment.View;
        }

        if (simulatedGpsPlayer == null)
        {
            GameObject foundSimPlayer = GameObject.Find("Simulated_GPS_Player");
            if (foundSimPlayer != null)
            {
                simulatedGpsPlayer = foundSimPlayer.transform;
                Debug.Log("[RouteManager] Automatically bound 'Simulated_GPS_Player' transform for location simulation verification.");
            }
        }

        ClearAndResetRoute();
    }

    void Update()
    {
        if (routeWaypoints.Count > 0 && lineRenderer != null && lineRenderer.enabled)
        {
            HandleDynamicReroutingLoop();
        }
    }

    private Vector3 lastTrackedUserPos;
    private float dynamicUserWalkingSpeed = 1.3f; // Default baseline: 1.3 m/s (standard walking speed)

    private void TrackDynamicUserSpeed()
    {
        Vector3 currentPos = GetUserTargetOrigin();
        if (lastTrackedUserPos != Vector3.zero && Time.deltaTime > 0f)
        {
            float instantSpeed = Vector3.Distance(currentPos, lastTrackedUserPos) / Time.deltaTime;
            // Filter realistic human walking speeds (0.4 m/s ~ 1.4 km/h to 2.8 m/s ~ 10 km/h)
            if (instantSpeed >= 0.4f && instantSpeed <= 2.8f)
            {
                // Exponential moving average filter for smooth speed tracking
                dynamicUserWalkingSpeed = Mathf.Lerp(dynamicUserWalkingSpeed, instantSpeed, Time.deltaTime * 0.8f);
            }
        }
        lastTrackedUserPos = currentPos;
    }

    private void HandleDynamicReroutingLoop()
    {
        TrackDynamicUserSpeed();
        rerouteTimer += Time.deltaTime;
        if (rerouteTimer >= rerouteCheckInterval)
        {
            rerouteTimer = 0f;
            Vector3 currentPlayerPos = GetUserTargetOrigin();

            if (Vector3.Distance(currentPlayerPos, lastRecalculatedPlayerPos) >= rerouteDistanceThreshold)
            {
                TriggerRouteUpdate();
            }
        }
    }

    private Vector3 GetUserTargetOrigin()
    {
        if (simulatedGpsPlayer != null)
        {
            return simulatedGpsPlayer.position;
        }

        Map2DController map2D = UnityEngine.Object.FindAnyObjectByType<Map2DController>(FindObjectsInactive.Include);
        if (map2D != null)
        {
            return map2D.GetUserCurrentWorldPosition();
        }

        return Vector3.zero;
    }

    public void ToggleRoutePlanningMode(bool activate)
    {
        isRoutePlanningActive = activate;
        if (!isRoutePlanningActive) ClearAndResetRoute();
    }

    public bool IsInModalitaPercorso() => isRoutePlanningActive;
    public List<string> GetSelectedStoneIDs() => selectedStoneIDs;

    public void GestisciTappa(string stoneID, InteractiveMapPin pinInstance)
    {
        if (!isRoutePlanningActive) return;

        if (pinInstance == null)
        {
            InteractiveMapPin[] allPins = UnityEngine.Object.FindObjectsByType<InteractiveMapPin>(FindObjectsInactive.Include);
            foreach (var pin in allPins)
            {
                if (pin != null)
                {
                    Transform pointTr = pin.transform.name.StartsWith("point_") ? pin.transform : pin.transform.parent;
                    if (pointTr != null)
                    {
                        string id = pointTr.name.Replace("point_", "").Trim();
                        if (id.Equals(stoneID, System.StringComparison.OrdinalIgnoreCase))
                        {
                            pinInstance = pin;
                            break;
                        }
                    }
                }
            }
        }

        if (selectedStoneIDs.Contains(stoneID))
        {
            selectedStoneIDs.Remove(stoneID);
            if (pinInstance != null)
            {
                routeWaypoints.Remove(pinInstance);
                highlightedPins.Remove(pinInstance);
                pinInstance.ResetToOriginalColor();
            }
        }
        else
        {
            TourManager tourManager = UnityEngine.Object.FindAnyObjectByType<TourManager>(FindObjectsInactive.Include);
            int insertionIndex = (tourManager != null && tourManager.HasSelectedTour)
                ? FindShortestDetourInsertionIndex(stoneID)
                : selectedStoneIDs.Count;
            selectedStoneIDs.Insert(insertionIndex, stoneID);
            if (pinInstance != null)
            {
                routeWaypoints.Insert(insertionIndex, pinInstance);
                if (!highlightedPins.Contains(pinInstance)) highlightedPins.Add(pinInstance);
                
                Color selColor = Color.yellow;
                Map2DController map2D = UnityEngine.Object.FindAnyObjectByType<Map2DController>(FindObjectsInactive.Include);
                if (map2D != null) selColor = map2D.GetSelectedRoutePinColor();
                pinInstance.SetMarkerColor(selColor);
            }
        }

        TriggerRouteUpdate();
    }

    private int FindShortestDetourInsertionIndex(string stoneID)
    {
        MemorialSpawner spawner = UnityEngine.Object.FindAnyObjectByType<MemorialSpawner>(FindObjectsInactive.Include);
        GameObject candidate = spawner != null ? spawner.GetSpawnedMemorial(stoneID) : null;
        if (candidate == null) return selectedStoneIDs.Count;

        Vector3 candidatePosition = candidate.transform.position;
        Vector3 previousPosition = GetUserTargetOrigin();
        float bestCost = float.MaxValue;
        int bestIndex = selectedStoneIDs.Count;

        for (int i = 0; i <= selectedStoneIDs.Count; i++)
        {
            Vector3 nextPosition = Vector3.zero;
            bool hasNext = i < selectedStoneIDs.Count;
            if (hasNext)
            {
                GameObject next = spawner.GetSpawnedMemorial(selectedStoneIDs[i]);
                if (next == null) continue;
                nextPosition = next.transform.position;
            }

            float cost = CalculateWalkableDistance(previousPosition, candidatePosition);
            if (hasNext) cost += CalculateWalkableDistance(candidatePosition, nextPosition) - CalculateWalkableDistance(previousPosition, nextPosition);
            if (cost < bestCost)
            {
                bestCost = cost;
                bestIndex = i;
            }

            if (hasNext) previousPosition = nextPosition;
        }

        return bestIndex;
    }

    public void SetRoutePlanningModeWithoutClearing(bool active)
    {
        isRoutePlanningActive = active;
    }

    public void TriggerRouteUpdate()
    {
        if (activeRouteCoroutine != null)
        {
            StopCoroutine(activeRouteCoroutine);
        }
        activeRouteCoroutine = StartCoroutine(UpdateRouteVisualizationCoroutine());
    }

    private IEnumerator UpdateRouteVisualizationCoroutine()
    {
        if (lineRenderer == null) yield break;

        if (routeWaypoints.Count == 0)
        {
            lineRenderer.positionCount = 0;
            lineRenderer.enabled = false;
            yield break;
        }

        List<Vector3> completePathPoints = new List<Vector3>();
        Vector3 currentStart = GetUserTargetOrigin();
        lastRecalculatedPlayerPos = currentStart;
        int unavailableSegments = 0;

        int segmentsCalculatedInThisFrame = 0;
        const int maxSegmentsPerFrameBudget = 3;

        for (int i = 0; i < routeWaypoints.Count; i++)
        {
            if (routeWaypoints[i] == null) continue;
            Vector3 currentTarget = routeWaypoints[i].transform.position;

            Vector3 snappedStart = currentStart;
            Vector3 snappedTarget = currentTarget;

            if (NavMesh.SamplePosition(currentStart, out NavMeshHit hitStart, 5.0f, NavigationAreaMask.VisitorWalkable)) snappedStart = hitStart.position;
            if (NavMesh.SamplePosition(currentTarget, out NavMeshHit hitTarget, 5.0f, NavigationAreaMask.VisitorWalkable)) snappedTarget = hitTarget.position;

            NavMeshPath navMeshPath = new NavMeshPath();

            if (NavMesh.CalculatePath(snappedStart, snappedTarget, NavigationAreaMask.VisitorWalkable, navMeshPath) &&
                navMeshPath.status == NavMeshPathStatus.PathComplete && navMeshPath.corners.Length > 1)
            {
                foreach (Vector3 corner in navMeshPath.corners)
                {
                    AddPointUnique(completePathPoints, corner);
                }

                currentStart = currentTarget;
            }
            else
            {
                unavailableSegments++;
                Debug.LogWarning($"[Route Manager] No complete NavMesh path to '{routeWaypoints[i].name}'. Segment is omitted instead of drawing through an obstacle.");
            }

            segmentsCalculatedInThisFrame++;
            if (segmentsCalculatedInThisFrame >= maxSegmentsPerFrameBudget)
            {
                segmentsCalculatedInThisFrame = 0;
                yield return null;
            }
        }

        if (completePathPoints.Count >= 2)
        {
            lineRenderer.enabled = true;
            lineRenderer.positionCount = completePathPoints.Count;

            float totalDistance = 0f;
            for (int w = 0; w < completePathPoints.Count; w++)
            {
                Vector3 p = completePathPoints[w];
                p.y = lineVerticalOffset;
                lineRenderer.SetPosition(w, p);

                if (w > 0)
                {
                    totalDistance += Vector3.Distance(completePathPoints[w - 1], completePathPoints[w]);
                }
            }

            if (routeInfoText != null)
            {
                routeInfoText.text = $"<b>Route:</b> {routeWaypoints.Count} stops\n<b>Distance:</b> {totalDistance:F1} meters";
            }

            // --- NARRATIVE INTEGRATION: AUTOMATED TIME-SLICER EVALUATION LOOP ---
            bool personalGuideOwnsWalkingNarration = ThesisManager.Instance != null &&
                                                      ThesisManager.Instance.CurrentMode == ThesisManager.GuidanceMode.Personal;
            if (DialogueManager.Instance != null && isRoutePlanningActive && !personalGuideOwnsWalkingNarration)
            {
                // Calculate estimated travel seconds matching the user's DYNAMIC real-time walking speed
                float activeSpeed = (dynamicUserWalkingSpeed >= 0.4f) ? dynamicUserWalkingSpeed : 1.3f;
                float estimatedTravelSeconds = totalDistance / activeSpeed;

                Debug.Log($"[RouteManager] Time-Slicer dynamic evaluation: Distance={totalDistance:F1}m, UserSpeed={activeSpeed:F2}m/s -> Estimated travel time: {estimatedTravelSeconds:F1}s");

                // Dynamically scan the Resources directory for compiled ScriptableObject dialogue pools
                DialogueSequence[] availableSequences = Resources.LoadAll<DialogueSequence>("Narrative/GlobalWalking");

                if (availableSequences != null && availableSequences.Length > 0)
                {
                    List<DialogueSequence> poolList = new List<DialogueSequence>(availableSequences);
                    List<DialogueSequence.DialogueLine> optimalLines = DialogueManager.Instance.CalculateOptimalTimeSliceQueue(poolList, estimatedTravelSeconds);

                    // Execute narration sequences exclusively on active travel legs exceeding 12 meters to avoid overlapping
                    if (optimalLines.Count > 0 && totalDistance > 12f)
                    {
                        // Compile a safe on-the-fly dynamic runtime asset container to hold scheduled items
                        DialogueSequence runtimeSequence = ScriptableObject.CreateInstance<DialogueSequence>();
                        runtimeSequence.sequenceLabelId = "Runtime_Scheduled_Tour_Leg";
                        runtimeSequence.narrativeCategory = DialogueSequence.DialogueCategory.Global_Walking;
                        runtimeSequence.dialogueLines = optimalLines;

                        DialogueManager.Instance.PlayNarrativeSequence(runtimeSequence);
                    }
                }
            }
        }

        activeRouteCoroutine = null;
    }

    private void AddPointUnique(List<Vector3> list, Vector3 p)
    {
        if (list.Count == 0 || Vector3.Distance(list[list.Count - 1], p) > 0.1f)
        {
            list.Add(p);
        }
    }

    public void ClearAndResetRoute()
    {
        if (activeRouteCoroutine != null)
        {
            StopCoroutine(activeRouteCoroutine);
            activeRouteCoroutine = null;
        }

        foreach (InteractiveMapPin pin in highlightedPins)
        {
            if (pin != null) pin.ResetToOriginalColor();
        }
        highlightedPins.Clear();
        selectedStoneIDs.Clear();
        routeWaypoints.Clear();

        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 0;
            lineRenderer.enabled = false;
        }
        if (routeInfoText != null) routeInfoText.text = "";

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.ForceStopNarrationImmediate();
        }
    }

    public void ImpostaPercorsoPredefinitoDaID(List<string> listaID)
    {
        if (listaID == null || listaID.Count == 0) return;

        InteractiveMapPin[] allPins = UnityEngine.Object.FindObjectsByType<InteractiveMapPin>(FindObjectsInactive.Include);
        Dictionary<string, InteractiveMapPin> pinDict = new Dictionary<string, InteractiveMapPin>(System.StringComparer.OrdinalIgnoreCase);

        foreach (var pin in allPins)
        {
            if (pin != null)
            {
                Transform pointTr = pin.transform.name.StartsWith("point_") ? pin.transform : pin.transform.parent;
                if (pointTr != null)
                {
                    string id = pointTr.name.Replace("point_", "").Trim();
                    if (!pinDict.ContainsKey(id)) pinDict.Add(id, pin);
                }
            }
        }

        ClearAndResetRoute();

        foreach (string id in listaID)
        {
            if (pinDict.TryGetValue(id, out InteractiveMapPin pin))
            {
                selectedStoneIDs.Add(id);
                routeWaypoints.Add(pin);
                if (!highlightedPins.Contains(pin)) highlightedPins.Add(pin);
                
                Color selColor = Color.yellow;
                Map2DController map2D = UnityEngine.Object.FindAnyObjectByType<Map2DController>(FindObjectsInactive.Include);
                if (map2D != null) selColor = map2D.GetSelectedRoutePinColor();
                pin.SetMarkerColor(selColor);
            }
        }

        TriggerRouteUpdate();
    }

    public void TriggerSaveCurrentRoute(string userCustomLabel, float calculatedDistance)
    {
        PersistenceManager persistence = UnityEngine.Object.FindAnyObjectByType<PersistenceManager>(FindObjectsInactive.Include);
        if (persistence != null && selectedStoneIDs.Count > 0)
        {
            persistence.SaveCustomRoute(userCustomLabel, selectedStoneIDs, calculatedDistance);
            Debug.Log($"[Route Integration] Saved custom route '{userCustomLabel}' containing {selectedStoneIDs.Count} nodes.");
        }
    }

    public void RemoveWaypointFromCurrentRoute(string stoneID)
    {
        if (string.IsNullOrEmpty(stoneID)) return;

        int index = selectedStoneIDs.FindIndex(id => id.Equals(stoneID, System.StringComparison.OrdinalIgnoreCase));

        if (index != -1)
        {
            selectedStoneIDs.RemoveAt(index);

            if (routeWaypoints != null && index < routeWaypoints.Count)
            {
                InteractiveMapPin pin = routeWaypoints[index];
                if (pin != null)
                {
                    pin.ResetToOriginalColor();
                    if (highlightedPins.Contains(pin)) highlightedPins.Remove(pin);
                }
                routeWaypoints.RemoveAt(index);
            }

            Debug.Log($"[Route Manager] Successfully removed waypoint '{stoneID}' from the current active path layout.");
            TriggerRouteUpdate();
        }
    }

    public void OptimizeCurrentRouteDistances()
    {
        if (selectedStoneIDs == null || selectedStoneIDs.Count <= 2)
        {
            Debug.Log("[Route Manager] Optimization bypassed: Path needs at least 3 points to sort topology grids.");
            return;
        }

        Vector3 startingPivotPoint = simulatedGpsPlayer != null ? simulatedGpsPlayer.position : transform.position;

        MemorialSpawner spawnerLookup = UnityEngine.Object.FindAnyObjectByType<MemorialSpawner>(FindObjectsInactive.Include);
        if (spawnerLookup == null) return;

        List<string> unvisitedNodes = new List<string>(selectedStoneIDs);
        List<string> optimizedNodesOrder = new List<string>();

        Vector3 currentSearchOrigin = startingPivotPoint;

        while (unvisitedNodes.Count > 0)
        {
            int closestIndex = -1;
            float minimumDisplacement = float.MaxValue;
            Vector3 closestNodeWorldPos = Vector3.zero;

            for (int i = 0; i < unvisitedNodes.Count; i++)
            {
                GameObject stoneObject = spawnerLookup.GetSpawnedMemorial(unvisitedNodes[i]);
                if (stoneObject != null)
                {
                    float distanceHeuristic = CalculateWalkableDistance(currentSearchOrigin, stoneObject.transform.position);
                    if (distanceHeuristic < minimumDisplacement)
                    {
                        minimumDisplacement = distanceHeuristic;
                        closestIndex = i;
                        closestNodeWorldPos = stoneObject.transform.position;
                    }
                }
            }

            if (closestIndex != -1)
            {
                optimizedNodesOrder.Add(unvisitedNodes[closestIndex]);
                currentSearchOrigin = closestNodeWorldPos;
                unvisitedNodes.RemoveAt(closestIndex);
            }
            else
            {
                optimizedNodesOrder.AddRange(unvisitedNodes);
                break;
            }
        }

        selectedStoneIDs = optimizedNodesOrder;

        List<InteractiveMapPin> synchronizedPins = new List<InteractiveMapPin>();
        InteractiveMapPin[] allActivePins = UnityEngine.Object.FindObjectsByType<InteractiveMapPin>(FindObjectsInactive.Include);

        foreach (string id in selectedStoneIDs)
        {
            foreach (var pin in allActivePins)
            {
                Transform pointTr = (pin != null && pin.transform.name.StartsWith("point_")) ? pin.transform : (pin != null ? pin.transform.parent : null);
                if (pointTr != null && pointTr.name.Contains(id))
                {
                    if (!synchronizedPins.Contains(pin)) synchronizedPins.Add(pin);
                    break;
                }
            }
        }
        routeWaypoints = synchronizedPins;

        Debug.Log("[Route Manager] Path optimization concluded successfully. Waypoints sorted by proximity factor.");
        TriggerRouteUpdate();
    }

    private static float CalculateWalkableDistance(Vector3 start, Vector3 end)
    {
        if (!NavMesh.SamplePosition(start, out NavMeshHit snappedStart, 10f, NavigationAreaMask.VisitorWalkable) ||
            !NavMesh.SamplePosition(end, out NavMeshHit snappedEnd, 10f, NavigationAreaMask.VisitorWalkable))
        {
            return float.MaxValue;
        }

        NavMeshPath path = new NavMeshPath();
        if (!NavMesh.CalculatePath(snappedStart.position, snappedEnd.position, NavigationAreaMask.VisitorWalkable, path) ||
            path.status != NavMeshPathStatus.PathComplete)
        {
            return float.MaxValue;
        }

        float length = 0f;
        for (int i = 0; i < path.corners.Length - 1; i++)
        {
            length += Vector3.Distance(path.corners[i], path.corners[i + 1]);
        }
        return length;
    }
}
