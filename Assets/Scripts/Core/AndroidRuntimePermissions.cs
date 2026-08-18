using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

/// <summary>
/// Handles explicit Android 6.0+ runtime permission requests for Camera (ARCore)
/// and Fine Location (Geospatial GPS), preventing black screen or silent tracking failures on real devices.
/// </summary>
public class AndroidRuntimePermissions : MonoBehaviour
{
    void Awake()
    {
        RequestAllRequiredPermissions();
    }

    void Start()
    {
        StartCoroutine(VerifyPermissionsAfterDelay());
    }

    public static void RequestAllRequiredPermissions()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Debug.Log("[AndroidPermissions] Requesting Camera permission for ARCore...");
            Permission.RequestUserPermission(Permission.Camera);
        }

        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            Debug.Log("[AndroidPermissions] Requesting Fine Location permission for Geospatial VPS...");
            Permission.RequestUserPermission(Permission.FineLocation);
        }

        if (!Permission.HasUserAuthorizedPermission("android.permission.ACCESS_COARSE_LOCATION"))
        {
            Permission.RequestUserPermission("android.permission.ACCESS_COARSE_LOCATION");
        }
#endif
    }

    private System.Collections.IEnumerator VerifyPermissionsAfterDelay()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        yield return new WaitForSeconds(2f);

        bool cameraOk = Permission.HasUserAuthorizedPermission(Permission.Camera);
        bool locationOk = Permission.HasUserAuthorizedPermission(Permission.FineLocation);

        if (!cameraOk || !locationOk)
        {
            string missing = "";
            if (!cameraOk) missing = "Camera";
            if (!locationOk) missing += (missing.Length > 0 ? " & " : "") + "Location";

            Debug.LogWarning($"[AndroidPermissions] Missing permissions after request: {missing}");

            var uiManager = Object.FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
            if (uiManager != null)
            {
                uiManager.ShowNotificationToast("Permissions Required",
                    $"{missing} permission(s) denied. AR features will not work correctly. Please grant permissions in device Settings.");
            }
        }
#else
        yield break;
#endif
    }
}
