using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine.UI;

/// <summary>
/// Manages the 2D UI map view camera controls, mobile pinch zoom, bounds clamping, 
/// and handles the UI marker bypass projection system for 3D GLB node alignments.
/// Automatically synchronizes Route LineRenderer thickness dynamically based on camera orthographic size.
/// Zooms precisely toward the mouse cursor or touch pinch midpoint using infinite ground plane intersections.
/// All internal code, variables, and logs are strictly maintained in English.
/// </summary>
public class Map2DController : MonoBehaviour, IScrollHandler, IBeginDragHandler, IDragHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("🎥 Camera Reference")]
    [SerializeField] private Camera mapCamera;

    [Header("🎛️ PC Zoom Settings")]
    [SerializeField] private float mouseScrollSensitivity = 15f;

    [Header("📱 Mobile Zoom Settings")]
    [SerializeField] private float pinchZoomSensitivity = 0.05f;

    [Header("🛑 View Limits")]
    [SerializeField] private float minOrthographicSize = 10f;
    [SerializeField] private float maxOrthographicSize = 250f;

    [Header("🎯 Click Selection Radius")]
    [SerializeField] private float clickSelectionRadius = 0.06f;

    [Header("📱 Popup Auto-Tracking Interface")]
    [SerializeField] private RectTransform mapMiniPopupPanel;

    [Header("🎨 2D UI Bypass System")]
    [SerializeField] private GameObject uiMarkerPrefab;
    [SerializeField] private Sprite massGraveMarkerSprite;
    [SerializeField] private Sprite otherMemorialMarkerSprite;
    [SerializeField] private Sprite userMarkerSprite;

    [Header("Compass Simulated GPS User Tracking")]
    [Tooltip("Drag the blue UI Image object created for user positioning representation here.")]
    [SerializeField] private RectTransform uiUserGpsMarker;
    [Tooltip("Drag the semi-transparent radar/gaze cone UI Image object created here.")]
    [SerializeField] private RectTransform uiUserGazeCone;
    [Tooltip("Drag the empty 3D Simulated_GPS_Player GameObject created inside the Scene here.")]
    [SerializeField] private Transform simulatedUserTransform;

    [System.Serializable]
    public struct MapStylingSettings
    {
        [Header("🎨 Marker Colors")]
        public Color userMarkerColor;
        public Color normalStoneColor;
        public Color massGraveColor;
        public Color otherMemorialColor;
        public Color selectedRoutePinColor;

        [Header("📐 Marker Sizes")]
        public float markerPixelSize;
        public float minMarkerPixelSize;
        public float maxMarkerPixelSize;
        public float userMarkerSize;

        [Header("🛣️ Route Line Style")]
        public Color routeLineColor;
        public float baseLineWidth;
        public float minLineWidth;
        public float maxLineWidth;
        public float referenceZoomSize;

        [Header("🎥 Camera Move Limits")]
        [Tooltip("Max offset the camera can move away from its starting position.")]
        public float maxMoveBounds;

        [Header("⚽ Soccer Field Test Zones")]
        public Color soccerZoneNeutralColor;
        public Color soccerZoneActiveColor;
        public float soccerZoneLineWidth;
    }

    [Header("🎨 Centralized Map Styling")]
    [SerializeField] private MapStylingSettings mapStyling = new MapStylingSettings
    {
        userMarkerColor = Color.yellow,
        normalStoneColor = new Color(0f, 0.447f, 0.698f, 1f), // blue
        massGraveColor = new Color(0.902f, 0.624f, 0f, 1f), // orange
        otherMemorialColor = new Color(0.8f, 0.475f, 0.655f, 1f), // magenta
        selectedRoutePinColor = Color.yellow,
        markerPixelSize = 32f,
        minMarkerPixelSize = 20f,
        maxMarkerPixelSize = 48f,
        userMarkerSize = 28f, // User marker size
        routeLineColor = Color.yellow,
        baseLineWidth = 2f,
        minLineWidth = 0.3f,
        maxLineWidth = 5.0f,
        referenceZoomSize = 50f,
        maxMoveBounds = 150f, // Increased move limits to 150f so dragging to map borders is fully permitted
        soccerZoneNeutralColor = new Color(1f, 0f, 1f, 0.6f),
        soccerZoneActiveColor = Color.green,
        soccerZoneLineWidth = 2.0f
    };

    // Public Getters for Centralized Configs
    public Color GetNormalStoneColor() => mapStyling.normalStoneColor;
    public Color GetMassGraveColor() => mapStyling.massGraveColor;
    public Color GetOtherMemorialColor() => mapStyling.otherMemorialColor;
    public Color GetSelectedRoutePinColor() => mapStyling.selectedRoutePinColor;
    public Color GetRouteLineColor() => mapStyling.routeLineColor;
    public Color GetSoccerZoneNeutralColor() => mapStyling.soccerZoneNeutralColor;
    public Color GetSoccerZoneActiveColor() => mapStyling.soccerZoneActiveColor;
    public float GetSoccerZoneLineWidth() => mapStyling.soccerZoneLineWidth;


    private RouteManager cachedRouteManager;
    private TourManager cachedTourManager;
    private GeospatialManager cachedGeospatialManager;
    private UIManager cachedUIManager;

    private struct UiMarkerData
    {
        public RectTransform rectTransform;
        public Image imageComponent;
        public TMPro.TextMeshProUGUI textComponent;
        public InteractiveMapPin associatedPin;
        public Color categoryColor;
        public Sprite categorySprite;
    }

    private RectTransform rectTransform;
    private InteractiveMapPin selectedPin;
    private Canvas parentCanvas;
    private Camera uiCamera;
    private GameObject mapLegend;
    private readonly TMPro.TextMeshProUGUI[] legendRows = new TMPro.TextMeshProUGUI[3];
    private readonly bool[] categoryVisible = { true, true, true };
    private List<string> lastLegendTour;
    private int lastLegendTourCount = -1;

    private List<UiMarkerData> activeUiMarkers = new List<UiMarkerData>();
    private bool isUiBypassInitialized = false;

    [Header("Overview Marker Clustering")]
    [SerializeField] private float clusterAtOrthographicSize = 30f;
    [Tooltip("Screen-space radius, in pixels, used to merge nearby markers in the overview.")]
    [SerializeField] private float clusterScreenRadius = 96f;
    private int[] clusterLeaders;
    private int[] clusterCounts;
    private int[,] clusterCategoryCounts;
    private Vector2[] clusterPositionSums;
    private Sprite runtimeUserMarkerSprite;

    private Vector3 initialCameraPosition;
    private bool hasCapturedInitialLimits = false;

    private Vector3 dragStartWorldPos;
    private Vector2 startPressPosition;
    private Vector2 lastScreenPosition;
    private bool isPressingMap = false;
    private bool hasDraggedCamera = false;
    private const float dragRejectionThreshold = 15f;

    private bool isMultiTouchActive = false;
    private float cachedTouchDistance = 0f;

    private void OnEnable()
    {
        if (mapLegend != null) mapLegend.SetActive(true);
        if (mapCamera != null)
        {
            mapCamera.enabled = true;
            mapCamera.clearFlags = CameraClearFlags.SolidColor;
            mapCamera.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 1f);
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer != -1) mapCamera.cullingMask = ~(1 << uiLayer);
        }
    }

    private void OnDisable()
    {
        if (mapLegend != null) mapLegend.SetActive(false);
        if (mapCamera != null) mapCamera.enabled = false;
    }

    void Awake()
    {
        // Only the controller on Belsen_Map_Graphic has the references required to own runtime markers.
        // A legacy copy on Map_2D_Panel would otherwise create a second marker/legend set in Play Mode.
        if (mapCamera == null || simulatedUserTransform == null)
        {
            enabled = false;
            return;
        }

        rectTransform = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();
        mapStyling.userMarkerColor = Color.yellow;
    }

    void Start()
    {
        cachedRouteManager = UnityEngine.Object.FindAnyObjectByType<RouteManager>(FindObjectsInactive.Include);
        cachedTourManager = UnityEngine.Object.FindAnyObjectByType<TourManager>(FindObjectsInactive.Include);
        cachedGeospatialManager = UnityEngine.Object.FindAnyObjectByType<GeospatialManager>(FindObjectsInactive.Include);
        cachedUIManager = UnityEngine.Object.FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);

        if (cachedTourManager == null)
            Debug.LogWarning("[Map2DController] TourManager not found in scene at Start.");
        if (cachedGeospatialManager == null)
            Debug.LogWarning("[Map2DController] GeospatialManager not found in scene at Start.");

        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = parentCanvas.worldCamera;
        }

        if (mapCamera != null)
        {
            mapCamera.aspect = 1.0f;
            mapCamera.enabled = gameObject.activeInHierarchy;
            mapCamera.clearFlags = CameraClearFlags.SolidColor;
            mapCamera.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 1f);
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer != -1) mapCamera.cullingMask = ~(1 << uiLayer);

            RawImage rawImg = GetComponent<RawImage>();
            if (rawImg != null)
            {
                if (mapCamera.targetTexture != null) rawImg.texture = mapCamera.targetTexture;
                rawImg.color = Color.white;
            }
        }

        CreateMapLegend();
    }

    private void CreateMapLegend()
    {
        Transform legendScope = transform.parent != null ? transform.parent : transform;
        foreach (var existingText in legendScope.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
        {
            if (existingText.gameObject != gameObject &&
                (existingText.text.Contains("Memorial stones") ||
                 existingText.text.Contains("Mass graves") ||
                 existingText.text.Contains("Other memorials")))
            {
                Destroy(existingText.gameObject);
            }
        }

        GameObject legend = new GameObject("Map_Legend", typeof(RectTransform));
        mapLegend = legend;
        legend.transform.SetParent(legendScope, false);

        RectTransform rect = legend.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(175f, -375f);
        rect.sizeDelta = new Vector2(450f, 140f);

        CreateLegendRow(legend.transform, 0, "\u25A0", "Memorial stones", mapStyling.normalStoneColor);
        CreateLegendRow(legend.transform, 1, "\u25B2", "Mass graves", mapStyling.massGraveColor);
        CreateLegendRow(legend.transform, 2, "\u25CF", "Other memorials", mapStyling.otherMemorialColor);

        GameObject hint = new GameObject("Legend_Tap_Hint", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        hint.transform.SetParent(legend.transform, false);
        RectTransform hintRect = hint.GetComponent<RectTransform>();
        hintRect.anchorMin = hintRect.anchorMax = new Vector2(0.5f, 0f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.anchoredPosition = new Vector2(0f, -18f);
        hintRect.sizeDelta = new Vector2(450f, 28f);
        var hintText = hint.GetComponent<TMPro.TextMeshProUGUI>();
        hintText.font = TMPro.TMP_Settings.defaultFontAsset;
        hintText.fontSize = PlayerPrefs.GetFloat("Thesis_BodyFontSize", 20f) * 0.75f;
        hintText.alignment = TMPro.TextAlignmentOptions.Left;
        hintText.color = new Color(1f, 1f, 1f, 0.75f);
        hintText.text = cachedUIManager != null && cachedUIManager.SelectedLanguage == "german"
            ? "Tippen zum Ein-/Ausblenden" : "Tap a row to show or hide it";
    }

    private void CreateLegendRow(Transform parent, int categoryIndex, string symbol, string label, Color color)
    {
        GameObject row = new GameObject($"Legend_Filter_{categoryIndex}", typeof(RectTransform), typeof(Image), typeof(Button));
        row.transform.SetParent(parent, false);
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.anchoredPosition = new Vector2(0f, -categoryIndex * 45f);
        rowRect.sizeDelta = new Vector2(0f, 44f);
        row.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.12f);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        labelObject.transform.SetParent(row.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 0f);
        labelRect.offsetMax = new Vector2(-12f, 0f);

        TMPro.TextMeshProUGUI text = labelObject.GetComponent<TMPro.TextMeshProUGUI>();
        text.font = TMPro.TMP_Settings.defaultFontAsset;
        text.fontSize = PlayerPrefs.GetFloat("Thesis_BodyFontSize", 20f);
        text.alignment = TMPro.TextAlignmentOptions.Left;
        text.raycastTarget = false;
        text.text = $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{symbol}</color> {label}";
        legendRows[categoryIndex] = text;

        int index = categoryIndex;
        row.GetComponent<Button>().onClick.AddListener(() => ToggleLegendCategory(index));
    }

    private void ToggleLegendCategory(int categoryIndex)
    {
        categoryVisible[categoryIndex] = !categoryVisible[categoryIndex];
        lastLegendTourCount = -1;
        UpdateAllMarkersPositions();
    }

    public void ApplyLegendFontSize(float fontSize)
    {
        foreach (TMPro.TextMeshProUGUI row in legendRows)
        {
            if (row != null) row.fontSize = fontSize;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        mapStyling.minMarkerPixelSize = Mathf.Max(mapStyling.minMarkerPixelSize, 20f);
        mapStyling.maxMarkerPixelSize = Mathf.Max(mapStyling.maxMarkerPixelSize, mapStyling.minMarkerPixelSize);
        SyncEditorLegendPreview();

        // Propagate styling changes to SoccerFieldZone in the Editor
        SoccerFieldZone[] zones = UnityEngine.Object.FindObjectsByType<SoccerFieldZone>(FindObjectsInactive.Include);
        foreach (var zone in zones)
        {
            if (zone != null)
            {
                zone.ForceSnapAndDraw();
            }
        }
    }

    private void SyncEditorLegendPreview()
    {
        if (Application.isPlaying || transform.parent == null) return;

        Transform legend = transform.parent.Find("Map_Legend");
        TMPro.TextMeshProUGUI legendText = legend != null ? legend.GetComponent<TMPro.TextMeshProUGUI>() : null;
        if (legendText == null) return;

        legendText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(mapStyling.normalStoneColor)}>■</color> Memorial stones\n" +
                          $"<color=#{ColorUtility.ToHtmlStringRGB(mapStyling.massGraveColor)}>▲</color> Mass graves\n" +
                          $"<color=#{ColorUtility.ToHtmlStringRGB(mapStyling.otherMemorialColor)}>●</color> Other memorials";
    }
#endif

    private void ClampCameraBoundsAndZoom()
    {
        if (mapCamera == null || !hasCapturedInitialLimits) return;

        mapCamera.orthographicSize = Mathf.Clamp(mapCamera.orthographicSize, minOrthographicSize, maxOrthographicSize);

        // Dynamic bounds clamping based on max map extent to allow zooming and panning anywhere on the map
        float maxShiftX = maxOrthographicSize * mapCamera.aspect;
        float maxShiftZ = maxOrthographicSize;

        float clampedX = Mathf.Clamp(mapCamera.transform.position.x, initialCameraPosition.x - maxShiftX, initialCameraPosition.x + maxShiftX);
        float clampedZ = Mathf.Clamp(mapCamera.transform.position.z, initialCameraPosition.z - maxShiftZ, initialCameraPosition.z + maxShiftZ);

        mapCamera.transform.position = new Vector3(clampedX, mapCamera.transform.position.y, clampedZ);
    }

    public Vector3 GetUserCurrentWorldPosition()
    {
        if (simulatedUserTransform != null) return simulatedUserTransform.position;
        return Vector3.zero;
    }

    public void ForceClosePopup()
    {
        selectedPin = null;
        if (mapMiniPopupPanel != null)
        {
            mapMiniPopupPanel.gameObject.SetActive(false);
        }
    }

    private Transform GetPointTransform(Transform pinTransform)
    {
        if (pinTransform == null) return null;
        if (pinTransform.name.StartsWith("point_")) return pinTransform;
        
        Transform curr = pinTransform;
        while (curr.parent != null)
        {
            if (curr.parent.name.StartsWith("point_")) return curr.parent;
            curr = curr.parent;
        }
        return pinTransform.parent;
    }

    private void Initialize2DUiBypass()
    {
        if (uiMarkerPrefab == null)
        {
            uiMarkerPrefab = Resources.Load<GameObject>("UI/UI_Marker_Prefab");
#if UNITY_EDITOR
            if (uiMarkerPrefab == null)
            {
                uiMarkerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/UI_Marker_Prefab.prefab");
            }
#endif
        }

        if (uiMarkerPrefab == null)
        {
            Debug.LogError("[UI BYPASS] Critical Error: Assign the uiMarkerPrefab inside the Inspector mapping slot!");
            isUiBypassInitialized = true;
            return;
        }

        EnsureInitialLimitsCaptured();

        // ⚡ SOURCE OF TRUTH FIX: Scansioniamo la gerarchia reale dei nodi fisici in scena (point_)
        // Troviamo sia i marker delle lapidi che quelli dei Mass Graves (es. point_MG1)
        InteractiveMapPin[] all3DPins = UnityEngine.Object.FindObjectsByType<InteractiveMapPin>(FindObjectsInactive.Exclude);
        MemorialDataManager dataManager = UnityEngine.Object.FindAnyObjectByType<MemorialDataManager>(FindObjectsInactive.Include);
        HashSet<string> initializedMarkerIDs = new HashSet<string>();

        foreach (InteractiveMapPin pin in all3DPins)
        {
            if (pin == null) continue;

            MeshRenderer renderer3D = pin.GetComponent<MeshRenderer>();
            if (renderer3D != null)
            {
                renderer3D.enabled = false; // Nascondiamo il mesh renderer 3D per usare il bypass 2D
            }

            // Look up node type to set correct styling color from centralized MapStylingSettings
            Transform pointTr = GetPointTransform(pin.transform);
            string markerID = pointTr != null ? pointTr.name.Replace("point_", "").Trim() : "";

            // Skip test/origin anchor points (e.g. point_0, point_1, point_2, point_3, origin, anchor, test)
            if (markerID == "0" || markerID == "1" || markerID == "2" || markerID == "3" ||
                markerID.ToLower().Contains("origin") || markerID.ToLower().Contains("anchor") ||
                markerID.ToLower().Contains("test") || markerID.ToLower().Contains("setup"))
            {
                continue;
            }
            if (!initializedMarkerIDs.Add(markerID)) continue;
            Color pinColor = mapStyling.normalStoneColor;

            if (dataManager != null)
            {
                object data = dataManager.GetDataByID(markerID);
                if (data is MemorialDataManager.MassGrave)
                {
                    pinColor = mapStyling.massGraveColor;
                }
                else if (data is MemorialDataManager.OtherMemorial)
                {
                    pinColor = mapStyling.otherMemorialColor;
                }
                else if (data is MemorialDataManager.MemorialStone)
                {
                    pinColor = mapStyling.normalStoneColor;
                }
            }
            else
            {
                // Fallback checks on ID prefixes if dataManager is missing
                if (markerID.StartsWith("MG", System.StringComparison.OrdinalIgnoreCase))
                {
                    pinColor = mapStyling.massGraveColor;
                }
                else if (markerID.StartsWith("OM", System.StringComparison.OrdinalIgnoreCase))
                {
                    pinColor = mapStyling.otherMemorialColor;
                }
            }

            pin.SetOriginalColor(pinColor);

            GameObject newMarkerObj = Instantiate(uiMarkerPrefab, this.transform);
            RectTransform markerRect = newMarkerObj.GetComponent<RectTransform>();
            Image markerImg = newMarkerObj.GetComponent<Image>();

            if (markerImg != null)
            {
                markerImg.color = pinColor;
                if (pinColor == mapStyling.massGraveColor && massGraveMarkerSprite != null)
                    markerImg.sprite = massGraveMarkerSprite;
                else if (pinColor == mapStyling.otherMemorialColor && otherMemorialMarkerSprite != null)
                    markerImg.sprite = otherMemorialMarkerSprite;

                Outline markerOutline = newMarkerObj.GetComponent<Outline>();
                if (markerOutline == null) markerOutline = newMarkerObj.AddComponent<Outline>();
                markerOutline.effectColor = new Color(0.03f, 0.05f, 0.08f, 0.9f);
                markerOutline.effectDistance = new Vector2(1.5f, -1.5f);
            }

            if (markerRect != null)
            {
                markerRect.sizeDelta = new Vector2(mapStyling.markerPixelSize, mapStyling.markerPixelSize);
                markerRect.anchorMin = new Vector2(0.5f, 0.5f);
                markerRect.anchorMax = new Vector2(0.5f, 0.5f);
                markerRect.pivot = new Vector2(0.5f, 0.5f);

                // Dynamically inject numbering text component child for tour sequence numbering
                GameObject textObj = new GameObject("MarkerNumberText");
                textObj.transform.SetParent(markerRect, false);
                var textComp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
                textComp.alignment = TMPro.TextAlignmentOptions.Center;
                textComp.fontSize = 11f;
                textComp.fontStyle = TMPro.FontStyles.Bold;
                textComp.color = Color.white;
                textComp.outlineColor = new Color(0.03f, 0.05f, 0.08f, 1f);
                textComp.outlineWidth = 0.18f;
                textComp.raycastTarget = false;
                
                RectTransform textRect = textObj.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.sizeDelta = Vector2.zero;
                textRect.anchoredPosition = Vector2.zero;

                UiMarkerData data = new UiMarkerData
                {
                    rectTransform = markerRect,
                    imageComponent = markerImg,
                    textComponent = textComp,
                    associatedPin = pin,
                    categoryColor = pinColor,
                    categorySprite = markerImg != null ? markerImg.sprite : null
                };
                activeUiMarkers.Add(data);
            }
        }

        if (mapMiniPopupPanel != null) mapMiniPopupPanel.SetAsLastSibling();
        if (uiUserGpsMarker != null) uiUserGpsMarker.SetAsLastSibling();

        isUiBypassInitialized = true;
        UpdateAllMarkersPositions();
    }

    void LateUpdate()
    {
        if (!isUiBypassInitialized)
        {
            Initialize2DUiBypass();
        }

        UpdateAllMarkersPositions();
        SyncRouteLineWidth();
    }

    private void UpdateAllMarkersPositions()
    {
        if (mapCamera == null || activeUiMarkers.Count == 0) return;

        float worldViewWindow = mapCamera.orthographicSize * 2f;
        Vector3 camPos = mapCamera.transform.position;

        // Check if a tour is active and get its ordered IDs
        if (cachedTourManager == null)
            cachedTourManager = UnityEngine.Object.FindAnyObjectByType<TourManager>(FindObjectsInactive.Include);

        List<string> activeTourOrderedStones = (cachedTourManager != null)
            ? cachedTourManager.GetActiveTourOrderedStoneIDs()
            : null;
        if (activeTourOrderedStones == null && cachedRouteManager != null)
            activeTourOrderedStones = cachedRouteManager.GetSelectedStoneIDs();

        foreach (var marker in activeUiMarkers)
        {
            Transform pointTr = GetPointTransform(marker.associatedPin.transform);
            if (marker.associatedPin == null || pointTr == null) continue;

            Vector3 targetWorldPos = pointTr.position;

            float deltaX = targetWorldPos.x - camPos.x;
            float deltaZ = targetWorldPos.z - camPos.z;

            float normX = deltaX / worldViewWindow;
            float normY = deltaZ / worldViewWindow;

            bool isInsideViewport = (normX >= -0.5f && normX <= 0.5f && normY >= -0.5f && normY <= 0.5f);

            bool shouldDisplay = isInsideViewport && IsCategoryVisible(marker.categoryColor);
            if (marker.imageComponent != null)
            {
                marker.imageComponent.enabled = shouldDisplay;
            }

            var markerText = marker.textComponent;

            if (shouldDisplay)
            {
                float localX = normX * rectTransform.rect.width;
                float localY = normY * rectTransform.rect.height;
                marker.rectTransform.anchoredPosition = new Vector2(localX, localY);

                // Centralized sizing limits / ranges
                float targetMarkerSize = mapStyling.markerPixelSize * (mapStyling.referenceZoomSize / mapCamera.orthographicSize);
                targetMarkerSize = Mathf.Clamp(targetMarkerSize, Mathf.Max(mapStyling.minMarkerPixelSize, 20f), mapStyling.maxMarkerPixelSize);
                marker.rectTransform.sizeDelta = new Vector2(targetMarkerSize, targetMarkerSize);

                marker.imageComponent.sprite = marker.categorySprite;
                marker.imageComponent.color = marker.categoryColor;

                // Sequence number rendering
                if (markerText != null)
                {
                    string stoneID = pointTr.name.Replace("point_", "").Trim();
                    int tourIndex = (activeTourOrderedStones != null) ? activeTourOrderedStones.IndexOf(stoneID) : -1;
                    if (tourIndex != -1)
                    {
                        markerText.text = (tourIndex + 1).ToString();
                        markerText.fontSize = Mathf.Max(6f, targetMarkerSize * 0.6f);

                        // Adjust text color based on marker color luminance for high contrast readability
                        Color markerColor = marker.imageComponent.color;
                        float luminance = 0.2126f * markerColor.r + 0.7152f * markerColor.g + 0.0722f * markerColor.b;
                        markerText.color = (luminance > 0.5f) ? Color.black : Color.white;
                    }
                    else
                    {
                        markerText.text = string.Empty;
                    }
                }
            }
            else
            {
                if (markerText != null)
                {
                    markerText.text = string.Empty;
                }
            }
        }

        ApplyOverviewClustering();
        RefreshLegendCounts(activeTourOrderedStones);

        // --- User GPS Position & Gaze Cone Heading Tracking ---
        if (uiUserGpsMarker != null && simulatedUserTransform != null)
        {
            Vector3 userWorldPos = simulatedUserTransform.position;
            float deltaX = userWorldPos.x - camPos.x;
            float deltaZ = userWorldPos.z - camPos.z;

            float normX = deltaX / worldViewWindow;
            float normY = deltaZ / worldViewWindow;

            bool isUserInside = (normX >= -0.5f && normX <= 0.5f && normY >= -0.5f && normY <= 0.5f);
            uiUserGpsMarker.GetComponent<Image>().enabled = isUserInside;
            if (uiUserGazeCone != null)
            {
                uiUserGazeCone.gameObject.SetActive(isUserInside);
            }

            if (isUserInside)
            {
                float localX = normX * rectTransform.rect.width;
                float localY = normY * rectTransform.rect.height;
                uiUserGpsMarker.anchoredPosition = new Vector2(localX, localY);

                // Set user marker styling from centralized config
                Image userMarkerImage = uiUserGpsMarker.GetComponent<Image>();
                if (userMarkerImage != null)
                {
                    userMarkerImage.color = mapStyling.userMarkerColor;
                    userMarkerImage.sprite = userMarkerSprite != null ? userMarkerSprite : GetRuntimeUserMarkerSprite();
                }
                uiUserGpsMarker.sizeDelta = new Vector2(mapStyling.userMarkerSize, mapStyling.userMarkerSize);

                // Live rotation updates utilizing device hardware compass strings
                if (uiUserGazeCone != null)
                {
                    Image gazeConeImage = uiUserGazeCone.GetComponent<Image>();
                    if (gazeConeImage != null) gazeConeImage.color = Color.yellow;
                    float targetHeadingDegrees = 0f;
                    GeospatialManager geospatial = cachedGeospatialManager ?? UnityEngine.Object.FindAnyObjectByType<GeospatialManager>(FindObjectsInactive.Include);

                    if (geospatial != null && geospatial.IsGeospatialReady)
                    {
                        targetHeadingDegrees = geospatial.CurrentHeading; // Active hardware device compass string
                    }
                    else
                    {
                        targetHeadingDegrees = simulatedUserTransform.eulerAngles.y; // PC Unity Editor simulation fallback
                    }

                    // Converts navigation systems compass tracking to responsive UI Z-axis angles
                    uiUserGazeCone.localEulerAngles = new Vector3(0f, 0f, -targetHeadingDegrees +180f);
                }
            }
        }

        if (mapMiniPopupPanel != null && mapMiniPopupPanel.gameObject.activeSelf && selectedPin != null)
        {
            Transform pointTr = GetPointTransform(selectedPin.transform);
            Vector3 targetWorldPos = pointTr != null ? pointTr.position : selectedPin.transform.position;
            float deltaX = targetWorldPos.x - camPos.x;
            float deltaZ = targetWorldPos.z - camPos.z;

            float normX = deltaX / worldViewWindow;
            float normY = deltaZ / worldViewWindow;

            if (normX >= -0.5f && normX <= 0.5f && normY >= -0.5f && normY <= 0.5f)
            {
                mapMiniPopupPanel.anchorMin = new Vector2(0.5f, 0.5f);
                mapMiniPopupPanel.anchorMax = new Vector2(0.5f, 0.5f);

                float localX = normX * rectTransform.rect.width;
                float localY = normY * rectTransform.rect.height;
                mapMiniPopupPanel.anchoredPosition = new Vector2(localX, localY);
            }
            else
            {
                mapMiniPopupPanel.gameObject.SetActive(false);
            }
        }
    }

    private void ApplyOverviewClustering()
    {
        if (mapCamera.orthographicSize < clusterAtOrthographicSize) return;

        int markerCount = activeUiMarkers.Count;
        if (clusterLeaders == null || clusterLeaders.Length != markerCount)
        {
            clusterLeaders = new int[markerCount];
            clusterCounts = new int[markerCount];
            clusterCategoryCounts = new int[markerCount, 3];
            clusterPositionSums = new Vector2[markerCount];
        }

        for (int i = 0; i < markerCount; i++)
        {
            clusterLeaders[i] = -1;
            clusterCounts[i] = 0;
            clusterCategoryCounts[i, 0] = 0;
            clusterCategoryCounts[i, 1] = 0;
            clusterCategoryCounts[i, 2] = 0;
            clusterPositionSums[i] = Vector2.zero;
        }

        float radiusSqr = clusterScreenRadius * clusterScreenRadius;
        for (int i = 0; i < markerCount; i++)
        {
            UiMarkerData marker = activeUiMarkers[i];
            if (marker.imageComponent == null || !marker.imageComponent.enabled) continue;

            clusterLeaders[i] = i;
        }

        for (int i = 0; i < markerCount; i++)
        {
            UiMarkerData marker = activeUiMarkers[i];
            if (marker.imageComponent == null || !marker.imageComponent.enabled) continue;

            for (int j = 0; j < i; j++)
            {
                UiMarkerData other = activeUiMarkers[j];
                if (other.imageComponent == null || !other.imageComponent.enabled) continue;

                Vector2 offset = marker.rectTransform.anchoredPosition - other.rectTransform.anchoredPosition;
                if (offset.sqrMagnitude <= radiusSqr)
                {
                    int rootI = GetClusterRoot(i);
                    int rootJ = GetClusterRoot(j);
                    if (rootI != rootJ) clusterLeaders[rootI] = rootJ;
                }
            }
        }

        for (int i = 0; i < markerCount; i++)
        {
            UiMarkerData marker = activeUiMarkers[i];
            if (marker.imageComponent == null || !marker.imageComponent.enabled) continue;

            int root = GetClusterRoot(i);
            clusterLeaders[i] = root;
            clusterCounts[root]++;
            clusterCategoryCounts[root, GetCategoryIndex(marker.categoryColor)]++;
            clusterPositionSums[root] += marker.rectTransform.anchoredPosition;
        }

        MergeOverlappingClusterBadges(markerCount);

        for (int i = 0; i < markerCount; i++)
        {
            clusterCounts[i] = 0;
            clusterCategoryCounts[i, 0] = 0;
            clusterCategoryCounts[i, 1] = 0;
            clusterCategoryCounts[i, 2] = 0;
            clusterPositionSums[i] = Vector2.zero;
        }

        for (int i = 0; i < markerCount; i++)
        {
            UiMarkerData marker = activeUiMarkers[i];
            if (marker.imageComponent == null || !marker.imageComponent.enabled) continue;

            int root = GetClusterRoot(i);
            clusterLeaders[i] = root;
            clusterCounts[root]++;
            clusterCategoryCounts[root, GetCategoryIndex(marker.categoryColor)]++;
            clusterPositionSums[root] += marker.rectTransform.anchoredPosition;
        }

        for (int i = 0; i < markerCount; i++)
        {
            UiMarkerData marker = activeUiMarkers[i];
            if (marker.imageComponent == null || !marker.imageComponent.enabled) continue;

            int leaderIndex = clusterLeaders[i];
            if (leaderIndex != i)
            {
                marker.imageComponent.enabled = false;
                if (marker.textComponent != null) marker.textComponent.text = string.Empty;
                continue;
            }

            int count = clusterCounts[i];
            if (marker.textComponent == null) continue;

            if (count > 1)
            {
                marker.rectTransform.anchoredPosition = clusterPositionSums[i] / count;
                marker.imageComponent.sprite = null;
                marker.imageComponent.color = new Color(0.07f, 0.11f, 0.16f, 0.9f);
                marker.textComponent.fontSize = Mathf.Clamp(21f + Mathf.Log(count, 2f) * 3f, 21f, 30f);
                marker.textComponent.text = FormatClusterSummary(clusterCategoryCounts, i);
                marker.rectTransform.sizeDelta = new Vector2(28f + marker.textComponent.preferredWidth, marker.textComponent.fontSize + 24f);
                marker.textComponent.color = Color.white;
            }
            else
            {
                // Sequence labels belong to detailed view; at overview they turn into unreadable noise.
                marker.textComponent.text = string.Empty;
            }
        }

        ResolveClusterBadgeCollisions(markerCount);
    }

    private int GetClusterRoot(int index)
    {
        while (clusterLeaders[index] != index)
        {
            clusterLeaders[index] = clusterLeaders[clusterLeaders[index]];
            index = clusterLeaders[index];
        }
        return index;
    }

    private void MergeOverlappingClusterBadges(int markerCount)
    {
        for (int i = 0; i < markerCount; i++)
        {
            if (clusterLeaders[i] != i || clusterCounts[i] == 0) continue;
            Vector2 centerI = clusterPositionSums[i] / clusterCounts[i];
            float halfWidthI = EstimateClusterBadgeWidth(clusterCategoryCounts, i) * 0.5f;

            for (int j = 0; j < i; j++)
            {
                if (clusterLeaders[j] != j || clusterCounts[j] == 0) continue;
                Vector2 centerJ = clusterPositionSums[j] / clusterCounts[j];
                float halfWidthJ = EstimateClusterBadgeWidth(clusterCategoryCounts, j) * 0.5f;
                if (Vector2.Distance(centerI, centerJ) <= halfWidthI + halfWidthJ)
                    clusterLeaders[i] = j;
            }
        }
    }

    private static float EstimateClusterBadgeWidth(int[,] categoryCounts, int clusterIndex)
    {
        int visibleCategories = 0;
        int digits = 0;
        for (int category = 0; category < 3; category++)
        {
            int count = categoryCounts[clusterIndex, category];
            if (count == 0) continue;
            visibleCategories++;
            digits += count >= 100 ? 3 : count >= 10 ? 2 : 1;
        }
        return 28f + visibleCategories * 20f + digits * 13f;
    }

    private void ResolveClusterBadgeCollisions(int markerCount)
    {
        // A cluster label is UI, not geometry: its visual rectangle must never obscure another.
        // Re-run a few passes because moving one compact badge can uncover a second collision.
        for (int pass = 0; pass < 4; pass++)
        {
            bool movedAny = false;
            for (int i = 0; i < markerCount; i++)
            {
                if (clusterLeaders[i] != i || clusterCounts[i] < 2) continue;
                RectTransform first = activeUiMarkers[i].rectTransform;
                for (int j = 0; j < i; j++)
                {
                    if (clusterLeaders[j] != j || clusterCounts[j] < 2) continue;
                    RectTransform second = activeUiMarkers[j].rectTransform;
                    float halfWidth = (first.sizeDelta.x + second.sizeDelta.x) * 0.5f;
                    float halfHeight = (first.sizeDelta.y + second.sizeDelta.y) * 0.5f;
                    Vector2 delta = first.anchoredPosition - second.anchoredPosition;
                    if (Mathf.Abs(delta.x) >= halfWidth || Mathf.Abs(delta.y) >= halfHeight) continue;

                    RectTransform moved = clusterCounts[i] <= clusterCounts[j] ? first : second;
                    float verticalOverlap = halfHeight - Mathf.Abs(delta.y);
                    moved.anchoredPosition += Vector2.up * (verticalOverlap + 8f);
                    movedAny = true;
                }
            }
            if (!movedAny) return;
        }
    }

    private Sprite GetRuntimeUserMarkerSprite()
    {
        if (runtimeUserMarkerSprite != null) return runtimeUserMarkerSprite;

        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.5f - 1f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float alpha = Vector2.Distance(new Vector2(x, y), center) <= radius ? 1f : 0f;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        texture.Apply();
        runtimeUserMarkerSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        return runtimeUserMarkerSprite;
    }

    private int GetCategoryIndex(Color categoryColor)
    {
        if (categoryColor == mapStyling.normalStoneColor) return 0;
        if (categoryColor == mapStyling.massGraveColor) return 1;
        return 2;
    }

    private string FormatClusterSummary(int[,] categoryCounts, int clusterIndex)
    {
        string summary = string.Empty;
        string[] symbols = { "■", "▲", "●" };
        Color[] colors = { mapStyling.normalStoneColor, mapStyling.massGraveColor, mapStyling.otherMemorialColor };
        for (int category = 0; category < 3; category++)
        {
            int count = categoryCounts[clusterIndex, category];
            if (count == 0) continue;
            if (summary.Length > 0) summary += "  ";
            summary += $"<color=#{ColorUtility.ToHtmlStringRGB(colors[category])}>{symbols[category]}</color>{count}";
        }
        return summary;
    }

    private bool IsCategoryVisible(Color categoryColor)
    {
        if (categoryColor == mapStyling.normalStoneColor) return categoryVisible[0];
        if (categoryColor == mapStyling.massGraveColor) return categoryVisible[1];
        return categoryVisible[2];
    }

    private void RefreshLegendCounts(List<string> activeTourOrderedStones)
    {
        if (legendRows[0] == null ||
            (ReferenceEquals(activeTourOrderedStones, lastLegendTour) &&
             (activeTourOrderedStones == null ? lastLegendTourCount == 0 : activeTourOrderedStones.Count == lastLegendTourCount)))
        {
            return;
        }

        int[] total = new int[3];
        int[] selected = new int[3];
        HashSet<string> countedMarkerIDs = new HashSet<string>();
        foreach (UiMarkerData marker in activeUiMarkers)
        {
            Transform point = marker.associatedPin != null ? GetPointTransform(marker.associatedPin.transform) : null;
            string id = point != null ? point.name.Replace("point_", string.Empty).Trim() : string.Empty;
            if (string.IsNullOrEmpty(id) || !countedMarkerIDs.Add(id)) continue;

            int categoryIndex = marker.categoryColor == mapStyling.normalStoneColor ? 0 :
                marker.categoryColor == mapStyling.massGraveColor ? 1 : 2;
            total[categoryIndex]++;

            if (activeTourOrderedStones != null && activeTourOrderedStones.Contains(id)) selected[categoryIndex]++;
        }

        string[] symbols = { "\u25A0", "\u25B2", "\u25CF" };
        string[] labels = { "Memorial stones", "Mass graves", "Other memorials" };
        Color[] colors = { mapStyling.normalStoneColor, mapStyling.massGraveColor, mapStyling.otherMemorialColor };
        for (int i = 0; i < 3; i++)
        {
            string routeCount = activeTourOrderedStones == null ? string.Empty : $"  {selected[i]}/{total[i]}";
            legendRows[i].text = $"<color=#{ColorUtility.ToHtmlStringRGB(colors[i])}>{symbols[i]}</color> {labels[i]}: {total[i]}{routeCount}";
            legendRows[i].alpha = categoryVisible[i] ? 1f : 0.4f;
        }

        lastLegendTour = activeTourOrderedStones;
        lastLegendTourCount = activeTourOrderedStones != null ? activeTourOrderedStones.Count : 0;
    }

    private void SyncRouteLineWidth()
    {
        if (mapCamera == null) return;

        RouteManager routeMgr = UnityEngine.Object.FindAnyObjectByType<RouteManager>(FindObjectsInactive.Include);
        if (routeMgr == null) return;

        LineRenderer lr = routeMgr.GetComponent<LineRenderer>();
        if (lr == null) lr = routeMgr.GetComponentInChildren<LineRenderer>();
        if (lr == null) return;

        float targetWidth = (mapCamera.orthographicSize / mapStyling.referenceZoomSize) * mapStyling.baseLineWidth;
        targetWidth = Mathf.Clamp(targetWidth, mapStyling.minLineWidth, mapStyling.maxLineWidth);

        lr.startWidth = targetWidth;
        lr.endWidth = targetWidth;
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (mapCamera == null) return;
        float scrollY = eventData.scrollDelta.y;
        if (Mathf.Abs(scrollY) > 0.01f)
        {
            float zoomAmount = scrollY * mouseScrollSensitivity * 0.05f;
            if (Mouse.current != null)
            {
                ExecuteFocusedZoom(zoomAmount, Mouse.current.position.ReadValue());
            }
        }
    }

    void Update()
    {
        if (mapCamera == null) return;

        if (Touchscreen.current != null && Touchscreen.current.touches.Count >= 2)
        {
            isPressingMap = false;
            var touch0 = Touchscreen.current.touches[0];
            var touch1 = Touchscreen.current.touches[1];

            if (touch0.press.isPressed && touch1.press.isPressed)
            {
                isMultiTouchActive = true;
                Vector2 t0Pos = touch0.position.ReadValue();
                Vector2 t1Pos = touch1.position.ReadValue();

                float currentDistance = Vector2.Distance(t0Pos, t1Pos);
                Vector2 pinchMidpoint = (t0Pos + t1Pos) / 2f;

                if (cachedTouchDistance > 0f)
                {
                    float distanceDelta = currentDistance - cachedTouchDistance;
                    float zoomAmount = distanceDelta * pinchZoomSensitivity;

                    if (Mathf.Abs(zoomAmount) > 0.01f)
                    {
                        ExecuteFocusedZoom(zoomAmount, pinchMidpoint);
                    }
                }
                cachedTouchDistance = currentDistance;
                return;
            }
        }
        else
        {
            isMultiTouchActive = false;
            cachedTouchDistance = 0f;
        }

        if (isPressingMap && !isMultiTouchActive)
        {
            Vector2 currentScreenPos = Vector2.zero;
            if (Mouse.current != null && Touchscreen.current == null) currentScreenPos = Mouse.current.position.ReadValue();
            else if (Touchscreen.current != null && Touchscreen.current.touches.Count == 1) currentScreenPos = Touchscreen.current.touches[0].position.ReadValue();

            if (currentScreenPos != Vector2.zero)
            {
                if (Vector2.Distance(startPressPosition, currentScreenPos) > dragRejectionThreshold)
                {
                    hasDraggedCamera = true;
                }

                Vector2 delta = currentScreenPos - lastScreenPosition;
                if (delta.magnitude > 0.05f)
                {
                    Vector3 worldPosLast = ConvertScreenToMapWorldSpace(lastScreenPosition);
                    Vector3 worldPosCurrent = ConvertScreenToMapWorldSpace(currentScreenPos);
                    Vector3 cameraShiftDirection = worldPosLast - worldPosCurrent;
                    cameraShiftDirection.y = 0f;

                    mapCamera.transform.position += cameraShiftDirection;
                    ClampCameraBoundsAndZoom();

                    lastScreenPosition = currentScreenPos;
                }
            }
        }
    }

    private void ExecuteFocusedZoom(float zoomStepAmount, Vector2 targetScreenPoint)
    {
        Vector3 worldPosBeforeZoom = ConvertScreenToMapWorldSpace(targetScreenPoint);
        float previousSize = mapCamera.orthographicSize;
        mapCamera.orthographicSize = Mathf.Clamp(mapCamera.orthographicSize - zoomStepAmount, minOrthographicSize, maxOrthographicSize);

        if (Mathf.Approximately(mapCamera.orthographicSize, previousSize)) return;

        Vector3 worldPosAfterZoom = ConvertScreenToMapWorldSpace(targetScreenPoint);
        Vector3 linearPositionShift = worldPosBeforeZoom - worldPosAfterZoom;
        linearPositionShift.y = 0f;

        mapCamera.transform.position += linearPositionShift;
        ClampCameraBoundsAndZoom();
    }

    /// <summary>
    /// Centers the map camera on a specific memorial stone anchor, forces a close zoom,
    /// and triggers the contextual layout mini-popup system automatically.
    /// </summary>
    /// <summary>
    /// Centers the map camera on a specific memorial stone anchor, forces a close zoom,
    /// and delegates popup visualization securely to the main UIManager tracking loops.
    /// </summary>
    private void EnsureInitialLimitsCaptured()
    {
        if (mapCamera != null && !hasCapturedInitialLimits)
        {
            initialCameraPosition = mapCamera.transform.position;
            maxOrthographicSize = Mathf.Max(mapCamera.orthographicSize, minOrthographicSize * 5.0f);
            hasCapturedInitialLimits = true;
        }
    }

    public void FocusAndZoomOnStone(string stoneID)
    {
        EnsureInitialLimitsCaptured();
        GameObject stoneAnchor = GameObject.Find($"point_{stoneID}");
        if (stoneAnchor != null && mapCamera != null)
        {
            // 1. Center camera coordinates on the target node plane
            Vector3 targetPos = stoneAnchor.transform.position;
            mapCamera.transform.position = new Vector3(targetPos.x, mapCamera.transform.position.y, targetPos.z);

            // 2. Set an elegant, close-up orthographic view scale
            mapCamera.orthographicSize = minOrthographicSize * 2.5f;

            // 3. Find the pin component and delegate popup tracking safely to UIManager
            InteractiveMapPin pin = stoneAnchor.GetComponentInChildren<InteractiveMapPin>();
            if (pin != null)
            {
                selectedPin = pin;

                UIManager uiMgr = UnityEngine.Object.FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
                if (uiMgr != null)
                {
                    // This automatically handles the text, activation, and real-time screen tracking
                    uiMgr.OpenMapMiniPopup(stoneID, GetPointTransform(pin.transform));
                }
            }
            ClampCameraBoundsAndZoom();
        }
    }

    public void OnBeginDrag(PointerEventData eventData) { if (mapCamera != null) dragStartWorldPos = ConvertScreenToMapWorldSpace(eventData.position); }
    public void OnDrag(PointerEventData eventData) { }

    private Vector3 ConvertScreenToMapWorldSpace(Vector2 screenPos)
    {
        if (mapCamera == null || rectTransform == null) return Vector3.zero;

        // Get the world corners of the UI map RawImage
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        // Map world corners to screen pixels using the UI camera (null if ScreenSpaceOverlay)
        Vector2 screenBL = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
        Vector2 screenTR = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]);

        float width = screenTR.x - screenBL.x;
        float height = screenTR.y - screenBL.y;

        if (Mathf.Approximately(width, 0f) || Mathf.Approximately(height, 0f))
        {
            return mapCamera.transform.position;
        }

        // Normalize screenPos to [0, 1] relative to the RawImage screen bounds
        float normX = Mathf.Clamp01((screenPos.x - screenBL.x) / width);
        float normY = Mathf.Clamp01((screenPos.y - screenBL.y) / height);

        Ray ray = mapCamera.ViewportPointToRay(new Vector3(normX, normY, 0f));
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        if (groundPlane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }

        return mapCamera.ViewportToWorldPoint(new Vector3(normX, normY, mapCamera.nearClipPlane));
    }

    public void OnPointerDown(PointerEventData eventDownData)
    {
        if (isMultiTouchActive) return;
        isPressingMap = true;
        startPressPosition = eventDownData.position;
        lastScreenPosition = eventDownData.position;
        hasDraggedCamera = false;
    }

    public void OnPointerUp(PointerEventData eventUpData)
    {
        isPressingMap = false;
        if (!hasDraggedCamera && !isMultiTouchActive)
        {
            FindClosestMarkerMathFlow(eventUpData);
        }
    }

    private void FindClosestMarkerMathFlow(PointerEventData eventData)
    {
        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out localPoint))
        {
            float normX = (localPoint.x - rectTransform.rect.x) / rectTransform.rect.width;
            float normY = (localPoint.y - rectTransform.rect.y) / rectTransform.rect.height;
            Vector2 clickViewportPos = new Vector2(normX, normY);

            InteractiveMapPin[] allActivePins = UnityEngine.Object.FindObjectsByType<InteractiveMapPin>(FindObjectsInactive.Include);
            InteractiveMapPin closestPin = null;
            float closestDistance = float.MaxValue;

            foreach (InteractiveMapPin pin in allActivePins)
            {
                Transform pointTr = GetPointTransform(pin.transform);
                if (pointTr == null) continue;

                Vector3 pinViewportPos = mapCamera.WorldToViewportPoint(pointTr.position);

                if (pinViewportPos.z > 0)
                {
                    float distance2D = Vector2.Distance(clickViewportPos, new Vector2(pinViewportPos.x, pinViewportPos.y));
                    if (distance2D < closestDistance)
                    {
                        closestDistance = distance2D;
                        closestPin = pin;
                    }
                }
            }

            if (closestPin != null && closestDistance <= clickSelectionRadius)
            {
                // ⚡ PERFORMANCE FIX: Utilizza il riferimento in cache anziché fare FindObjectOfType ad ogni click
                bool isRouteMode = (cachedRouteManager != null && cachedRouteManager.IsInModalitaPercorso());

                if (!isRouteMode) selectedPin = closestPin;
                else if (mapMiniPopupPanel != null) mapMiniPopupPanel.gameObject.SetActive(false);

                closestPin.TriggerSelection();
            }
        }
    }

    public void SetZoneRectanglesVisible(bool visible)
    {
        GameObject fieldTestMgr = GameObject.Find("Field Test Manager");
        if (fieldTestMgr == null)
        {
            foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (g != null && (g.name == "Field Test Manager" || g.name == "FieldTestManager") && g.scene.isLoaded)
                {
                    fieldTestMgr = g;
                    break;
                }
            }
        }
        if (fieldTestMgr != null) fieldTestMgr.SetActive(visible);

        Transform zonesGroup = transform.Find("Zone_Rectangles") ?? transform.Find("ZonesGroup");
        if (zonesGroup != null) zonesGroup.gameObject.SetActive(visible);

        SoccerFieldZone[] zones = UnityEngine.Object.FindObjectsByType<SoccerFieldZone>(FindObjectsInactive.Include);
        foreach (var z in zones)
        {
            if (z == null) continue;
            if (visible) z.gameObject.SetActive(true);
            var lr = z.GetComponent<LineRenderer>();
            if (lr != null) lr.enabled = visible;
        }
    }
}
