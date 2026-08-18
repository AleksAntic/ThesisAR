using UnityEngine;

/// <summary>
/// Abstract base class defining structural contracts for all experimental thesis guidance conditions
/// (Impersonal, Intermediate, Personal). Manages baseline system reference injections.
/// </summary>
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

        if (hasBeenInitializedOnce)
        {
            Debug.Log($"[{GetType().Name}] Initialize() re-invoked on already initialized instance - skipping duplicate OnInitialize().");
            return;
        }

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
        if (UnityEngine.AI.NavMesh.SamplePosition(rayOrigin, out UnityEngine.AI.NavMeshHit navHit, 4.0f, UnityEngine.AI.NavMesh.AllAreas))
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