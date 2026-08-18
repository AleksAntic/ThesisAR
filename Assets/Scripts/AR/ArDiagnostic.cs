using UnityEngine;
using TMPro;

/// <summary>
/// Monitors and displays ARCore Extensions VPS status tracking variables split across a professional two-column UI layout.
/// </summary>
public class ArDiagnostic : MonoBehaviour
{
    [SerializeField] private GeospatialManager geospatialManager;

    [Header("📊 UI Column Outputs")]
    [SerializeField] private TextMeshProUGUI leftColumnText;
    [SerializeField] private TextMeshProUGUI rightColumnText;

    void Update()
    {
        if (geospatialManager == null) return;

        string trackingState = geospatialManager.EarthTrackingState.ToString();
        string status = geospatialManager.IsGeospatialReady ? "READY" : "LOCATING";

        float posAccuracy = geospatialManager.CurrentHorizontalAccuracy;
        float headingAccuracy = geospatialManager.CurrentHeadingAccuracy;
        float headingDegrees = geospatialManager.CurrentHeading;

        // Column 1: Core System Tracking Flags
        if (leftColumnText != null)
        {
            leftColumnText.text = $"<b>VPS SYSTEM CORE</b>\n" +
                                 $"Tracking State: {trackingState}\n" +
                                 $"VPS Status: <color=yellow>{status}</color>\n" +
                                 $"System Mode: {(geospatialManager.HasGoodAccuracy() ? "<color=green>HIGH-PRECISION</color>" : "<color=red>CALIBRATING</color>")}";
        }

        // Column 2: Mathematical Accuracy Metrics
        if (rightColumnText != null)
        {
            rightColumnText.text = $"<b>ACCURACY METRICS</b>\n" +
                                  $"Position Error: {posAccuracy:F2} m\n" +
                                  $"Compass Error: {headingAccuracy:F1}°\n" +
                                  $"Current Heading: {headingDegrees:F1}°";
        }
    }
}