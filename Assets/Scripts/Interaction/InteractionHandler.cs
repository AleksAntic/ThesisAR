using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Phase 1: Minimal & Isolated Geospatial Screen-Space Viewport Detector.
/// Uses native point_<ID> transform anchors from MemorialSpawner as the spatial source of truth.
/// Evaluates screen-center proximity via Camera.WorldToViewportPoint and applies real-time hysteresis.
/// </summary>
public class InteractionHandler : MonoBehaviour
{
    [Header("🔗 Dependencies")]
    [SerializeField] private UIManager uiManager;
    [SerializeField] private MemorialSpawner memorialSpawner;
    [SerializeField] private float raycastDistance = 1000f;

    [Header("🎯 AR Visual Marker Overlay (Optional Field Indicator)")]
    [Tooltip("Optional 3D AR reticle or ring prefab placed at the physical location of the detected memorial.")]
    [SerializeField] private GameObject arTargetMarkerPrefab;

    [Header("🌐 Geospatial Directional Detection (Phase 1 Parameters)")]
    [Tooltip("Maximum distance (in meters) from camera to detect a memorial stone.")]
    [SerializeField] private float detectionRadius = 10.0f;

    [Tooltip("Maximum allowed distance from screen center (0.0 = exact center, 0.25 = central 50% screen box).")]
    [SerializeField] private float maxScreenCenterOffset = 0.25f;

    [Tooltip("Scan frequency interval (in seconds) for running directional detection.")]
    [SerializeField] private float scanInterval = 0.1f;

    [Tooltip("Duration (in seconds) a new candidate must remain stable before confirming target.")]
    [SerializeField] private float hysteresisDuration = 0.3f;

    [Tooltip("Minimum score improvement required to switch away from an already confirmed target.")]
    [SerializeField] private float hysteresisScoreThreshold = 0.1f;

    [Header("⚖️ Scoring Weights (Screen Center vs Distance)")]
    [Tooltip("Weight multiplier applied to screen-center offset (higher = prioritizes looking directly at stone).")]
    [SerializeField] private float screenCenterWeight = 2.0f;

    [Tooltip("Weight multiplier applied to 3D distance in meters.")]
    [SerializeField] private float distanceWeight = 0.5f;

    [Header("🎮 Editor Simulation Testing")]
    [Tooltip("If true, allows direct raycast selection of 3D proxy stone meshes when testing in the Editor.")]
    [SerializeField] private bool allowEditorProxyRaycast = true;

    private Camera mainCamera;
    private MemorialObject lastSelectedMemorial;
    private ThesisManager thesisManager;

    // Directional Scanning Runtime State
    private float scanTimer = 0f;
    private string confirmedTargetID = null;
    private float confirmedTargetScore = float.MaxValue;
    private string pendingTargetID = null;
    private float pendingTargetTimer = 0f;

    private GameObject activeArMarkerInstance;

    void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("[Interaction] Critical Error: Main camera instance missing from scene context.");
            return;
        }

        if (uiManager == null) uiManager = UnityEngine.Object.FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
        if (memorialSpawner == null) memorialSpawner = UnityEngine.Object.FindAnyObjectByType<MemorialSpawner>(FindObjectsInactive.Include);
        thesisManager = UnityEngine.Object.FindAnyObjectByType<ThesisManager>(FindObjectsInactive.Include);

        if (arTargetMarkerPrefab != null && activeArMarkerInstance == null)
        {
            activeArMarkerInstance = Instantiate(arTargetMarkerPrefab);
            activeArMarkerInstance.name = "AR_Geospatial_Target_Marker";
            activeArMarkerInstance.SetActive(false);
        }
    }

    void Update()
    {
        HandleInput();
        HandleGeospatialViewportScanning();
    }

    private void HandleInput()
    {
        if (Pointer.current == null) return;
        if (IsPointerOverUI()) return;

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            HandleTap(Touchscreen.current.primaryTouch.position.ReadValue());
        }
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            HandleTap(Mouse.current.position.ReadValue());
        }
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;
        if (EventSystem.current.IsPointerOverGameObject()) return true;

        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
        {
            int touchId = Touchscreen.current.touches[0].touchId.ReadValue();
            if (EventSystem.current.IsPointerOverGameObject(touchId)) return true;
        }

        return false;
    }

    /// <summary>
    /// Phase 1 Core Engine: Screen-Space Viewport Detection using native point_<ID> anchors.
    /// </summary>
    private void HandleGeospatialViewportScanning()
    {
        scanTimer += Time.deltaTime;
        if (scanTimer < scanInterval) return;
        scanTimer = 0f;

        // Dependency Timing Guard: Wait until MemorialSpawner has completed binding point_<ID> nodes
        if (mainCamera == null || memorialSpawner == null) return;

        var activeMemorials = memorialSpawner.GetAllSpawnedMemorials();
        if (activeMemorials == null || activeMemorials.Count == 0)
        {
            ClearDirectionalTarget();
            return;
        }

        Vector3 userPos = mainCamera.transform.position;
        Vector2 screenCenter = new Vector2(0.5f, 0.5f);

        string bestCandidateID = null;
        float bestScore = float.MaxValue;
        Vector3 bestWorldPos = Vector3.zero;

        foreach (var kvp in activeMemorials)
        {
            string stoneID = kvp.Key;
            GameObject stoneGO = kvp.Value;
            if (stoneGO == null) continue;

            Vector3 stoneWorldPos = stoneGO.transform.position;
            float distance = Vector3.Distance(userPos, stoneWorldPos);

            // Step 1: Performance Distance Guard
            if (distance > detectionRadius) continue;

            // Step 2: Screen Viewport Projection
            Vector3 viewportPos = mainCamera.WorldToViewportPoint(stoneWorldPos);

            // Verify stone is in front of camera lens (z > 0)
            if (viewportPos.z <= 0f) continue;

            // Step 3: Screen-Center Distance Offset
            Vector2 viewport2D = new Vector2(viewportPos.x, viewportPos.y);
            float offsetFromCenter = Vector2.Distance(viewport2D, screenCenter);

            // Filter out stones outside the central screen box
            if (offsetFromCenter > maxScreenCenterOffset) continue;

            // Step 4: Screen-Space Weighted Scoring
            float score = (offsetFromCenter * screenCenterWeight) + (distance * distanceWeight);

            if (score < bestScore)
            {
                bestScore = score;
                bestCandidateID = stoneID;
                bestWorldPos = stoneWorldPos;
            }
        }

        // Step 5: Real-Time Hysteresis Evaluation using Time.deltaTime
        EvaluateTargetHysteresis(bestCandidateID, bestScore, bestWorldPos);
    }

    private void EvaluateTargetHysteresis(string candidateID, float candidateScore, Vector3 candidateWorldPos)
    {
        if (string.IsNullOrEmpty(candidateID))
        {
            pendingTargetID = null;
            pendingTargetTimer = 0f;
            ClearDirectionalTarget();
            return;
        }

        if (candidateID == confirmedTargetID)
        {
            confirmedTargetScore = candidateScore;
            pendingTargetID = candidateID;
            pendingTargetTimer = 0f;
            UpdateArMarkerPosition(candidateWorldPos);
            return;
        }

        // Hysteresis score delta requirement to prevent rapid target oscillation
        if (confirmedTargetID != null && (confirmedTargetScore - candidateScore) < hysteresisScoreThreshold)
        {
            return;
        }

        if (candidateID == pendingTargetID)
        {
            pendingTargetTimer += scanInterval; // accumulate real interval time
            if (pendingTargetTimer >= hysteresisDuration)
            {
                ConfirmDirectionalTarget(candidateID, candidateScore, candidateWorldPos);
            }
        }
        else
        {
            pendingTargetID = candidateID;
            pendingTargetTimer = 0f;
        }
    }

    private void ConfirmDirectionalTarget(string stoneID, float score, Vector3 worldPos)
    {
        confirmedTargetID = stoneID;
        confirmedTargetScore = score;

        UpdateArMarkerPosition(worldPos);
        if (uiManager != null) uiManager.ShowCameraDetectionPrompt(stoneID, string.Empty);

        Debug.Log($"[DETECTOR] Target Confirmed: point_{stoneID} (WorldPos: {worldPos}, Score: {score:F2})");
    }

    private void UpdateArMarkerPosition(Vector3 worldPos)
    {
        if (activeArMarkerInstance != null)
        {
            activeArMarkerInstance.transform.position = worldPos;
            if (!activeArMarkerInstance.activeSelf) activeArMarkerInstance.SetActive(true);
        }
    }

    private void ClearDirectionalTarget()
    {
        if (confirmedTargetID != null)
        {
            Debug.Log($"[PHASE 1 DETECTOR] Target Cleared: (was point_{confirmedTargetID})");
            confirmedTargetID = null;
            confirmedTargetScore = float.MaxValue;

            if (activeArMarkerInstance != null)
            {
                activeArMarkerInstance.SetActive(false);
            }
            if (uiManager != null) uiManager.HideCameraDetectionPrompt();
        }
    }

    private void HandleTap(Vector2 screenPosition)
    {
        if (!allowEditorProxyRaycast || mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(screenPosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, raycastDistance))
        {
            MemorialObject memorialObject = hit.collider.GetComponentInParent<MemorialObject>();
            if (memorialObject != null)
            {
                SelectMemorial(memorialObject);
            }
        }
    }

    private void SelectMemorial(MemorialObject memorialObject)
    {
        if (lastSelectedMemorial != null && lastSelectedMemorial != memorialObject)
        {
            DeselectMemorial();
        }

        lastSelectedMemorial = memorialObject;
        string memorialID = memorialObject.GetID();

        Debug.Log($"[Interaction] Target memorial selected via Tap: {memorialID}");

        if (uiManager != null)
        {
            uiManager.ShowMemorialDetail(memorialID);
        }

        thesisManager?.OnMemorialSelected(memorialID);
    }

    private void DeselectMemorial()
    {
        if (lastSelectedMemorial != null)
        {
            Debug.Log($"[Interaction] Target memorial deselected: {lastSelectedMemorial.GetID()}");
            lastSelectedMemorial = null;
        }

        if (uiManager != null)
        {
            uiManager.HideMemorialDetail();
        }

        thesisManager?.OnMemorialDeselected();
    }

    public void SelectByID(string memorialID)
    {
        if (uiManager != null) uiManager.ShowMemorialDetail(memorialID);
    }

    public void ClearSelection() => DeselectMemorial();
    public MemorialObject GetSelectedMemorial() => lastSelectedMemorial;
}
