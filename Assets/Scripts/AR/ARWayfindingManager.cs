using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Handles real-time AR pathfinding using Unity NavMesh baked on top of the ProBuilder path layout.
/// Computes accurate paths toward GLB nodes and feeds coordinates to both the LineRenderer and 2D Arrow Sprites
/// with CPU throttling optimization and structural Object Pooling to maximize device performance.
/// All internal architecture code is maintained strictly in English.
/// </summary>
public class ARWayfindingManager : MonoBehaviour
{
    public event Action<string> OnMemorialReached;

    [SerializeField] private MemorialSpawner memorialSpawner;
    [SerializeField] private LineRenderer pathLineRenderer;
    [SerializeField] private Transform userCameraTransform;
    [SerializeField] private float arrivalDistanceThreshold = 2.5f;

    [Header("⏳ Optimization Settings")]
    [Tooltip("Time interval (in seconds) between NavMesh path calculations to safeguard mobile CPU and battery.")]
    [SerializeField] private float pathUpdateInterval = 0.3f;

    [Header("🏹 AR Sprite Trail Settings")]
    [Tooltip("The 2D arrow/sprite prefab that lies flat on the terrain.")]
    [SerializeField] private GameObject directionalArrowPrefab;
    [Tooltip("Vertical offset to prevent the sprite from clipping into the terrain geometry.")]
    [SerializeField] private float verticalGroundOffset = 0.15f;
    [Tooltip("Safe distance threshold from camera to prevent arrows from clipping directly into the user's face.")]
    [SerializeField] private float minCameraClippingDistance = 4.0f;
    [Tooltip("Which layers represent the walkable ground/terrain for raycast snapping.")]
    [SerializeField] private LayerMask groundLayerMask = ~0; // Default to everything

    private Transform targetTransform;
    private string currentTargetID;
    private NavMeshPath navMeshPath;
    private bool isNavigating = false;
    private float nextPathUpdateTime = 0f;

    // Architectural memory optimization layer utilizing the custom SimpleGameObjectPool
    private SimpleGameObjectPool arrowPool;
    private readonly List<GameObject> activeSpawnedArrows = new List<GameObject>();

    public Vector3? NextWaypoint => (navMeshPath != null && navMeshPath.corners.Length > 1) ? navMeshPath.corners[1] : null;

    void Awake()
    {
        arrivalDistanceThreshold = Mathf.Max(arrivalDistanceThreshold, 5f);
        navMeshPath = new NavMeshPath();
        if (pathLineRenderer == null) pathLineRenderer = GetComponent<LineRenderer>();
        GameObject simulatedPlayer = GameObject.Find("Simulated_GPS_Player");
        if (simulatedPlayer != null) userCameraTransform = simulatedPlayer.transform;
        else if (userCameraTransform == null) userCameraTransform = Camera.main != null ? Camera.main.transform : null;
        if (memorialSpawner == null) memorialSpawner = UnityEngine.Object.FindAnyObjectByType<MemorialSpawner>(FindObjectsInactive.Include);
    }

    void Start()
    {
        // Pre-warms the memory buffer with 30 inactive instances to completely eliminate runtime allocation spikes
        if (directionalArrowPrefab != null)
        {
            arrowPool = new SimpleGameObjectPool(directionalArrowPrefab, 30, this.transform);
        }
        else
        {
            Debug.LogError("[Wayfinding] Critical Dependency Missing: DirectionalArrowPrefab is unassigned inside the Inspector blueprint layout.");
        }
    }

    void Update()
    {
        if (!isNavigating || targetTransform == null || userCameraTransform == null) return;

        // Throttled NavMesh computation running safely in the background thread layout
        if (Time.time >= nextPathUpdateTime)
        {
            UpdatePathRoute();
            nextPathUpdateTime = Time.time + pathUpdateInterval;
        }

        CheckArrivalDistance();
    }

    /// <summary>
    /// Initiates route navigation using NavMesh towards a specific GLB marker node found by ID.
    /// </summary>
    public void NavigateTo(string memorialID)
    {
        if (memorialSpawner == null) return;

        GameObject targetObject = memorialSpawner.GetSpawnedMemorial(memorialID);
        if (targetObject == null)
        {
            Debug.LogWarning($"[Wayfinding] Target node '{memorialID}' could not be located inside the bound GLB nodes.");
            return;
        }

        targetTransform = targetObject.transform;
        currentTargetID = memorialID;
        isNavigating = true;
        nextPathUpdateTime = 0f;

        if (pathLineRenderer != null) pathLineRenderer.enabled = true;
    }

    /// <summary>
    /// Shuts down active running navigation tracking states and flushes 3D/2D visual trail pointers.
    /// </summary>
    public void StopNavigation()
    {
        isNavigating = false;
        targetTransform = null;
        currentTargetID = string.Empty;
        lastArrowTrailCorners = null;

        if (pathLineRenderer != null)
        {
            pathLineRenderer.positionCount = 0;
            pathLineRenderer.enabled = false;
        }

        ClearActiveArrowTrail();
    }

    public void ForceArrivalForEditorTesting(string memorialID)
    {
        if (string.IsNullOrEmpty(memorialID)) return;
        StopNavigation();
        OnMemorialReached?.Invoke(memorialID);
    }

    [Header("🎯 Arrow Regeneration Stability")]
    [Tooltip("Distanza minima (metri) che il percorso deve spostarsi prima di rigenerare le frecce.")]
    [SerializeField] private float arrowRegenerationThreshold = 0.75f;

    private Vector3[] lastArrowTrailCorners = null;

    private void UpdatePathRoute()
    {
        Vector3 snappedStart = userCameraTransform.position;
        Vector3 snappedTarget = targetTransform.position;

        if (NavMesh.SamplePosition(userCameraTransform.position, out NavMeshHit startHit, 5.0f, NavigationAreaMask.VisitorWalkable)) snappedStart = startHit.position;
        if (NavMesh.SamplePosition(targetTransform.position, out NavMeshHit targetHit, 5.0f, NavigationAreaMask.VisitorWalkable)) snappedTarget = targetHit.position;

        if (NavMesh.CalculatePath(snappedStart, snappedTarget, NavigationAreaMask.VisitorWalkable, navMeshPath))
        {
            if (navMeshPath.status == NavMeshPathStatus.PathComplete)
            {
                Vector3[] corners = navMeshPath.corners;

                if (pathLineRenderer != null)
                {
                    pathLineRenderer.positionCount = corners.Length;
                    for (int i = 0; i < corners.Length; i++)
                    {
                        pathLineRenderer.SetPosition(i, corners[i]);
                    }
                }

                if (HasPathChangedSignificantly(corners))
                {
                    GenerateDynamicArrowTrail(corners);
                    lastArrowTrailCorners = (Vector3[])corners.Clone();
                }
            }
            else
            {
                Debug.LogWarning($"[Wayfinding] NavMesh.CalculatePath returned status: {navMeshPath.status} (Incomplete path between player {snappedStart} and target {snappedTarget})");
                ClearActiveArrowTrail();
                lastArrowTrailCorners = null;
            }
        }
        else
        {
            Debug.LogError($"[Wayfinding] NavMesh.CalculatePath failed completely to calculate a path between player {snappedStart} and target {snappedTarget}!");
            ClearActiveArrowTrail();
            lastArrowTrailCorners = null;
        }
    }

    private bool HasPathChangedSignificantly(Vector3[] newCorners)
    {
        if (lastArrowTrailCorners == null || lastArrowTrailCorners.Length != newCorners.Length) return true;

        for (int i = 0; i < newCorners.Length; i++)
        {
            if (Vector3.Distance(lastArrowTrailCorners[i], newCorners[i]) > arrowRegenerationThreshold)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Interpolates path vertices points to regularly distribute 2D arrow sprites flat on top of the terrain utilizing Object Pooling.
    /// </summary>
    private void GenerateDynamicArrowTrail(Vector3[] pathCorners)
    {
        // Recycles all currently active arrow transforms back into the structural pool queue instead of destroying them
        ClearActiveArrowTrail();

        if (pathCorners == null || pathCorners.Length < 2 || arrowPool == null) return;

        if (pathLineRenderer != null && pathLineRenderer.sharedMaterial == null)
        {
            Shader urpShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (urpShader != null) pathLineRenderer.material = new Material(urpShader);
        }

        List<Vector3> interpolatedPoints = new List<Vector3>();

        for (int i = 0; i < pathCorners.Length - 1; i++)
        {
            Vector3 startSegment = pathCorners[i];
            Vector3 endSegment = pathCorners[i + 1];
            float segmentMagnitude = Vector3.Distance(startSegment, endSegment);

            Vector3 segmentDirection = (endSegment - startSegment).normalized;
            float currentDistanceOffset = 0f;

            while (currentDistanceOffset < segmentMagnitude)
            {
                Vector3 targetSamplePoint = startSegment + (segmentDirection * currentDistanceOffset);
                interpolatedPoints.Add(targetSamplePoint);

                // Keep the trail readable at phone scale without clustering arrows.
                currentDistanceOffset += 6.0f;
            }
        }

        interpolatedPoints.Add(pathCorners[pathCorners.Length - 1]);

        for (int k = 0; k < interpolatedPoints.Count; k++)
        {
            Vector3 spawnPosition = interpolatedPoints[k];
            // Spariamo un raggio invisibile dall'alto verso il basso per trovare la quota esatta della mesh
            Vector3 rayOrigin = new Vector3(spawnPosition.x, spawnPosition.y + 10f, spawnPosition.z);
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 20f, groundLayerMask))
            {
                spawnPosition.y = hit.point.y + verticalGroundOffset; // Perfettamente adagiata sul terreno reale
            }
            else
            {
                spawnPosition.y += verticalGroundOffset; // Fallback se non colpisce nulla
            }
            spawnPosition.y = Mathf.Max(0f, spawnPosition.y);
            // Safe frustum bypass verification to prevent spatial elements from clipping directly over camera perspective
            if (userCameraTransform != null && Vector3.Distance(spawnPosition, userCameraTransform.position) < minCameraClippingDistance)
            {
                continue;
            }

            // Retrieves a clean, pooled instance seamlessly without causing memory GC spikes
            GameObject arrowInstance = arrowPool.Get(spawnPosition, Quaternion.identity);
            arrowInstance.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);

            DirectionalArrow arrowController = arrowInstance.GetComponent<DirectionalArrow>() ?? arrowInstance.AddComponent<DirectionalArrow>();
            arrowController.SetBasePosition(spawnPosition);

            if (k < interpolatedPoints.Count - 1)
            {
                arrowController.SetLookTarget(interpolatedPoints[k + 1]);
            }
            else
            {
                arrowController.SetLookTarget(targetTransform.position);
            }

            activeSpawnedArrows.Add(arrowInstance);
        }
    }

    private void ClearActiveArrowTrail()
    {
        if (arrowPool == null) return;

        foreach (GameObject arrow in activeSpawnedArrows)
        {
            if (arrow != null)
            {
                arrowPool.Release(arrow);
            }
        }
        activeSpawnedArrows.Clear();
    }

    private void CheckArrivalDistance()
    {
        float distance = Vector3.Distance(userCameraTransform.position, targetTransform.position);
        if (distance <= arrivalDistanceThreshold)
        {
            Debug.Log($"[Wayfinding] User successfully reached target destination: {currentTargetID}");
            StopNavigation();
            OnMemorialReached?.Invoke(currentTargetID);
        }
    }
}
