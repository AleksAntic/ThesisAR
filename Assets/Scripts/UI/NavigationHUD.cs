using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Monitors active pathfinding layouts from RouteManager to drive the on-screen 
/// navigation heads-up display (HUD), printing target coordinates and remaining distance indicators.
/// All variables, comments, and structures are strictly maintained in English.
/// </summary>
[ExecuteAlways]
public class NavigationHUD : MonoBehaviour
{
    [Header("📝 UI Text Component Interfaces")]
    [SerializeField] private TMP_Text targetStoneText;       // Text component for "Walking towards: Stone X"
    [SerializeField] private TMP_Text distanceIndicatorText; // Text component for "Distance: X meters"

    [Header("🔗 Core Engine Dependencies")]
    private RouteManager routeManager;
    private Transform userTransform;
    private TourManager tourManager;
    private MemorialSpawner memorialSpawner;
    private MemorialDataManager memorialDataManager;
    [SerializeField] private Image directionIndicator;

    private string lastTargetId = null;
    private float lastDistance = -1f;

    private void OnEnable()
    {
        EnsureDirectionIndicator();
    }

    private void Awake()
    {
        if (routeManager == null) routeManager = UnityEngine.Object.FindAnyObjectByType<RouteManager>(FindObjectsInactive.Include);
        tourManager = UnityEngine.Object.FindAnyObjectByType<TourManager>(FindObjectsInactive.Include);
        memorialSpawner = UnityEngine.Object.FindAnyObjectByType<MemorialSpawner>(FindObjectsInactive.Include);
        memorialDataManager = UnityEngine.Object.FindAnyObjectByType<MemorialDataManager>(FindObjectsInactive.Include);
        EnsureDirectionIndicator();
    }

    void Start()
    {
        if (routeManager == null) routeManager = UnityEngine.Object.FindAnyObjectByType<RouteManager>(FindObjectsInactive.Include);
        if (memorialDataManager == null) memorialDataManager = UnityEngine.Object.FindAnyObjectByType<MemorialDataManager>(FindObjectsInactive.Include);
        EnsureDirectionIndicator();
        
        // Hide UI elements initially if no active tracking route is running (only in Play mode)
        if (Application.isPlaying)
        {
            ToggleHudVisibility(false);
        }
    }

    void Update()
    {
        if (routeManager == null) return;

        // Dynamically resolve user transform if null, ensuring robustness if Camera.main is initialized late
        if (userTransform == null)
        {
            GameObject simPlayer = GameObject.Find("Simulated_GPS_Player");
            if (simPlayer != null)
            {
                userTransform = simPlayer.transform;
            }
            else if (Camera.main != null)
            {
                userTransform = Camera.main.transform;
            }

            // Programmatically fix short Far Clipping Plane (e.g. 15m limit) to see distant stones/landscape
            if (Camera.main != null && Camera.main.farClipPlane < 300f)
            {
                Camera.main.farClipPlane = 1000f;
                Debug.Log($"[NavigationHUD] Adjusted Camera.main farClipPlane to 1000m to expand viewing range.");
            }
        }

        if (userTransform == null) return;

        // Verify if a route is currently drawn and active in the application state
        bool isTourRunning = tourManager != null && tourManager.IsTourActiveAndRunning;
        if ((isTourRunning || routeManager.IsInModalitaPercorso()) && routeManager.GetSelectedStoneIDs().Count > 0)
        {
            ToggleHudVisibility(true);
            UpdateNavigationMetrics();
        }
        else
        {
            ToggleHudVisibility(false);
        }
    }

    private void UpdateNavigationMetrics()
    {
        string immediateNextTargetId = "";

        // Check if there is an active running Tour first to get the correct stop
        if (tourManager != null && tourManager.IsTourActiveAndRunning)
        {
            immediateNextTargetId = tourManager.GetCurrentTargetStoneID();
        }

        // Fallback to route manager list if no active tour is running
        if (string.IsNullOrEmpty(immediateNextTargetId))
        {
            var activeIds = routeManager.GetSelectedStoneIDs();
            if (activeIds == null || activeIds.Count == 0) return;
            immediateNextTargetId = activeIds[0];
        }

        // 1. Update Target Stone Text
        if (immediateNextTargetId != lastTargetId)
        {
            lastTargetId = immediateNextTargetId;
            lastDistance = -1f;
            if (targetStoneText != null)
            {
                Color targetColor = GetTargetCategoryColor(immediateNextTargetId);
                targetStoneText.text = $"Walking towards: <b><color=#{ColorUtility.ToHtmlStringRGB(targetColor)}>Stone {immediateNextTargetId}</color></b>";
            }
        }

        // 2. Update Dynamic Distance Text (Calculates real-time straight-line displacement vector)
        if (memorialSpawner == null)
        {
            memorialSpawner = UnityEngine.Object.FindAnyObjectByType<MemorialSpawner>(FindObjectsInactive.Include);
        }

        if (memorialSpawner != null)
        {
            GameObject targetMonumentObject = memorialSpawner.GetSpawnedMemorial(immediateNextTargetId);
            if (targetMonumentObject != null)
            {
                float physicalDistanceMeters = Vector3.Distance(userTransform.position, targetMonumentObject.transform.position);
                
                if (distanceIndicatorText != null && (lastDistance < 0f || Mathf.Abs(physicalDistanceMeters - lastDistance) >= 0.05f))
                {
                    lastDistance = physicalDistanceMeters;
                    distanceIndicatorText.text = $"Distance: <b>{physicalDistanceMeters:F1} meters</b>";
                }
                UpdateDirectionIndicator(targetMonumentObject.transform.position);
            }
            else if (distanceIndicatorText != null && lastDistance != -999f)
            {
                lastDistance = -999f;
                distanceIndicatorText.text = "Distance: <b>--</b>";
            }
        }
    }

    private Color GetTargetCategoryColor(string id)
    {
        if (memorialDataManager == null)
        {
            memorialDataManager = UnityEngine.Object.FindAnyObjectByType<MemorialDataManager>(FindObjectsInactive.Include);
        }
        object target = memorialDataManager != null ? memorialDataManager.GetDataByID(id) : null;
        if (target is MemorialDataManager.MassGrave) return new Color(0.902f, 0.624f, 0f, 1f);
        if (target is MemorialDataManager.OtherMemorial) return new Color(0.8f, 0.475f, 0.655f, 1f);
        return new Color(0f, 0.447f, 0.698f, 1f);
    }

    private string GetDirectionArrow(Vector3 targetPosition)
    {
        if (userTransform == null) return "↑";
        Vector3 direction = targetPosition - userTransform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) return "↑";
        float angle = Vector3.SignedAngle(userTransform.forward, direction.normalized, Vector3.up);
        if (angle > 135f || angle < -135f) return "↓";
        if (angle > 67.5f) return "→";
        if (angle > 22.5f) return "↗";
        if (angle < -67.5f) return "←";
        if (angle < -22.5f) return "↖";
        return "↑";
    }

    private void UpdateDirectionIndicator(Vector3 targetPosition)
    {
        EnsureDirectionIndicator();
        if (directionIndicator == null || userTransform == null) return;

        Vector3 direction = targetPosition - userTransform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) return;

        float angle = Vector3.SignedAngle(userTransform.forward, direction.normalized, Vector3.up);
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, -angle);
        directionIndicator.rectTransform.localRotation = Quaternion.Slerp(directionIndicator.rectTransform.localRotation, targetRotation, Time.deltaTime * 14f);

        if (!directionIndicator.gameObject.activeSelf)
            directionIndicator.gameObject.SetActive(true);
    }

    private void EnsureDirectionIndicator()
    {
        // Resolve direct parent: AR_Exploration_Hub canvas container if available
        Transform parentContainer = (hudPanelContainer != null && hudPanelContainer.transform.parent != null)
            ? hudPanelContainer.transform.parent
            : transform;

        if (directionIndicator == null)
        {
            Transform existing = parentContainer.Find("Direction_Indicator") ?? parentContainer.Find("DirectionIndicator");
            if (existing == null && hudPanelContainer != null)
            {
                existing = hudPanelContainer.transform.Find("Direction_Indicator") ?? hudPanelContainer.transform.Find("DirectionIndicator");
            }

            if (existing != null)
            {
                directionIndicator = existing.GetComponent<Image>();
            }
        }

        if (directionIndicator == null)
        {
            GameObject indicator = new GameObject("Direction_Indicator", typeof(RectTransform), typeof(Image));
            indicator.transform.SetParent(parentContainer, false);

            RectTransform rect = indicator.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(110f, -110f); // Default 200x200 top-left position
            rect.sizeDelta = new Vector2(200f, 200f);         // 200x200 badge size

            directionIndicator = indicator.GetComponent<Image>();
        }

        if (directionIndicator != null)
        {
            if (directionIndicator.sprite == null)
            {
                directionIndicator.sprite = CreateDirectionSprite();
            }
            directionIndicator.type = Image.Type.Simple;
            directionIndicator.preserveAspect = true;
            directionIndicator.color = Color.white;
            directionIndicator.raycastTarget = false;

            // In Edit Mode, make sure it stays active so user can see and adjust it in Scene/Hierarchy
            if (!Application.isPlaying && !directionIndicator.gameObject.activeSelf)
            {
                directionIndicator.gameObject.SetActive(true);
            }
        }
    }

    private static Sprite CreateDirectionSprite()
    {
        const int s = 128;
        float center = (s - 1) * 0.5f;
        Texture2D texture = new Texture2D(s, s, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color discBg = new Color(0.06f, 0.09f, 0.16f, 0.85f); // Slate dark circular badge
        Color discBorder = new Color(0.22f, 0.74f, 0.97f, 0.95f); // Sky blue ring

        // Arrow 3D facets
        Color arrowLeftLight = new Color(0.95f, 0.98f, 1f, 1f); // Bright white-cyan left facet
        Color arrowLeftDark = new Color(0.7f, 0.9f, 1f, 1f);
        Color arrowRightLight = new Color(0.05f, 0.55f, 0.85f, 1f); // Shadowed blue right facet
        Color arrowRightDark = new Color(0.01f, 0.35f, 0.65f, 1f);
        Color shadowColor = new Color(0f, 0f, 0f, 0.45f);

        float discRadius = 58f;
        float borderThickness = 3f;

        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // Anti-aliased outer circular badge
                if (dist > discRadius + 1f)
                {
                    texture.SetPixel(x, y, clear);
                    continue;
                }

                Color pixelColor = clear;

                // Base disc + border
                if (dist >= discRadius - borderThickness)
                {
                    float alpha = Mathf.Clamp01(discRadius + 1f - dist);
                    pixelColor = Color.Lerp(discBg, discBorder, (dist - (discRadius - borderThickness)) / borderThickness);
                    pixelColor.a *= alpha;
                }
                else
                {
                    pixelColor = discBg;
                }

                // Drop Shadow (offset +2, -2)
                float sdx = dx - 2f;
                float sdy = dy + 2f;
                if (IsInsideArrow(sdx, sdy))
                {
                    pixelColor = Color.Lerp(pixelColor, shadowColor, shadowColor.a);
                }

                // Main Arrow 3D geometry
                if (IsInsideArrow(dx, dy))
                {
                    float tY = Mathf.Clamp01((dy + 35f) / 75f);
                    if (dx < 0f) // Left Facet (Lighted)
                    {
                        Color facetColor = Color.Lerp(arrowLeftDark, arrowLeftLight, tY);
                        pixelColor = facetColor;
                    }
                    else // Right Facet (Shadowed 3D Depth)
                    {
                        Color facetColor = Color.Lerp(arrowRightDark, arrowRightLight, tY);
                        pixelColor = facetColor;
                    }

                    // Highlight central ridge spine
                    if (Mathf.Abs(dx) < 1.2f && dy > -30f && dy < 38f)
                    {
                        pixelColor = Color.white;
                    }
                }

                texture.SetPixel(x, y, pixelColor);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
    }

    private static bool IsInsideArrow(float dx, float dy)
    {
        // Arrow pointing UP: Head tip at dy = +40, base at dy = -32, wings at dy = -12, dx = +/- 26
        if (dy > 40f || dy < -32f) return false;

        // Head Triangle: from (0, 40) down to (-26, -12) and (+26, -12)
        if (dy >= -12f)
        {
            float widthAtY = (40f - dy) * (26f / 52f);
            return Mathf.Abs(dx) <= widthAtY;
        }

        // Arrow Tail / Notch: from dy = -12 to dy = -32 with inner V notch
        float tailWidth = (dy - (-32f)) * (26f / 20f);
        float innerNotch = -32f + Mathf.Abs(dx) * 0.5f;
        return Mathf.Abs(dx) <= tailWidth && dy >= innerNotch;
    }

    [SerializeField] private GameObject hudPanelContainer;

    public bool IsHudVisible => hudPanelContainer != null && hudPanelContainer.activeInHierarchy;
    public RectTransform HudRectTransform => hudPanelContainer != null ? hudPanelContainer.GetComponent<RectTransform>() : null;

    private void ToggleHudVisibility(bool isVisible)
    {
        if (!isVisible)
        {
            lastTargetId = null;
            lastDistance = -1f;
        }

        if (hudPanelContainer == null)
        {
            var t = transform.Find("Navigation_HUD_Panel") ?? transform.Find("NavigationHUDPanel");
            if (t != null) hudPanelContainer = t.gameObject;
            if (hudPanelContainer == null) hudPanelContainer = GameObject.Find("Canvas/AR_Exploration_Hub/Navigation_HUD_Panel") ?? GameObject.Find("Navigation_HUD_Panel");
        }

        if (hudPanelContainer != null && hudPanelContainer != this.gameObject && hudPanelContainer.activeSelf != isVisible)
        {
            hudPanelContainer.SetActive(isVisible);
        }

        if (directionIndicator != null && !isVisible) directionIndicator.gameObject.SetActive(false);

        UIManager uiManager = UnityEngine.Object.FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
        if (uiManager != null)
        {
            uiManager.UpdateToastPlacement(hudPanelContainer != null ? hudPanelContainer.GetComponent<RectTransform>() : null, isVisible);
        }

        if (targetStoneText != null && targetStoneText.gameObject.activeSelf != isVisible)
            targetStoneText.gameObject.SetActive(isVisible);

        if (distanceIndicatorText != null && distanceIndicatorText.gameObject.activeSelf != isVisible)
            distanceIndicatorText.gameObject.SetActive(isVisible);
    }

    /// <summary>
    /// Cancels and clears the currently active tour or route, hiding the HUD bar immediately.
    /// </summary>
    public void StopActiveRoute()
    {
        if (routeManager != null) routeManager.ClearAndResetRoute();
        
        ARWayfindingManager wayfinding = UnityEngine.Object.FindAnyObjectByType<ARWayfindingManager>(FindObjectsInactive.Include);
        if (wayfinding != null) wayfinding.StopNavigation();

        ToggleHudVisibility(false);
    }
}
