using UnityEngine;

/// <summary>
/// Remote Field Testing Zone component (e.g. Local Soccer Pitch or Park).
/// Allows researchers and developers to simulate and test AR location triggers 
/// and geospatial navigation on a local field away from the physical Bergen-Belsen site.
/// </summary>
public class SoccerFieldZone : MonoBehaviour
{
    [Header("⚽ Remote Field Testing Configuration")]
    public string zoneName = "SDU Soccer Field";

    [Tooltip("Enable to remap site coordinates onto a local physical testing field.")]
    [SerializeField] private bool enableRemoteFieldSimulation = true;

    [Tooltip("Latitude of local testing origin (e.g., local soccer field center).")]
    [SerializeField] private double localFieldLatitude = 52.757620;

    [Tooltip("Longitude of local testing origin.")]
    [SerializeField] private double localFieldLongitude = 9.912300;

    [Header("📐 Field Dimensions")]
    [SerializeField] private float fieldWidthMeters = 68f;
    [SerializeField] private float fieldLengthMeters = 105f;

    public bool IsRemoteFieldSimulationActive => enableRemoteFieldSimulation;
    public double LocalLatitude => localFieldLatitude;
    public double LocalLongitude => localFieldLongitude;

    private void Awake()
    {
        if (enableRemoteFieldSimulation)
        {
            Debug.Log($"[SoccerFieldZone] Remote field testing simulation '{zoneName}' ACTIVE at Lat: {localFieldLatitude}, Lon: {localFieldLongitude}");
        }
    }

    public bool IsPositionInsideTestZone(Vector3 localPosition)
    {
        float halfLength = fieldLengthMeters * 0.5f;
        float halfWidth = fieldWidthMeters * 0.5f;
        return Mathf.Abs(localPosition.x) <= halfWidth && Mathf.Abs(localPosition.z) <= halfLength;
    }

    public void UpdateBoundsVisualization(bool isActive)
    {
        gameObject.SetActive(isActive);
    }

    public void ForceSnapAndDraw()
    {
        // Visual update call for Editor / Scene view drawing
    }
}
