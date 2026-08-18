using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class SimulationAutopilot : MonoBehaviour
{
    [Header("⚙️ Core Systems Injection")]
    [SerializeField] private RouteManager routeManager;
    [SerializeField] private ARWayfindingManager arWayfindingManager;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private LineRenderer routeLineRenderer;

    [Header("🚶 Movement Settings")]
    [SerializeField] private float walkSpeed = 5.0f; // Velocità aumentata per velocizzare i tuoi test a PC
    [SerializeField] private float waypointThreshold = 0.5f;

    [Header("👁️ Camera Attachment (Editor Only)")]
    [SerializeField] private Transform cameraRigToAttach;

    [Header("📋 UI Text Fields Injection")]
    [SerializeField] private TextMeshProUGUI walkingTowardsText;
    [SerializeField] private TextMeshProUGUI distanceText;

    private List<Vector3> currentPathPoints = new List<Vector3>();
    private int targetPointIndex = 0;
    private bool isAutopilotActive = false;
    private string targetStoneID = "";

    void Start()
    {
        if (routeManager == null) routeManager = UnityEngine.Object.FindAnyObjectByType<RouteManager>(FindObjectsInactive.Include);
        if (arWayfindingManager == null) arWayfindingManager = UnityEngine.Object.FindAnyObjectByType<ARWayfindingManager>(FindObjectsInactive.Include);
        if (uiManager == null) uiManager = UnityEngine.Object.FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);

        if (cameraRigToAttach == null && Camera.main != null)
        {
            Transform mainCamTransform = Camera.main.transform;
            if (mainCamTransform.parent != null)
            {
                cameraRigToAttach = mainCamTransform.parent.parent != null ? mainCamTransform.parent.parent : mainCamTransform.parent;
            }
            else
            {
                cameraRigToAttach = mainCamTransform;
            }
            Debug.Log($"[Autopilot] Auto-assigned cameraRigToAttach to: {cameraRigToAttach.name}");
        }
    }

    void Update()
    {
        if (isAutopilotActive)
        {
            ExecuteAutopilotMovement();
            UpdateLiveWayfindingUI();
        }
    }

    public void StartAutopilot(string targetID)
    {
        Debug.Log("[Autopilot] Autopilot is disabled to preserve user manual control. Moving manually.");
        isAutopilotActive = false;
    }

    public void StopAutopilot()
    {
        isAutopilotActive = false;
        if (cameraRigToAttach != null) cameraRigToAttach.SetParent(null);
        if (arWayfindingManager != null) arWayfindingManager.StopNavigation();
    }

    private void ExecuteAutopilotMovement()
    {
        if (targetPointIndex >= currentPathPoints.Count)
        {
            StopAutopilot();
            if (uiManager != null) uiManager.ShowMemorialDetail(targetStoneID);
            return;
        }

        Vector3 targetTarget = currentPathPoints[targetPointIndex];
        transform.position = Vector3.MoveTowards(transform.position, targetTarget, walkSpeed * Time.deltaTime);

        Vector3 direction = (targetTarget - transform.position).normalized;
        if (direction != Vector3.zero)
        {
            direction.y = 0;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 5f * Time.deltaTime);
        }

        if (Vector3.Distance(transform.position, targetTarget) <= waypointThreshold)
        {
            targetPointIndex++;
        }
    }

    private void UpdateLiveWayfindingUI()
    {
        if (currentPathPoints.Count == 0 || targetPointIndex >= currentPathPoints.Count) return;

        float remainingDistance = Vector3.Distance(transform.position, currentPathPoints[targetPointIndex]);
        for (int i = targetPointIndex; i < currentPathPoints.Count - 1; i++)
        {
            remainingDistance += Vector3.Distance(currentPathPoints[i], currentPathPoints[i + 1]);
        }

        if (walkingTowardsText != null) walkingTowardsText.text = $"Walking towards: <b>{targetStoneID}</b>";
        if (distanceText != null) distanceText.text = $"Distance: <b>{remainingDistance:F1} m</b>";
    }
}
