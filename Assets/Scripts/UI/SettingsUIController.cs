using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the Settings Panel UI layer. 
/// Bridges user graphic inputs (Slider, Toggles) with local storage via PersistenceManager,
/// forcing real-time text mesh updates and layout rebuilding to fix runtime font sizing bugs.
/// Maintained strictly in English.
/// </summary>
public class SettingsUIController : MonoBehaviour
{
    [Header("💾 Core Dependencies")]
    [SerializeField] private PersistenceManager persistenceManager;
    [SerializeField] private GameObject diagnosticUIPanel; // Reference to the entire ArDiagnostic parent GameObject

    [Header("🎛️ UI Interactive Elements")]
    [SerializeField] private Slider subtitleScaleSlider;
    [SerializeField] private Toggle worldSpaceUIToggle;
    [SerializeField] private Toggle developerModeToggle;
    [SerializeField] private TMP_Dropdown thesisModeDropdown;


    [Header("👁️ Preview Fields (Optional)")]
    [SerializeField] private TextMeshProUGUI subtitlePreviewText;

    // Local cached variables matching PlayerPrefs keys inside PersistenceManager
    private bool useWorldSpacePopups = false;
    private float subtitleTextScale = 1.0f;
    private bool isDeveloperModeActive = true;

    private const string DevModePrefsKey = "Settings_UI_DeveloperMode";

    void Awake()
    {
        // Ensure panel is hidden on boot so it never overlaps onboarding panel
        gameObject.SetActive(false);
    }

    void Start()
    {
        // Fail-safe dependency lookup
        if (persistenceManager == null)
            persistenceManager = UnityEngine.Object.FindAnyObjectByType<PersistenceManager>(FindObjectsInactive.Include);

        LoadCachedSettings();
        
        // Dynamic fail-safe runtime wire for preview text if inspector reference was lost
        if (subtitlePreviewText == null)
        {
            GameObject previewGo = GameObject.Find("textPreview");
            if (previewGo != null)
            {
                subtitlePreviewText = previewGo.GetComponent<TextMeshProUGUI>();
                if (subtitlePreviewText != null)
                {
                    Debug.Log("[SettingsUIController] Dynamically resolved lost 'textPreview' reference at runtime.");
                }
            }
        }

        InitializeUIElements();
        RegisterUIEventListeners();
    }

    /// <summary>
    /// Reads stored settings directly from PlayerPrefs or falls back to standard baselines.
    /// </summary>
    private void LoadCachedSettings()
    {
        useWorldSpacePopups = PlayerPrefs.GetInt("Settings_UI_WorldSpace", 0) == 1;
        subtitleTextScale = PlayerPrefs.GetFloat("Settings_UI_SubtitleScale", 1.0f);
        isDeveloperModeActive = PlayerPrefs.GetInt(DevModePrefsKey, 0) == 1;
    }

    /// <summary>
    /// Synchronizes the graphic status of sliders and toggles to match loaded data indices.
    /// </summary>
    private void InitializeUIElements()
    {
        if (subtitleScaleSlider != null)
        {
            subtitleScaleSlider.minValue = 1.0f;
            subtitleScaleSlider.maxValue = 2.0f;
            subtitleScaleSlider.value = subtitleTextScale;
        }

        if (worldSpaceUIToggle != null)
        {
            worldSpaceUIToggle.isOn = useWorldSpacePopups;
        }

        if (developerModeToggle != null)
        {
            developerModeToggle.isOn = isDeveloperModeActive;
        }

        if (thesisModeDropdown != null)
        {
            // Sync with PlayerPrefs, default to index 0 (Personal Experience)
            int savedIndex = PlayerPrefs.GetInt("Thesis_GuidanceMode", 0);
            thesisModeDropdown.value = savedIndex;
        }

        // Apply initial visual states
        UpdateSubtitlePreview(subtitleTextScale);
        ApplyDeveloperModeState(isDeveloperModeActive);

    }

    /// <summary>
    /// Registers programmatic listeners to intercept user finger touch and click inputs.
    /// </summary>
    private void RegisterUIEventListeners()
    {
        if (subtitleScaleSlider != null)
            subtitleScaleSlider.onValueChanged.AddListener(HandleSubtitleScaleChanged);

        if (worldSpaceUIToggle != null)
            worldSpaceUIToggle.onValueChanged.AddListener(HandleDisplayModeChanged);

        if (developerModeToggle != null)
            developerModeToggle.onValueChanged.AddListener(HandleDeveloperModeChanged);

        if (thesisModeDropdown != null)
            thesisModeDropdown.onValueChanged.AddListener(HandleThesisModeChanged);
    }


    /// <summary>
    /// Triggered automatically when user drags the accessibility font slider.
    /// </summary>
    private void HandleSubtitleScaleChanged(float rawValue)
    {
        subtitleTextScale = rawValue;
        UpdateSubtitlePreview(subtitleTextScale);

        UIManager uiManager2 = UnityEngine.Object.FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
        if (uiManager2 != null)
        {
            uiManager2.UpdateSubtitleScale(subtitleTextScale);
        }

        if (persistenceManager != null)
        {
            persistenceManager.SaveAccessibilitySettings(useWorldSpacePopups, subtitleTextScale);
        }
    }

    /// <summary>
    /// Triggered automatically when user toggles between 2D Screen overlays and 3D World labels.
    /// </summary>
    private void HandleDisplayModeChanged(bool useWorldSpace)
    {
        useWorldSpacePopups = useWorldSpace;

        if (persistenceManager != null)
        {
            persistenceManager.SaveAccessibilitySettings(useWorldSpacePopups, subtitleTextScale);
        }

        Debug.Log($"[Settings] Display mode updated. Use World Space: {useWorldSpacePopups}");
    }

    /// <summary>
    /// Triggered automatically when user clicks the Developer Diagnostics toggle box.
    /// </summary>
    private void HandleDeveloperModeChanged(bool isActive)
    {
        isDeveloperModeActive = isActive;

        PlayerPrefs.SetInt(DevModePrefsKey, isDeveloperModeActive ? 1 : 0);
        PlayerPrefs.Save();

        ApplyDeveloperModeState(isDeveloperModeActive);
    }

    /// <summary>
    /// Dynamically enforces font size alterations over a visual preview text element.
    /// Incorporates safe mesh refreshes and container re-layouts to bypass native Unity UI freezing bugs.
    /// </summary>
    private void UpdateSubtitlePreview(float scale)
    {
        if (subtitlePreviewText != null)
        {
            // FIX 1: Explicitly disable TMPro auto-sizing, otherwise any manual font size code mutations are entirely ignored
            subtitlePreviewText.enableAutoSizing = false;

            // Base size 24px multiplied by the ergonomics scaling factor (1.0x - 2.0x)
            subtitlePreviewText.fontSize = 24f * scale;
            subtitlePreviewText.text = $"Subtitle Preview (Size: {(scale * 100f):F0}%)";

            // FIX 2: Force immediate mathematical rebuild of the structural text vertex geometries
            subtitlePreviewText.ForceMeshUpdate();

            // FIX 3: Force the parent layout group system to instantly recalculate its bounds to prevent text clipping
            if (subtitlePreviewText.transform.parent is RectTransform parentRect)
            {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            }
        }
    }

    /// <summary>
    /// Switches visibility parameters for the live telemetry diagnostic canvas panels instantly.
    /// </summary>
    private void ApplyDeveloperModeState(bool isVisible)
    {
        if (diagnosticUIPanel != null)
        {
            diagnosticUIPanel.SetActive(isVisible);
        }

        var advSettings = UnityEngine.Object.FindAnyObjectByType<AdvancedSettingsPanel>(FindObjectsInactive.Include);
        if (advSettings != null)
        {
            advSettings.OnDiagnosticToggleChanged(isVisible);
        }
    }

    private void HandleThesisModeChanged(int index)
    {
        ThesisManager.GuidanceMode mode;
        if (index == 0) mode = ThesisManager.GuidanceMode.Personal;
        else if (index == 1) mode = ThesisManager.GuidanceMode.Intermediate;
        else mode = ThesisManager.GuidanceMode.Impersonal;

        if (ThesisManager.Instance != null)
        {
            ThesisManager.Instance.SetGuidanceMode(mode);
            PlayerPrefs.SetInt("Thesis_GuidanceMode", index);
            PlayerPrefs.Save();
            Debug.Log($"[Settings] Changed thesis guidance mode to: {mode} (Index: {index})");
        }
    }

    public void SyncDropdownSelection(ThesisManager.GuidanceMode mode)
    {
        if (thesisModeDropdown == null) return;
        int index = 0;
        if (mode == ThesisManager.GuidanceMode.Intermediate) index = 1;
        else if (mode == ThesisManager.GuidanceMode.Impersonal) index = 2;

        thesisModeDropdown.onValueChanged.RemoveListener(HandleThesisModeChanged);
        thesisModeDropdown.value = index;
        thesisModeDropdown.onValueChanged.AddListener(HandleThesisModeChanged);
    }

    void OnDestroy()

    {
        if (subtitleScaleSlider != null) subtitleScaleSlider.onValueChanged.RemoveListener(HandleSubtitleScaleChanged);
        if (worldSpaceUIToggle != null) worldSpaceUIToggle.onValueChanged.RemoveListener(HandleDisplayModeChanged);
        if (developerModeToggle != null) developerModeToggle.onValueChanged.RemoveListener(HandleDeveloperModeChanged);
        if (thesisModeDropdown != null) thesisModeDropdown.onValueChanged.RemoveListener(HandleThesisModeChanged);
    }
}