using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simulates a "Phone-in-Hand" AR view in the PC Unity Editor.
/// Keeps AR olograms and virtual marks hidden in the main view (No-Scope),
/// and displays them inside a screen-space Phone screen frame on Left Mouse Click (Scope).
/// </summary>
public class ARPhoneSimulator : MonoBehaviour
{
    [Header("📷 Camera References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Camera arPhoneCamera;

    [Header("📱 UI Phone Visuals")]
    [SerializeField] private GameObject phoneUIFrame;
    [SerializeField] private CanvasGroup phoneCanvasGroup;

    [Header("⚙️ Culling Configurations")]
    [SerializeField] private string arLayerName = "MapMarkers"; // standard layer for virtual markers
    [SerializeField] private float slideSpeed = 8f;

    private int arLayerIndex;
    private bool isPhoneActive = false;
    private Vector2 phoneHiddenPos;
    private Vector2 phoneActivePos;
    private RectTransform phoneRectTransform;

    void Start()
    {
        // Fallback checks
        if (mainCamera == null) mainCamera = Camera.main;
        
        arLayerIndex = LayerMask.NameToLayer(arLayerName);
        if (arLayerIndex == -1)
        {
            arLayerIndex = 10; // Fallback to layer 10 if not defined
            Debug.LogWarning($"[AR Simulator] Layer '{arLayerName}' not found. Defaulting to index {arLayerIndex}.");
        }

        // Configure main camera culling to exclude AR elements by default (No-Scope)
        if (mainCamera != null)
        {
            mainCamera.cullingMask &= ~(1 << arLayerIndex);
        }

        // Configure the secondary AR camera to render the olograms/markers
        if (arPhoneCamera != null)
        {
            arPhoneCamera.cullingMask |= (1 << arLayerIndex);
            arPhoneCamera.enabled = false;
        }

        if (phoneUIFrame != null)
        {
            phoneRectTransform = phoneUIFrame.GetComponent<RectTransform>();
            if (phoneRectTransform != null)
            {
                // Slide coordinates: bottom of screen to center
                phoneActivePos = Vector2.zero;
                phoneHiddenPos = new Vector2(0f, -Screen.height * 1.2f);
                phoneRectTransform.anchoredPosition = phoneHiddenPos;
            }

            if (phoneCanvasGroup == null)
            {
                phoneCanvasGroup = phoneUIFrame.GetComponent<CanvasGroup>();
                if (phoneCanvasGroup == null) phoneCanvasGroup = phoneUIFrame.AddComponent<CanvasGroup>();
            }
            phoneCanvasGroup.alpha = 0f;
            phoneUIFrame.SetActive(true); // Keep active for script processing
        }
    }

    void Update()
    {
        // Activate "Scope Mode" (AR Phone View) when holding Right Mouse Button (RMB)
        // Ensure we are in Editor or Standalone and not clicking on other UI buttons
        bool inputHeld = Input.GetMouseButton(1) && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        if (inputHeld)
        {
            isPhoneActive = true;
            if (arPhoneCamera != null) arPhoneCamera.enabled = true;
        }
        else
        {
            isPhoneActive = false;
        }

        // Smoothly animate the Phone UI Frame sliding and fading in
        if (phoneRectTransform != null && phoneCanvasGroup != null)
        {
            Vector2 targetPos = isPhoneActive ? phoneActivePos : phoneHiddenPos;
            float targetAlpha = isPhoneActive ? 1f : 0f;

            phoneRectTransform.anchoredPosition = Vector2.Lerp(phoneRectTransform.anchoredPosition, targetPos, Time.deltaTime * slideSpeed);
            phoneCanvasGroup.alpha = Mathf.Lerp(phoneCanvasGroup.alpha, targetAlpha, Time.deltaTime * slideSpeed);

            // Disable the AR camera when the phone completely slides off-screen to save draw calls
            if (!isPhoneActive && phoneCanvasGroup.alpha < 0.05f && arPhoneCamera != null)
            {
                arPhoneCamera.enabled = false;
            }
        }
    }
}
