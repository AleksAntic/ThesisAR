using UnityEngine;

/// <summary>
/// Scans the physical scene hierarchy for structural coordinate anchors starting with 'point_',
/// dynamically attaches physics wrappers, and maps them safely to the 2D layout engine.
/// All internal code, variables, and logs are strictly maintained in English.
/// </summary>
public class SetupEmbeddedMarkers : MonoBehaviour
{
    [Header("🌐 Core UI References")]
    [SerializeField] private UIManager uiManager;

    [Header("🎨 Map Marker Appearance")]
    [SerializeField] private string markerLayerName = "MapMarkers";

    [Header("🛑 Local Z-Axis Height Lift")]
    [SerializeField] private float globalZLift = -5.0f;

    void Start()
    {
        if (uiManager == null) uiManager = UnityEngine.Object.FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
        ConfigurePhysicalSceneNodes();
    }

    [ContextMenu("🔧 Prepare Embedded Markers")]
    public void ConfigurePhysicalSceneNodes()
    {
        int layerIndex = LayerMask.NameToLayer(markerLayerName);
        if (layerIndex == -1) layerIndex = 0;

        int totalCounter = 0;

        // Scan the entire active scene hierarchy to locate physical anchor parents starting with 'point_'
        GameObject[] allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);

        foreach (GameObject obj in allObjects)
        {
            if (obj != null && obj.name.StartsWith("point_"))
            {
                string markerID = obj.name.Replace("point_", "").Trim();

                // Skip non-memorial test/origin anchor points (e.g. point_0, point_1, point_2, point_3, origin, anchor, test)
                if (markerID == "0" || markerID == "1" || markerID == "2" || markerID == "3" ||
                    markerID.ToLower().Contains("origin") || markerID.ToLower().Contains("anchor") ||
                    markerID.ToLower().Contains("test") || markerID.ToLower().Contains("setup"))
                {
                    continue;
                }

                // Locate the nested 'Geom3D' node inside the GLB sub-hierarchy if present, otherwise default to root anchor
                Transform targetGeom = obj.transform.Find("Geom3D");
                if (targetGeom == null)
                {
                    targetGeom = obj.transform;
                }

                targetGeom.gameObject.layer = layerIndex;

                // SOURCE OF TRUTH FIX: Avoid using the C# null-coalescing operator (??) on Unity Objects.
                // Unity objects override standard equality operators (==, !=) to perform native lifetime checks,
                // whereas (??) bypasses this and checks only for C# null, causing MissingComponentExceptions on GLB meshes.
                InteractiveMapPin pinLogic = targetGeom.GetComponent<InteractiveMapPin>();
                if (pinLogic == null)
                {
                    pinLogic = targetGeom.gameObject.AddComponent<InteractiveMapPin>();
                }

                pinLogic.ConfigPin(markerID, uiManager, globalZLift);

                BoxCollider boxCollider = targetGeom.GetComponent<BoxCollider>();
                if (boxCollider == null)
                {
                    boxCollider = targetGeom.gameObject.AddComponent<BoxCollider>();
                }

                // If it is an identified Mass Grave node, expand its interaction bounds to assist mobile touch ergonomics
                if (markerID.StartsWith("MG", System.StringComparison.OrdinalIgnoreCase))
                {
                    boxCollider.size = new Vector3(2f, 2f, 2f);
                }
                else
                {
                    boxCollider.size = Vector3.one;
                }
                boxCollider.center = Vector3.zero;

                totalCounter++;
            }
        }
        Debug.Log($"<color=green>[✔ SCENE INITIALIZATION COMPLETE]</color> Configured {totalCounter} physical scene nodes as verified layout pins.");
    }
}
