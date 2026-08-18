using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// Controls the UI modal for interactive 3D model inspection.
/// Renders the target stone model onto a RawImage using a dedicated camera and RenderTexture,
/// and handles drag rotation and mouse scroll/pinch zoom.
/// Compatible with Unity's Event System (New Input System UI Module).
/// </summary>
public class ModelInspectorUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
{
    private static ModelInspectorUI instance;
    public static ModelInspectorUI Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Object.FindAnyObjectByType<ModelInspectorUI>(FindObjectsInactive.Include);
            }
            return instance;
        }
        private set => instance = value;
    }

    [Header("📺 UI Viewport Bindings")]
    [SerializeField] private GameObject inspectorPanel;
    public GameObject GetInspectorPanel() => inspectorPanel;
    [SerializeField] private RawImage viewportRawImage;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button zoomInButton;
    [SerializeField] private Button zoomOutButton;
    [SerializeField] private Button resetViewButton;
    [SerializeField] private TMPro.TextMeshProUGUI titleText;

    [Header("🎥 Dedicated 3D Setup")]
    [SerializeField] private Camera inspectorCamera;
    [SerializeField] private Transform modelContainer;
    [SerializeField] private Light inspectorLight;
    [SerializeField] private string inspectorLayerName = "UI"; // Render on a separate layer if desired

    [Header("⚙️ Interaction Sensitivity")]
    [SerializeField] private float rotationSpeed = 0.4f;
    [SerializeField] private float autoRotationSpeed = 12f;
    [SerializeField] private float zoomSpeed = 0.05f;
    [SerializeField] private float minZoom = 0.05f; // Expanded zoom range for close-up epigraph inspection
    [SerializeField] private float maxZoom = 5.0f;
    [SerializeField] private float maxPanOffset = 0.75f;

    private GameObject currentSpawnedModel;
    private float currentZoom = 1.5f;
    private float initialModelScale = 1.0f;
    private Vector2 rotationAngles = Vector2.zero;
    private Vector2 panOffset = Vector2.zero;
    private Vector3 baseCameraLocalPosition = new Vector3(0f, 0f, -3.0f);
    private Vector3 currentModelBoundsExtents = Vector3.one * 0.5f;
    private int inspectorLayer;
    private Vector3 lastMousePosition;
    private float lastUnloadTime = -999f;
    private const float unloadCooldownSeconds = 5.0f;
    private bool isClosing = false;

    private void Awake()
    {
        if (instance == null) instance = this;

        int layerIndex = !string.IsNullOrWhiteSpace(inspectorLayerName)
            ? LayerMask.NameToLayer(inspectorLayerName)
            : -1;
        if (layerIndex == -1) layerIndex = LayerMask.NameToLayer("3DInspector");
        if (layerIndex == -1) layerIndex = 30; // Dedicated layer 30 for 3D model inspector offscreen rendering
        inspectorLayer = layerIndex;

        // Ensure modelContainer & inspectorCamera are auto-wired and isolated offscreen at (1000, 1000, 1000)
        if (modelContainer == null)
        {
            var foundContainer = transform.Find("ModelContainer") ?? transform.Find("Container") ?? transform.Find("ModelPivot");
            if (foundContainer != null) modelContainer = foundContainer;
            else
            {
                GameObject containerGo = new GameObject("Inspector_ModelContainer");
                containerGo.transform.position = new Vector3(1000f, 1000f, 1000f);
                modelContainer = containerGo.transform;
            }
        }
        else if (modelContainer.position.magnitude < 100f)
        {
            modelContainer.position = new Vector3(1000f, 1000f, 1000f);
        }

        if (inspectorCamera == null)
        {
            var existingCam = modelContainer.GetComponentInChildren<Camera>() ?? GetComponentInChildren<Camera>(true);
            if (existingCam != null) inspectorCamera = existingCam;
            else
            {
                GameObject camGo = new GameObject("Inspector_Camera", typeof(Camera));
                camGo.transform.SetParent(modelContainer.parent != null ? modelContainer.parent : modelContainer, false);
                camGo.transform.position = modelContainer.position + new Vector3(0f, 0f, -3f);
                camGo.transform.LookAt(modelContainer.position);
                inspectorCamera = camGo.GetComponent<Camera>();
            }
        }
        else if (inspectorCamera.transform.position.magnitude < 100f)
        {
            inspectorCamera.transform.position = modelContainer.position + new Vector3(0f, 0f, -3f);
            inspectorCamera.transform.LookAt(modelContainer.position);
        }

        if (inspectorCamera != null)
        {
            inspectorCamera.clearFlags = CameraClearFlags.SolidColor;
            inspectorCamera.backgroundColor = new Color(0.12f, 0.14f, 0.18f, 1f);
            inspectorCamera.cullingMask = 1 << inspectorLayer;
            inspectorCamera.enabled = false; // Disable camera when modal is closed to stop URP render graph job exceptions

            if (inspectorCamera.targetTexture == null)
            {
                RenderTexture rt = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32);
                rt.depth = 24;
                rt.Create();
                inspectorCamera.targetTexture = rt;
                if (viewportRawImage != null)
                {
                    viewportRawImage.texture = rt;
                    viewportRawImage.raycastTarget = true;
                }
            }
        }

        // Ensure dedicated directional light at modelContainer offscreen position on inspectorLayer
        if (modelContainer != null)
        {
            if (inspectorLight == null)
            {
                var existingLight = modelContainer.GetComponentInChildren<Light>();
                if (existingLight != null) inspectorLight = existingLight;
                else
                {
                    GameObject lightGo = new GameObject("Inspector_Directional_Light", typeof(Light));
                    lightGo.transform.SetParent(modelContainer.parent != null ? modelContainer.parent : modelContainer, false);
                    lightGo.transform.position = modelContainer.position + new Vector3(2f, 3f, -2f);
                    lightGo.transform.LookAt(modelContainer.position);
                    inspectorLight = lightGo.GetComponent<Light>();
                }
            }

            if (inspectorLight != null)
            {
                inspectorLight.type = LightType.Directional;
                inspectorLight.intensity = 2.0f;
                inspectorLight.color = Color.white;
                inspectorLight.gameObject.layer = inspectorLayer;
                inspectorLight.cullingMask = 1 << inspectorLayer;
                inspectorLight.enabled = true;
            }
        }

        if (closeButton == null && inspectorPanel != null)
        {
            foreach (Button b in inspectorPanel.GetComponentsInChildren<Button>(true))
            {
                if (b.name.Equals("buttonClose", System.StringComparison.OrdinalIgnoreCase) ||
                    b.name.Equals("CloseButton", System.StringComparison.OrdinalIgnoreCase) ||
                    b.name.Equals("Btn_Close", System.StringComparison.OrdinalIgnoreCase) ||
                    b.name.Contains("Close"))
                {
                    closeButton = b;
                    break;
                }
            }
        }

        if (zoomInButton == null && inspectorPanel != null)
        {
            foreach (Button b in inspectorPanel.GetComponentsInChildren<Button>(true))
            {
                if (b.name.Equals("buttonZoomIn", System.StringComparison.OrdinalIgnoreCase) ||
                    b.name.Equals("Btn_ZoomIn", System.StringComparison.OrdinalIgnoreCase) ||
                    b.name.Equals("ZoomIn", System.StringComparison.OrdinalIgnoreCase) ||
                    b.name.Equals("+"))
                {
                    zoomInButton = b;
                    break;
                }
            }
        }

        if (zoomOutButton == null && inspectorPanel != null)
        {
            foreach (Button b in inspectorPanel.GetComponentsInChildren<Button>(true))
            {
                if (b.name.Equals("buttonZoomOut", System.StringComparison.OrdinalIgnoreCase) ||
                    b.name.Equals("Btn_ZoomOut", System.StringComparison.OrdinalIgnoreCase) ||
                    b.name.Equals("ZoomOut", System.StringComparison.OrdinalIgnoreCase) ||
                    b.name.Equals("-"))
                {
                    zoomOutButton = b;
                    break;
                }
            }
        }

        if (titleText == null && inspectorPanel != null)
        {
            foreach (var txt in inspectorPanel.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
            {
                if (txt.transform.GetComponentInParent<Button>() != null) continue;
                titleText = txt;
                break;
            }
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseInspector);
        }

        if (zoomInButton != null)
        {
            zoomInButton.onClick.RemoveAllListeners();
            zoomInButton.onClick.AddListener(ZoomIn);
        }

        if (zoomOutButton != null)
        {
            zoomOutButton.onClick.RemoveAllListeners();
            zoomOutButton.onClick.AddListener(ZoomOut);
        }

        ResolveResetViewButton();

        if (inspectorPanel != null) inspectorPanel.SetActive(false);
    }

    /// <summary>
    /// Opens the 3D model viewer overlay and instantiates the designated stone mesh.
    /// </summary>
    public void OpenInspector(string stoneId)
    {
        if (inspectorPanel == null) return;

        // Auto-wire buttons if added dynamically
        if (closeButton == null || zoomInButton == null || zoomOutButton == null)
        {
            foreach (Button b in inspectorPanel.GetComponentsInChildren<Button>(true))
            {
                if (closeButton == null && (b.name.Equals("buttonClose", System.StringComparison.OrdinalIgnoreCase) || b.name.Contains("Close")))
                {
                    closeButton = b;
                    closeButton.onClick.RemoveAllListeners();
                    closeButton.onClick.AddListener(CloseInspector);
                }
                else if (zoomInButton == null && (b.name.Equals("buttonZoomIn", System.StringComparison.OrdinalIgnoreCase) || b.name.Contains("ZoomIn") || b.name == "+"))
                {
                    zoomInButton = b;
                    zoomInButton.onClick.RemoveAllListeners();
                    zoomInButton.onClick.AddListener(ZoomIn);
                }
                else if (zoomOutButton == null && (b.name.Equals("buttonZoomOut", System.StringComparison.OrdinalIgnoreCase) || b.name.Contains("ZoomOut") || b.name == "-"))
                {
                    zoomOutButton = b;
                    zoomOutButton.onClick.RemoveAllListeners();
                    zoomOutButton.onClick.AddListener(ZoomOut);
                }
            }
        }

        if (titleText == null && inspectorPanel != null)
        {
            foreach (var txt in inspectorPanel.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
            {
                if (txt.transform.GetComponentInParent<Button>() != null) continue;
                titleText = txt;
                break;
            }
        }

        // Formulate a clean, human-readable title for the 3D model inspector
        string formattedTitle = $"Memorial Stone {stoneId}";
        var dataMgr = UnityEngine.Object.FindAnyObjectByType<MemorialDataManager>(FindObjectsInactive.Include);
        if (dataMgr != null)
        {
            object data = dataMgr.GetDataByID(stoneId);
            if (data is MemorialDataManager.MemorialStone stone)
            {
                string personName = (stone.persons != null && stone.persons.Count > 0)
                    ? $"{stone.persons[0].forename} {stone.persons[0].surname}".Trim()
                    : "";
                formattedTitle = string.IsNullOrEmpty(personName) ? $"Memorial Stone {stone.id}" : $"Memorial Stone {stone.id} — {personName}";
            }
            else if (data is MemorialDataManager.MassGrave grave)
            {
                formattedTitle = !string.IsNullOrEmpty(grave.description) ? grave.description : $"Mass Grave {grave.id}";
            }
            else if (data is MemorialDataManager.OtherMemorial memorial)
            {
                formattedTitle = !string.IsNullOrEmpty(memorial.description) ? memorial.description : $"Memorial {memorial.id}";
            }
        }

        if (titleText != null)
        {
            titleText.text = formattedTitle;
        }

        inspectorPanel.SetActive(true);
        if (inspectorCamera != null) inspectorCamera.enabled = true;
        if (inspectorLight != null) inspectorLight.enabled = true;

        Debug.Log($"[UI Event Trace] ModelInspector_Panel OPENED for Stone: '{stoneId}' ({formattedTitle})");
        ResetView();
        StartCoroutine(LoadAndDisplayModel(stoneId));
    }

    public void CloseInspector()
    {
        if (isClosing) return;
        isClosing = true;

        ClearModelContainer();
        if (currentSpawnedModel != null)
        {
            Destroy(currentSpawnedModel);
            currentSpawnedModel = null;
        }

        if (inspectorCamera != null)
        {
            inspectorCamera.enabled = false;
        }

        if (inspectorPanel != null)
        {
            inspectorPanel.SetActive(false);
        }

        // ⚡ UX FIX: Instead of calling CloseCurrentAndReturn (which can pop the wrong panel if the stack is corrupted),
        // we explicitly tell the UIManager that the 3D Inspector is closed, so it can restore the Memorial Detail Panel safely.
        var uiMgr = UnityEngine.Object.FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
        if (uiMgr != null)
        {
            uiMgr.RestoreMemorialDetailAfterInspector();
        }

        panOffset = Vector2.zero;
        rotationAngles = Vector2.zero;
        if (inspectorLight != null) inspectorLight.enabled = false;
        
        // Debounced memory cleanup: Runs at most once every 5 seconds to prevent micro-stutter on rapid open/close
        if (Time.time - lastUnloadTime > unloadCooldownSeconds)
        {
            lastUnloadTime = Time.time;
            Resources.UnloadUnusedAssets();
        }

        Debug.Log("[UI Event Trace] ModelInspector_Panel CLOSED");

        isClosing = false;
    }

    private IEnumerator LoadAndDisplayModel(string stoneId)
    {
        ClearModelContainer();

        // 1. Asynchronously load the stone prefab from Resources/Stones/
        ResourceRequest request = Resources.LoadAsync<GameObject>($"Stones/{stoneId}");
        yield return request;

        GameObject prefab = request.asset as GameObject;
        if (prefab == null)
        {
            // Imported GLB names retain their source casing (for example MG11a),
            // while memorial data can use MG11A. Try the common casing variants.
            string lowerId = stoneId != null ? stoneId.ToLowerInvariant() : string.Empty;
            string upperId = stoneId != null ? stoneId.ToUpperInvariant() : string.Empty;
            prefab = Resources.Load<GameObject>($"Stones/{lowerId}") ??
                     Resources.Load<GameObject>($"Stones/{upperId}");
        }
        if (prefab == null)
        {
            foreach (GameObject candidate in Resources.LoadAll<GameObject>("Stones"))
            {
                if (candidate != null && string.Equals(candidate.name, stoneId, System.StringComparison.OrdinalIgnoreCase))
                {
                    prefab = candidate;
                    break;
                }
            }
        }
        if (prefab == null)
        {
            prefab = Resources.Load<GameObject>($"Stones/{stoneId}-1") ??
                     Resources.Load<GameObject>($"Stones/{stoneId}_1") ??
                     Resources.Load<GameObject>($"Stones/{stoneId}-2") ??
                     Resources.Load<GameObject>($"Stones/{stoneId}_2");
        }
#if UNITY_EDITOR
        if (prefab == null)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/Stones_Source", "Assets/Resources/Stones" });
            foreach (string guid in guids)
            {
                string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                string fileName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                if (string.Equals(fileName, stoneId, System.StringComparison.OrdinalIgnoreCase) ||
                    fileName.StartsWith(stoneId + "_", System.StringComparison.OrdinalIgnoreCase) ||
                    fileName.StartsWith(stoneId + " ", System.StringComparison.OrdinalIgnoreCase))
                {
                    prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    if (prefab != null) break;
                }
            }
        }
#endif
        if (prefab != null)
        {
            MeshFilter mf = prefab.GetComponentInChildren<MeshFilter>(true);
            SkinnedMeshRenderer smr = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if ((mf != null && mf.sharedMesh == null) && (smr != null && smr.sharedMesh == null))
            {
                Debug.LogWarning($"[ModelInspectorUI] Asset '{stoneId}' has a missing mesh reference. Attempting fallback...");
                prefab = null;
            }
        }

        if (prefab != null)
        {
            currentSpawnedModel = Instantiate(prefab, modelContainer);
            FrameModelInViewport(currentSpawnedModel);
        }
        else
        {
            // Attempt GitHub dynamic GLB asset download as secondary fallback
            bool downloadComplete = false;
            GameObject downloadedObj = null;

            GitHubAssetDownloader downloader = GitHubAssetDownloader.Instance;
            if (downloader != null)
            {
                downloader.DownloadOrCacheModelAsync(stoneId, (result) =>
                {
                    downloadedObj = result;
                    downloadComplete = true;
                });

                float timeoutTimer = 0f;
                while (!downloadComplete && timeoutTimer < 10f)
                {
                    timeoutTimer += Time.deltaTime;
                    yield return null;
                }
            }

            if (downloadedObj != null)
            {
                currentSpawnedModel = downloadedObj;
                currentSpawnedModel.transform.SetParent(modelContainer, false);
                FrameModelInViewport(currentSpawnedModel);
            }
            else
            {
                Debug.LogWarning($"[ModelInspectorUI] 3D model unavailable for '{stoneId}'. Spawning 3D placeholder slab.");
                currentSpawnedModel = SpawnFallbackStoneCube(stoneId);
                FrameModelInViewport(currentSpawnedModel);
            }
        }

        currentSpawnedModel.transform.localRotation = Quaternion.identity;

        // 2. Set layers recursively so the main camera does not see this model
        SetLayerRecursively(currentSpawnedModel, inspectorLayer);

        // 3. Immediately frame, scale and position the newly loaded model
        UpdateCameraZoom();
    }

    private void ClearModelContainer()
    {
        if (currentSpawnedModel != null)
        {
            Destroy(currentSpawnedModel);
            currentSpawnedModel = null;
        }

        // Clean up any remaining children
        if (modelContainer != null)
        {
            foreach (Transform child in modelContainer)
            {
                Destroy(child.gameObject);
            }
        }
    }

    private void FrameModelInViewport(GameObject model)
    {
        if (model == null) return;

        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        MeshFilter[] filters = model.GetComponentsInChildren<MeshFilter>(true);
        if (filters != null && filters.Length > 0)
        {
            Bounds localBounds = new Bounds();
            bool hasBounds = false;

            foreach (var mf in filters)
            {
                if (mf.sharedMesh != null)
                {
                    Bounds b = mf.sharedMesh.bounds;
                    Vector3 localCenter = model.transform.InverseTransformPoint(mf.transform.TransformPoint(b.center));

                    if (!hasBounds)
                    {
                        localBounds = new Bounds(localCenter, b.size);
                        hasBounds = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(new Bounds(localCenter, b.size));
                    }
                }
            }

            if (hasBounds)
            {
                model.transform.localPosition = -localBounds.center;
                float maxDimension = Mathf.Max(localBounds.size.x, localBounds.size.y, localBounds.size.z);
                initialModelScale = (maxDimension > 0.001f) ? (1.5f / maxDimension) : 1.0f;
                currentModelBoundsExtents = localBounds.extents;
            }
            else
            {
                initialModelScale = 1.0f;
                currentModelBoundsExtents = Vector3.one * 0.5f;
            }
        }
        else
        {
            initialModelScale = 1.0f;
            currentModelBoundsExtents = Vector3.one * 0.5f;
        }
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;
        int layerToUse = newLayer;
        if (!string.IsNullOrEmpty(inspectorLayerName))
        {
            int parsedLayer = LayerMask.NameToLayer(inspectorLayerName);
            if (parsedLayer >= 0) layerToUse = parsedLayer;
        }
        obj.layer = layerToUse;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layerToUse);
        }
    }

    // --- INTERACTION CALLBACKS ---

    /// <summary>
    /// Event System callback for mouse wheel scrolling to zoom in/out.
    /// </summary>
    public void OnScroll(PointerEventData eventData)
    {
        currentZoom -= eventData.scrollDelta.y * zoomSpeed;
        currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);
        UpdateCameraZoom();
    }

    public void ZoomIn()
    {
        currentZoom = Mathf.Clamp(currentZoom - 0.25f, minZoom, maxZoom);
        UpdateCameraZoom();
    }

    public void ZoomOut()
    {
        currentZoom = Mathf.Clamp(currentZoom + 0.25f, minZoom, maxZoom);
        UpdateCameraZoom();
    }

    public void ResetView()
    {
        rotationAngles = new Vector2(-15f, 0f);
        panOffset = Vector2.zero;
        currentZoom = 1.5f;
        if (modelContainer != null)
            modelContainer.localRotation = Quaternion.Euler(rotationAngles.x, rotationAngles.y, 0f);
        UpdateCameraZoom();
    }

    private void ResolveResetViewButton()
    {
        if (resetViewButton == null && inspectorPanel != null)
        {
            foreach (Button button in inspectorPanel.GetComponentsInChildren<Button>(true))
            {
                if (button.name.Contains("Reset"))
                {
                    resetViewButton = button;
                    break;
                }
            }
        }

        if (resetViewButton == null && inspectorPanel != null)
        {
            GameObject buttonObject = new GameObject("Btn_Reset_View", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(inspectorPanel.transform, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-28f, -28f);
            rect.sizeDelta = new Vector2(150f, 52f);
            buttonObject.GetComponent<Image>().color = new Color(0.12f, 0.22f, 0.29f, 0.96f);
            resetViewButton = buttonObject.GetComponent<Button>();

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(buttonObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            Text label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = "Reset view";
            label.fontSize = 18;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
        }

        if (resetViewButton != null)
        {
            resetViewButton.onClick.RemoveAllListeners();
            resetViewButton.onClick.AddListener(ResetView);
        }
    }

    private void UpdateCameraZoom()
    {
        if (modelContainer == null) return;

        // SINGLE SOURCE OF TRUTH: Zoom scales modelContainer.localScale directly
        float baseScale = initialModelScale > 0.0001f ? initialModelScale : 1.0f;
        float zoomMultiplier = 1.5f / Mathf.Clamp(currentZoom, minZoom, maxZoom);
        modelContainer.localScale = Vector3.one * (baseScale * zoomMultiplier);

        // Position modelContainer relative to offscreen base (1000, 1000, 1000) using panOffset
        Vector3 basePos = new Vector3(1000f, 1000f, 1000f);
        modelContainer.position = basePos + new Vector3(panOffset.x, panOffset.y, 0f);

        if (inspectorCamera != null)
        {
            inspectorCamera.transform.position = basePos + baseCameraLocalPosition;
            inspectorCamera.transform.LookAt(basePos);
        }
    }

    public void OnBeginDrag(PointerEventData eventData) { }
    public void OnEndDrag(PointerEventData eventData) { }

    public void OnDrag(PointerEventData eventData)
    {
        if (modelContainer == null) return;

        bool isMiddlePressed = Mouse.current != null && Mouse.current.middleButton.isPressed;
        bool isRightPressed = Mouse.current != null && Mouse.current.rightButton.isPressed;
        bool isShiftPressed = Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);

        bool isPanMode = isMiddlePressed || isRightPressed || isShiftPressed ||
                         eventData.button == PointerEventData.InputButton.Middle ||
                         eventData.button == PointerEventData.InputButton.Right;

        if (!isPanMode)
        {
            // Left Click Drag: Rotate
            rotationAngles.y -= eventData.delta.x * rotationSpeed;
            rotationAngles.x += eventData.delta.y * rotationSpeed;
            rotationAngles.x = Mathf.Clamp(rotationAngles.x, -80f, 80f);

            modelContainer.localRotation = Quaternion.Euler(rotationAngles.x, rotationAngles.y, 0f);
        }
        else
        {
            // Middle-Click / Shift+Drag / Right-Click: Pan
            float panSensitivity = 0.012f / Mathf.Max(0.1f, currentZoom);
            panOffset.x += eventData.delta.x * panSensitivity;
            panOffset.y += eventData.delta.y * panSensitivity;

            float maxPanX = Mathf.Min((currentModelBoundsExtents.x * modelContainer.localScale.x) + 3.0f, maxPanOffset);
            float maxPanY = Mathf.Min((currentModelBoundsExtents.y * modelContainer.localScale.y) + 3.0f, maxPanOffset);
            panOffset.x = Mathf.Clamp(panOffset.x, -maxPanX, maxPanX);
            panOffset.y = Mathf.Clamp(panOffset.y, -maxPanY, maxPanY);

            UpdateCameraZoom();
        }
    }

    private void Update()
    {
        if (inspectorPanel == null || !inspectorPanel.activeSelf) return;

        if (modelContainer != null && currentSpawnedModel != null)
        {
            rotationAngles.y += autoRotationSpeed * Time.unscaledDeltaTime;
            modelContainer.localRotation = Quaternion.Euler(rotationAngles.x, rotationAngles.y, 0f);
        }

        // Native New Input System Mouse Scroll Wheel fallback
        if (Mouse.current != null)
        {
            float scrollY = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) > 0.01f)
            {
                float delta = scrollY > 0f ? 0.15f : -0.15f;
                currentZoom = Mathf.Clamp(currentZoom - delta, minZoom, maxZoom);
                UpdateCameraZoom();
            }
        }
    }

    private GameObject SpawnFallbackStoneCube(string stoneId)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = $"[Fallback_Stone]_{stoneId}";
        cube.transform.SetParent(modelContainer, false);
        cube.transform.localScale = new Vector3(1.2f, 1.8f, 0.4f);

        var mr = cube.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.material = new Material(Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"));
            mr.material.color = new Color(0.45f, 0.45f, 0.48f);
        }

        UIManager uiMgr = Object.FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
        if (uiMgr != null)
        {
            uiMgr.ShowNotificationToast("Model Offline", $"3D model for '{stoneId}' unavailable. Displaying 3D placeholder.");
        }

        return cube;
    }
}
