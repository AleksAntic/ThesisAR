using UnityEngine;

/// <summary>
/// Component attached to GLB marker nodes to bridge the 3D geometry with its historical JSON data context.
/// </summary>
public class MemorialObject : MonoBehaviour
{
    [SerializeField] private DistanceCullingBillboard billboardCulling;
    [Header("Debug Inspector Values")]
    [SerializeField] private string memorialId;

    private object dataContext;

    void Awake()
    {
        if (billboardCulling == null)
            billboardCulling = GetComponentInChildren<DistanceCullingBillboard>();
    }

    public void SetData(object memorialData, string memorialID)
    {
        dataContext = memorialData;
        memorialId = memorialID;
    }

    public object GetData() => dataContext;
    public string GetID() => memorialId;

    public void ResetVisuals()
    {
        if (billboardCulling != null)
            billboardCulling.ResetState();
    }
}