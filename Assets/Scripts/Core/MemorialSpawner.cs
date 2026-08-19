using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scans pre-existing nodes inside the georeferenced GLB model and binds structural 
/// metadata from MemorialDataManager to them, injecting interaction logic.
/// </summary>
public class MemorialSpawner : MonoBehaviour
{
    [SerializeField] private MemorialDataManager dataManager;
    [SerializeField] private Transform glbRootObject; // Drag the loaded GLB model hierarchy root here

    [Header("Optional Interaction Overlays")]
    [SerializeField] private GameObject interactionPrefabOverlay; // E.g., visual bounding box, indicator arrows

    private Dictionary<string, GameObject> boundMemorialNodes = new Dictionary<string, GameObject>();

    // Cache per azzerare la ricerca ricorsiva ad ogni ciclo
    private Dictionary<string, Transform> glbHierarchyCache = new Dictionary<string, Transform>(System.StringComparer.OrdinalIgnoreCase);

    void Start()
    {
        if (dataManager == null) dataManager = Object.FindAnyObjectByType<MemorialDataManager>(FindObjectsInactive.Include);

        if (dataManager == null)
        {
            Debug.LogError("[MemorialLinker] MemorialDataManager dependency missing!");
            return;
        }

        if (!dataManager.IsLoaded) dataManager.LoadData();

        if (glbRootObject == null)
        {
            Debug.LogWarning("[MemorialLinker] GLB Root Object not assigned. Binding will wait until manually triggered.");
            return;
        }

        BindDatabaseToGlbNodes();
    }

    /// <summary>
    /// Traverses the GLB hierarchy and maps matching marker names to JSON records.
    /// </summary>
    [ContextMenu("Bind Database To GLB Markers")]
    public void BindDatabaseToGlbNodes()
    {
        if (glbRootObject == null || dataManager == null || !dataManager.IsLoaded) return;

        boundMemorialNodes.Clear();
        glbHierarchyCache.Clear();

        // 1. Genera la cache dell'intera gerarchia GLB in un unico passaggio lineare
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        BuildHierarchyCache(glbRootObject);

        // 2. Bind Memorial Stones (Lookup O(1) ultra-veloce)
        foreach (var stone in dataManager.GetAllMemorialStones())
        {
            TryLinkNode(stone.id, stone);
        }

        // Bind Mass Graves
        foreach (var grave in dataManager.GetAllMassGraves())
        {
            TryLinkNode(grave.id, grave);
        }

        // Bind Other Memorials
        foreach (var memorial in dataManager.GetAllOtherMemorials())
        {
            TryLinkNode(memorial.id, memorial);
        }

        sw.Stop();
        Debug.Log($"[MemorialLinker] Link process complete in {sw.ElapsedMilliseconds} ms. Successfully bound {boundMemorialNodes.Count} GLB markers.");
    }

    /// <summary>
    /// Popola ricorsivamente il dizionario Flat della gerarchia per evitare ricerche lineari successive.
    /// </summary>
    private void BuildHierarchyCache(Transform current)
    {
        if (current == null) return;

        // Registra il nodo usando il nome come chiave (gestisce eventuali duplicati ignorandoli o sovrascrivendo)
        if (!glbHierarchyCache.ContainsKey(current.name))
        {
            glbHierarchyCache[current.name] = current;
        }

        for (int i = 0; i < current.childCount; i++)
        {
            BuildHierarchyCache(current.GetChild(i));
        }
    }

    private void TryLinkNode(string id, object dataContext)
    {
        // Cerca direttamente nella cache O(1) invece di fare la ricerca ricorsiva nell'albero
        Transform markerTransform = null;

        if (!glbHierarchyCache.TryGetValue(id, out markerTransform))
        {
            // Fallback se ci sono prefissi
            if (!glbHierarchyCache.TryGetValue($"Stone_{id}", out markerTransform))
            {
                if (!glbHierarchyCache.TryGetValue($"Grave_{id}", out markerTransform))
                {
                    // 🚨 IL FIX CRITICO: Cerca il nodo usando il prefisso nativo del GLB "point_"
                    glbHierarchyCache.TryGetValue($"point_{id}", out markerTransform);
                }
            }
        }

        // Global Scene Fallback: if hierarchy cache search failed, search the entire active/inactive scene structure
        if (markerTransform == null)
        {
            GameObject targetGo = GameObject.Find(id);
            if (targetGo == null) targetGo = GameObject.Find($"point_{id}");
            if (targetGo == null) targetGo = GameObject.Find($"Stone_{id}");
            if (targetGo == null) targetGo = GameObject.Find($"Grave_{id}");

            // Look up inactive objects if Find failed
            if (targetGo == null)
            {
                var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
                foreach (var tr in allTransforms)
                {
                    if (tr != null && (
                        tr.name.Equals(id, System.StringComparison.OrdinalIgnoreCase) ||
                        tr.name.Equals($"point_{id}", System.StringComparison.OrdinalIgnoreCase) ||
                        tr.name.Equals($"Stone_{id}", System.StringComparison.OrdinalIgnoreCase) ||
                        tr.name.Equals($"Grave_{id}", System.StringComparison.OrdinalIgnoreCase)))
                    {
                        targetGo = tr.gameObject;
                        break;
                    }
                }
            }

            if (targetGo != null)
            {
                markerTransform = targetGo.transform;
                Debug.Log($"[MemorialSpawner] Global Fallback matched marker '{id}' outside GLB root at position {markerTransform.position}");
            }
        }

        if (markerTransform != null)
        {
            GameObject targetObj = markerTransform.gameObject;

            // Ensure the node has the interaction reference wrapper component attached
            var memorialComponent = targetObj.GetComponent<MemorialObject>() ?? targetObj.AddComponent<MemorialObject>();
            memorialComponent.SetData(dataContext, id);

            // Ensure a collider exists on the GLB element so the interaction raycast can hit it
            if (targetObj.GetComponent<Collider>() == null)
            {
                targetObj.AddComponent<BoxCollider>();
            }

            // Optional: If you want to instantiate an overlay effect
            if (interactionPrefabOverlay != null)
            {
                GameObject overlay = Instantiate(interactionPrefabOverlay, targetObj.transform);
                overlay.transform.localPosition = Vector3.zero;
                overlay.transform.localRotation = Quaternion.identity;
            }

            boundMemorialNodes[id] = targetObj;
        }
        else
        {
            Debug.LogWarning($"[MemorialLinker] Marker identifier '{id}' (or point_{id}) could not be located inside the current GLB geometry hierarchy.");
        }
    }

    public GameObject GetSpawnedMemorial(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (boundMemorialNodes.TryGetValue(id, out var obj)) return obj;

        GameObject fallback = GameObject.Find("point_" + id) ??
                              GameObject.Find(id) ??
                              GameObject.Find("Stone_" + id) ??
                              GameObject.Find("Grave_" + id);
        if (fallback != null) boundMemorialNodes[id] = fallback;
        return fallback;
    }
    public IReadOnlyDictionary<string, GameObject> GetAllSpawnedMemorials() => boundMemorialNodes;
}
