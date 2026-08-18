using UnityEngine;

/// <summary>
/// Attached to individual interactive map pins. Handles selection triggers, 
/// dynamic map scaling compensation, and visual state modifications.
/// </summary>
public class InteractiveMapPin : MonoBehaviour
{
    private string targetStoneID;
    private UIManager ui;
    private Color originalColor = Color.red;
    private MeshRenderer meshRenderer;

    private Vector3 localMeshOffset;
    private bool hasMeshOffset = false;
    private float localZLift = 0f;

    void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null && meshRenderer.material != null)
        {
            originalColor = meshRenderer.material.color;
        }

        MeshFilter filter = GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh != null)
        {
            // Caches the native vertex offset of the loaded GLB model geometry
            localMeshOffset = filter.sharedMesh.bounds.center;
            hasMeshOffset = true;
        }
    }

    public void ConfigPin(string id, UIManager uiManager, float zLift)
    {
        targetStoneID = id;
        this.ui = uiManager;
        this.localZLift = zLift;
    }

    /// <summary>
    /// Adjusts local scale and counter-aligns position offsets to lock the marker in place during map zooms.
    /// </summary>
    public void SetDynamicScale(float scale)
    {
        transform.localScale = new Vector3(scale, scale, scale);

        if (hasMeshOffset)
        {
            // Multiplies the cached offset by current scale, visually canceling out displacement artifacts during zoom operations
            transform.localPosition = (-localMeshOffset * scale) + new Vector3(0f, 0f, localZLift);
        }
        else
        {
            transform.localPosition = new Vector3(0f, 0f, localZLift);
        }
    }

    public Vector3 GetVisualCenter()
    {
        if (meshRenderer != null)
        {
            return meshRenderer.bounds.center;
        }
        return transform.position;
    }

    public void TriggerSelection()
    {
        if (ui == null) ui = UnityEngine.Object.FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
        if (ui == null) return;

        RouteManager routeMgr = UnityEngine.Object.FindAnyObjectByType<RouteManager>(FindObjectsInactive.Include);

        if (routeMgr != null && routeMgr.IsInModalitaPercorso())
        {
            routeMgr.GestisciTappa(targetStoneID, this);
        }
        else
        {
            ui.OpenMapMiniPopup(targetStoneID, this.transform);
        }
    }

    public void SetMarkerColor(Color color)
    {
        if (meshRenderer != null && meshRenderer.material != null)
        {
            meshRenderer.material.color = color;
        }
    }

    public void SetOriginalColor(Color color)
    {
        originalColor = color;
        SetMarkerColor(color);
    }

    public void ResetToOriginalColor()
    {
        SetMarkerColor(originalColor);
    }
}