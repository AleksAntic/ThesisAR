using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum LocationSignalState { HighPrecisionVPS, StandardGPS, LowAccuracyGPS, SignalLost }

/// <summary>
/// Monitors Google ARCore Geospatial API VPS status and GPS horizontal accuracy to display
/// a real-time 4-state traffic-light badge (Green/Yellow/Orange/Red) in the top-right AR HUD.
/// </summary>
public class GPSQualityIndicatorUI : MonoBehaviour
{
    [Header("🎨 UI Badge Components")]
    [SerializeField] private Image badgeIconImage;
    [SerializeField] private Image badgeBackground;
    [SerializeField] private TextMeshProUGUI statusLabelText;
    [SerializeField] private Button badgeButton;

    [Header("🎨 State Colors")]
    [SerializeField] private Color greenColor = new Color(0.18f, 0.8f, 0.44f, 1.0f);   // High-Precision VPS (<1m)
    [SerializeField] private Color yellowColor = new Color(0.95f, 0.77f, 0.05f, 1.0f);  // GPS Standard (1-5m)
    [SerializeField] private Color orangeColor = new Color(0.9f, 0.49f, 0.13f, 1.0f);  // Low Accuracy (5-15m)
    [SerializeField] private Color redColor = new Color(0.91f, 0.3f, 0.24f, 1.0f);     // Signal Lost / GPS Off (>15m)

    [Header("🔗 Dependencies")]
    [SerializeField] private GeospatialManager geospatialManager;
    [SerializeField] private UIManager uiManager;

    private LocationSignalState currentState = LocationSignalState.SignalLost;
    private float updateTimer = 0f;
    private const float updateInterval = 0.5f;

    private void Awake()
    {
        if (badgeButton != null)
            badgeButton.onClick.AddListener(OnBadgeTapped);
    }

    private void Start()
    {
        if (geospatialManager == null)
            geospatialManager = FindAnyObjectByType<GeospatialManager>(FindObjectsInactive.Include);
        if (uiManager == null)
            uiManager = FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);

    }

    private void Update()
    {
        updateTimer += Time.deltaTime;
        if (updateTimer < updateInterval) return;
        updateTimer = 0f;

        EvaluateLocationQuality();
    }

    private void EvaluateLocationQuality()
    {
        if (!Input.location.isEnabledByUser)
        {
            SetState(LocationSignalState.SignalLost, "GPS Off", redColor);
            return;
        }

        float horizAccuracy = geospatialManager != null ? geospatialManager.CurrentHorizontalAccuracy : 999f;
        bool hasHighAccuracy = geospatialManager != null && geospatialManager.HasGoodAccuracy();

        if (hasHighAccuracy)
        {
            bool hasVpsCoverage = geospatialManager.CurrentVpsAvailability == Google.XR.ARCoreExtensions.VpsAvailability.Available;
            SetState(LocationSignalState.HighPrecisionVPS, hasVpsCoverage ? "VPS Coverage" : "High Accuracy", greenColor);
        }
        else if (horizAccuracy <= 5.0f)
        {
            SetState(LocationSignalState.StandardGPS, $"GPS ±{horizAccuracy:F0}m", yellowColor);
        }
        else if (horizAccuracy <= 15.0f)
        {
            SetState(LocationSignalState.LowAccuracyGPS, $"Low ±{horizAccuracy:F0}m", orangeColor);
        }
        else
        {
            SetState(LocationSignalState.SignalLost, "Signal Lost", redColor);
        }
    }

    private void SetState(LocationSignalState state, string label, Color color)
    {
        currentState = state;
        if (statusLabelText != null) statusLabelText.text = label;
        if (badgeIconImage != null) badgeIconImage.color = color;
        if (badgeBackground != null) badgeBackground.color = new Color(color.r, color.g, color.b, 0.25f);
    }

    public void OnBadgeTapped()
    {
        float accuracy = geospatialManager != null ? geospatialManager.CurrentHorizontalAccuracy : -1f;
        string title = currentState == LocationSignalState.HighPrecisionVPS ? "High-precision location" :
                      currentState == LocationSignalState.StandardGPS ? "Standard GPS" :
                      currentState == LocationSignalState.LowAccuracyGPS ? "Reduced location accuracy" : "Location unavailable";

        string message = $"Current horizontal accuracy: +/-{accuracy:F1} m. Enable location services and keep the camera on visible ground or landmarks.";

        if (uiManager != null)
        {
            uiManager.ShowNotificationToast(title, message);
        }
        else
        {
            Debug.Log($"[GPS Quality Toast] {title}: {message}");
        }
    }

}
