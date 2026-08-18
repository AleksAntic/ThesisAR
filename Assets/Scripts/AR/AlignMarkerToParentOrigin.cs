using UnityEngine;

/// <summary>
/// Attach this to the imported GLB root object.
/// It moves the GLB root so that a nested marker Transform matches this object's parent origin.
/// The source GLB asset is not modified.
/// </summary>
public class AlignMarkerToParentOrigin : MonoBehaviour
{
    private const string DefaultMarkerName = "point_B-B Geolocation origin";

    [SerializeField] private Transform markerOrigin;
    [SerializeField] private bool includeY = true;
    [SerializeField] private bool autoFindMarkerOnValidate = true;

    void OnValidate()
    {
        if (!autoFindMarkerOnValidate)
            return;

        TryAutoAssignMarker();
    }

    [ContextMenu("Auto-Assign Default Marker")]
    public bool AutoAssignDefaultMarker()
    {
        bool found = TryAutoAssignMarker();
        if (!found)
            Debug.LogWarning($"[Aligner] Marker '{DefaultMarkerName}' not found under '{name}'.", this);
        return found;
    }

    [ContextMenu("Align Marker To Parent Origin")]
    public void AlignNow()
    {
        if (markerOrigin == null && !TryAutoAssignMarker())
        {
            Debug.LogWarning($"[Aligner] Marker not assigned and '{DefaultMarkerName}' not found.", this);
            return;
        }

        if (transform.parent == null)
        {
            Debug.LogWarning("[Aligner] This object must have a parent (e.g., GeoRoot or MapRoot) to align to.", this);
            return;
        }

        Vector3 desiredWorld = transform.parent.position;
        Vector3 currentWorld = markerOrigin.position;
        Vector3 delta = desiredWorld - currentWorld;

        if (!includeY)
            delta.y = 0f;

        transform.position += delta;
        Debug.Log($"[Aligner] Aligned '{markerOrigin.name}' to parent origin. Delta vector applied: {delta}", this);
    }

    private bool TryAutoAssignMarker()
    {
        Transform found = FindChildRecursive(transform, DefaultMarkerName);
        if (found == null)
            return false;

        markerOrigin = found;
        return true;
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
            return null;

        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildRecursive(root.GetChild(i), childName);
            if (result != null)
                return result;
        }

        return null;
    }
}