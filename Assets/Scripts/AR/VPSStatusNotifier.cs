using UnityEngine;
using System.Collections;

/// <summary>
/// Monitors ARCore Geospatial positioning accuracy and triggers non-blocking UI notifications
/// when tracking quality drops or recovers. Runs lightweight polling with a hysteresis cooldown.
/// Completely read-only and decoupled from navigation/narration domain logic.
/// </summary>
public class VPSStatusNotifier : MonoBehaviour
{
    [Header("⚙️ Dependencies")]
    [SerializeField] private GeospatialManager geospatialManager;
    [SerializeField] private UIManager uiManager;

    [Header("⏱️ Cooldown & Thresholds")]
    [SerializeField] private float checkIntervalSeconds = 2.0f;
    [SerializeField] private float notificationCooldownSeconds = 15.0f;

    public enum AccuracyState
    {
        HighPrecision,
        LowPrecision,
        NoSignal
    }

    private AccuracyState currentState = AccuracyState.NoSignal;
    private float cooldownTimer = 15f;

    void Start()
    {
        if (geospatialManager == null) geospatialManager = UnityEngine.Object.FindAnyObjectByType<GeospatialManager>(FindObjectsInactive.Include);
        if (uiManager == null) uiManager = UnityEngine.Object.FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);

        StartCoroutine(StatusCheckLoop());
    }

    private IEnumerator StatusCheckLoop()
    {
        var wait = new WaitForSeconds(checkIntervalSeconds);

        while (true)
        {
            yield return wait;

            if (geospatialManager == null || uiManager == null) continue;

            cooldownTimer += checkIntervalSeconds;
            AccuracyState newState = EvaluateAccuracyState();

            if (newState != currentState && cooldownTimer >= notificationCooldownSeconds)
            {
                currentState = newState;
                cooldownTimer = 0f;
                TriggerNotificationForState(currentState);
            }
        }
    }

    private AccuracyState EvaluateAccuracyState()
    {
        if (!geospatialManager.IsGeospatialReady)
        {
            return AccuracyState.NoSignal;
        }

        if (geospatialManager.HasGoodAccuracy())
        {
            return AccuracyState.HighPrecision;
        }

        return AccuracyState.LowPrecision;
    }

    private void TriggerNotificationForState(AccuracyState state)
    {
        string lang = (uiManager != null) ? uiManager.SelectedLanguage : "english";
        bool isGerman = lang == "german";

        switch (state)
        {
            case AccuracyState.LowPrecision:
                string lowTitle = isGerman ? "GPS Genauigkeit" : "GPS Accuracy";
                string lowMsg = isGerman 
                    ? "GPS aktiv. Reduzierte Genauigkeit — Navigation läuft normal weiter."
                    : "GPS active. Reduced visual precision due to environment — Navigation continues normally.";
                if (uiManager != null) uiManager.ShowNotificationToast(lowTitle, lowMsg);
                else Debug.Log($"[GPS Status] {lowTitle}: {lowMsg}");
                break;

            case AccuracyState.NoSignal:
                string noSignalTitle = isGerman ? "Geospatiales Signal" : "Geospatial Signal";
                string noSignalMsg = isGerman
                    ? "Geospatiales Signal verloren. Bitte gehen Sie in eine offene Fläche."
                    : "Geospatial signal lost. Please move to an open area and point camera around.";
                if (uiManager != null) uiManager.ShowNotificationToast(noSignalTitle, noSignalMsg);
                else Debug.Log($"[GPS Status] {noSignalTitle}: {noSignalMsg}");
                break;

            case AccuracyState.HighPrecision:
                // Silent recovery — no notification needed
                break;
        }
    }
}
