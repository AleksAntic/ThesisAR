using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Manages the Advanced Settings Panel UI for ThesisAR Phase 2.
/// Dynamically categorizes and scales all Canvas text elements at runtime across 3 distinct groups:
/// 1. Subtitles (Default: 26px, Range: 20px - 32px)
/// 2. Titles (Default: 28px, Range: 22px - 36px)
/// 3. Body & Inscriptions (Default: 18px, Range: 14px - 22px)
/// Also manages Panel Opacity (0.5 - 1.0), Language (EN/DE), GDPR Toggle, Diagnostic Toggle, and Post-Visit Survey.
/// </summary>
public class AdvancedSettingsPanel : MonoBehaviour
{
    [Header("Accessibility Font Sliders")]
    [SerializeField] private Slider subtitleFontSlider;
    [SerializeField] private Slider titleFontSlider;
    [SerializeField] private Slider bodyFontSlider;

    [Header("General UI Controls")]
    [SerializeField] private TMP_Dropdown explorationModeDropdown;
    [SerializeField] private Slider opacitySlider;
    [SerializeField] private TMP_Dropdown languageDropdown;
    [SerializeField] private Toggle gdprDataSharingToggle;
    [SerializeField] private Toggle diagnosticToggle;
    [SerializeField] private Toggle tutorialEnabledToggle;
    [SerializeField] private Button closeSettingsButton;
    [SerializeField] private Button openSurveyButton;
    [SerializeField] private Button restartTutorialButton;

    [Header("Target Text Collections by Group")]
    [SerializeField] private List<TextMeshProUGUI> subtitleTextElements = new List<TextMeshProUGUI>();
    [SerializeField] private List<TextMeshProUGUI> titleTextElements = new List<TextMeshProUGUI>();
    [SerializeField] private List<TextMeshProUGUI> bodyTextElements = new List<TextMeshProUGUI>();
    [SerializeField] private List<Image> translucentPanelImages = new List<Image>();
    [SerializeField] private GameObject diagnosticPanelOverlay;

    [Header("Dynamic Slider Value Labels & Multi-Line Preview Box")]
    [SerializeField] private TextMeshProUGUI subValueText;
    [SerializeField] private TextMeshProUGUI titleValueText;
    [SerializeField] private TextMeshProUGUI bodyValueText;
    [SerializeField] private TextMeshProUGUI opacityValueText;
    [SerializeField] private Image previewPanelImage;
    [SerializeField] private TextMeshProUGUI previewTitleText;
    [SerializeField] private TextMeshProUGUI previewSubtitleText;
    [SerializeField] private TextMeshProUGUI previewBodyText;

    private UIManager uiManager;
    private int pendingModeIndex = -1;

    void Awake()
    {
        gameObject.SetActive(false);
    }

    void Start()
    {
        if (uiManager == null) uiManager = FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);

        if (explorationModeDropdown == null) explorationModeDropdown = transform.Find("Dropdown_ExplorationMode")?.GetComponent<TMP_Dropdown>();
        if (subtitleFontSlider == null) subtitleFontSlider = transform.Find("Slider_SubtitleFont")?.GetComponent<Slider>();
        if (titleFontSlider == null) titleFontSlider = transform.Find("Slider_TitleFont")?.GetComponent<Slider>();
        if (bodyFontSlider == null) bodyFontSlider = transform.Find("Slider_BodyFont")?.GetComponent<Slider>();
        if (opacitySlider == null) opacitySlider = transform.Find("Slider_Opacity")?.GetComponent<Slider>();
        if (languageDropdown == null) languageDropdown = transform.Find("Dropdown_Language")?.GetComponent<TMP_Dropdown>();
        if (gdprDataSharingToggle == null) gdprDataSharingToggle = transform.Find("Toggle_GDPRDataSharing")?.GetComponent<Toggle>();
        if (diagnosticToggle == null) diagnosticToggle = transform.Find("Toggle_Diagnostic")?.GetComponent<Toggle>();
        if (tutorialEnabledToggle == null) tutorialEnabledToggle = transform.Find("Toggle_Tutorial")?.GetComponent<Toggle>();
        if (closeSettingsButton == null) closeSettingsButton = transform.Find("Btn_Close")?.GetComponent<Button>();
        if (openSurveyButton == null) openSurveyButton = transform.Find("Button_OpenPostVisitSurvey")?.GetComponent<Button>();
        if (restartTutorialButton == null) restartTutorialButton = FindButtonByName("Button_RestartTutorial");

        if (subValueText == null) subValueText = FindTextByName("Text_SubValue");
        if (titleValueText == null) titleValueText = FindTextByName("Text_TitleValue");
        if (bodyValueText == null) bodyValueText = FindTextByName("Text_BodyValue");
        if (opacityValueText == null) opacityValueText = FindTextByName("Text_OpacityValue");
        if (previewPanelImage == null) previewPanelImage = transform.Find("Preview_Panel")?.GetComponent<Image>();
        if (previewTitleText == null) previewTitleText = transform.Find("Preview_Panel/Text_PreviewTitle")?.GetComponent<TextMeshProUGUI>();
        if (previewSubtitleText == null) previewSubtitleText = transform.Find("Preview_Panel/Text_PreviewSub")?.GetComponent<TextMeshProUGUI>();
        if (previewBodyText == null) previewBodyText = transform.Find("Preview_Panel/Text_PreviewBody")?.GetComponent<TextMeshProUGUI>();

        CollectTextsByCategory();
        CollectTranslucentPanels();
        InitializeControls();
        UpdatePanelLocalization();
    }

    private void OnEnable()
    {
        InitializeControls();
        UpdatePanelLocalization();
        if (uiManager != null) uiManager.SyncDiagnosticUIState();
    }

    private void CollectTranslucentPanels()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        translucentPanelImages.Clear();

        string[] panelPaths = new string[]
        {
            "Stone_Popup_Panel",
            "Database_Search_Panel",
            "Map_2D_Panel",
            "Sidebar_Menu_Panel",
            "Advanced_Settings_Panel",
            "Panel_SiteHistory"
        };

        foreach (string path in panelPaths)
        {
            Transform t = canvas.transform.Find(path);
            if (t != null)
            {
                Image img = t.GetComponent<Image>();
                if (img != null && !translucentPanelImages.Contains(img))
                    translucentPanelImages.Add(img);
            }
        }
    }

    private void CollectTextsByCategory()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        subtitleTextElements.Clear();
        titleTextElements.Clear();
        bodyTextElements.Clear();

        TryAddText(canvas, "AR_Exploration_Hub/Subtitle_Panel/Text_GuideSubtitle", subtitleTextElements);
        TryAddText(canvas, "Stone_Popup_Panel/Header_Bar/Text_Stone_ID", titleTextElements);
        TryAddText(canvas, "Stone_Popup_Panel/Person_Info_Area/Text_Fullname", titleTextElements);
        TryAddText(canvas, "AR_Exploration_Hub/NotificationToast_Panel/Text_Title", titleTextElements);
        TryAddText(canvas, "Database_Search_Panel/Header_Bar/Text_Title", titleTextElements);
        TryAddText(canvas, "Map_2D_Panel/Header_Bar/Text_Title", titleTextElements);
        TryAddText(canvas, "Advanced_Settings_Panel/Header_Text", titleTextElements);

        TryAddText(canvas, "Stone_Popup_Panel/Person_Info_Area/Text_Dates", bodyTextElements);
        TryAddText(canvas, "Stone_Popup_Panel/Person_Info_Area/Text_Inmate", bodyTextElements);
        TryAddText(canvas, "AR_Exploration_Hub/NotificationToast_Panel/Text_Body", bodyTextElements);
        TryAddText(canvas, "Map_2D_Panel/Map_Mini_Popup_Panel/Text_Mini_Popup_ID", bodyTextElements);
        TryAddText(canvas, "Map_2D_Panel/Map_Legend", bodyTextElements);
        TryAddText(canvas, "ModelInspector_Panel/Btn_Reset_View/Text", bodyTextElements);

        foreach (TextMeshProUGUI text in GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text.name.StartsWith("Label_") && !bodyTextElements.Contains(text))
                bodyTextElements.Add(text);
        }
    }

    private void TryAddText(Canvas canvas, string path, List<TextMeshProUGUI> list)
    {
        Transform t = canvas.transform.Find(path);
        if (t != null)
        {
            var tmp = t.GetComponent<TextMeshProUGUI>();
            if (tmp != null && !list.Contains(tmp)) list.Add(tmp);
        }
    }

    public void InitializeControls()
    {
        if (subtitleFontSlider != null)
        {
            subtitleFontSlider.minValue = 18f;
            subtitleFontSlider.maxValue = 36f;
            float currentActualSize = (uiManager != null) ? uiManager.CurrentSubtitleFontSize : PlayerPrefs.GetFloat("Thesis_SubtitleFontSize", 24f);
            subtitleFontSlider.SetValueWithoutNotify(currentActualSize);
            if (subValueText != null) subValueText.text = $"{Mathf.RoundToInt(currentActualSize)} pt";
            subtitleFontSlider.onValueChanged.RemoveAllListeners();
            subtitleFontSlider.onValueChanged.AddListener(OnSubtitleFontChanged);
        }

        if (titleFontSlider != null)
        {
            titleFontSlider.minValue = 24f;
            titleFontSlider.maxValue = 40f;
            titleFontSlider.value = PlayerPrefs.GetFloat("Thesis_TitleFontSize", 30f);
            titleFontSlider.onValueChanged.RemoveAllListeners();
            titleFontSlider.onValueChanged.AddListener(OnTitleFontChanged);
            OnTitleFontChanged(titleFontSlider.value);
        }

        if (bodyFontSlider != null)
        {
            bodyFontSlider.minValue = 16f;
            bodyFontSlider.maxValue = 32f;
            bodyFontSlider.value = PlayerPrefs.GetFloat("Thesis_BodyFontSize", 20f);
            bodyFontSlider.onValueChanged.RemoveAllListeners();
            bodyFontSlider.onValueChanged.AddListener(OnBodyFontChanged);
            OnBodyFontChanged(bodyFontSlider.value);
        }

        if (opacitySlider != null)
        {
            opacitySlider.minValue = 0.5f;
            opacitySlider.maxValue = 1.0f;
            opacitySlider.value = PlayerPrefs.GetFloat("Thesis_PanelOpacity", 0.7f);
            opacitySlider.onValueChanged.RemoveAllListeners();
            opacitySlider.onValueChanged.AddListener(OnOpacityChanged);
            OnOpacityChanged(opacitySlider.value);
        }

        string currentLang = (uiManager != null) ? uiManager.SelectedLanguage : PlayerPrefs.GetString("Thesis_Language", "english");
        bool isGerman = string.Equals(currentLang, "german", System.StringComparison.OrdinalIgnoreCase);

        if (explorationModeDropdown != null)
        {
            UpdateExplorationDropdownOptions(isGerman);
            int currentMode = PlayerPrefs.GetInt("Thesis_GuidanceMode", PlayerPrefs.GetInt("Thesis_ExplorationMode", 0));
            explorationModeDropdown.SetValueWithoutNotify(currentMode);
            explorationModeDropdown.onValueChanged.RemoveAllListeners();
            explorationModeDropdown.onValueChanged.AddListener(OnExplorationModeChanged);
        }

        if (languageDropdown != null)
        {
            int langIdx = isGerman ? 1 : 0;
            languageDropdown.SetValueWithoutNotify(langIdx);
            languageDropdown.onValueChanged.RemoveAllListeners();
            languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
        }

        if (gdprDataSharingToggle != null)
        {
            gdprDataSharingToggle.isOn = PlayerPrefs.GetInt("Thesis_GDPRConsent", 0) == 1;
            gdprDataSharingToggle.onValueChanged.RemoveAllListeners();
            gdprDataSharingToggle.onValueChanged.AddListener(OnGDPRConsentChanged);
            OnGDPRConsentChanged(gdprDataSharingToggle.isOn);
        }

        if (diagnosticToggle != null)
        {
            diagnosticToggle.isOn = PlayerPrefs.GetInt("Thesis_DiagnosticMode", 0) == 1;
            diagnosticToggle.onValueChanged.RemoveAllListeners();
            diagnosticToggle.onValueChanged.AddListener(OnDiagnosticToggleChanged);
        }

        CoachmarkTutorialController tutorial = FindAnyObjectByType<CoachmarkTutorialController>(FindObjectsInactive.Include);
        if (tutorialEnabledToggle != null)
        {
            tutorialEnabledToggle.SetIsOnWithoutNotify(tutorial == null || tutorial.IsTutorialFeatureEnabled);
            tutorialEnabledToggle.interactable = tutorial != null;
            tutorialEnabledToggle.onValueChanged.RemoveAllListeners();
            tutorialEnabledToggle.onValueChanged.AddListener(OnTutorialEnabledChanged);
        }

        if (closeSettingsButton != null)
        {
            closeSettingsButton.onClick.RemoveAllListeners();
            closeSettingsButton.onClick.AddListener(HideSettingsPanel);
        }

        if (openSurveyButton != null)
        {
            openSurveyButton.onClick.RemoveAllListeners();
            openSurveyButton.onClick.AddListener(OnOpenSurveyClicked);
        }

        if (restartTutorialButton != null)
        {
            restartTutorialButton.interactable = tutorial != null;
            restartTutorialButton.onClick.RemoveAllListeners();
            restartTutorialButton.onClick.AddListener(OnRestartTutorialClicked);
        }
    }

    public void ApplySavedAppearance()
    {
        if (uiManager == null) uiManager = FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);

        CollectTextsByCategory();
        CollectTranslucentPanels();
        OnSubtitleFontChanged(PlayerPrefs.GetFloat("Thesis_SubtitleFontSize", 24f));
        OnTitleFontChanged(PlayerPrefs.GetFloat("Thesis_TitleFontSize", 30f));
        OnBodyFontChanged(PlayerPrefs.GetFloat("Thesis_BodyFontSize", 20f));
        OnOpacityChanged(PlayerPrefs.GetFloat("Thesis_PanelOpacity", 0.7f));
    }

    public void ShowSettingsPanel()
    {
        pendingModeIndex = -1;
        UpdatePanelLocalization();
        gameObject.SetActive(true);
    }

    public void HideSettingsPanel()
    {
        if (uiManager != null) uiManager.ClosePanelAndReturn(gameObject);
        else gameObject.SetActive(false);

        if (pendingModeIndex >= 0)
        {
            if (uiManager != null) uiManager.SetOnboardingThesisMode(pendingModeIndex, startGuidanceAfterChange: true);
            pendingModeIndex = -1;
        }
    }

    public void OnSubtitleFontChanged(float size)
    {
        PlayerPrefs.SetFloat("Thesis_SubtitleFontSize", size);
        PlayerPrefs.Save();
        if (subValueText != null) subValueText.text = $"{Mathf.RoundToInt(size)} pt";
        if (previewSubtitleText != null) previewSubtitleText.fontSize = size;
        foreach (var txt in subtitleTextElements) if (txt != null) txt.fontSize = size;
        RefreshPreviewLayout();

        if (uiManager != null) uiManager.SetSubtitleFontSize(size);
        else
        {
            var ui = UnityEngine.Object.FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
            if (ui != null) ui.SetSubtitleFontSize(size);
        }
    }

    public void OnTitleFontChanged(float size)
    {
        PlayerPrefs.SetFloat("Thesis_TitleFontSize", size);
        PlayerPrefs.Save();
        if (titleValueText != null) titleValueText.text = $"{Mathf.RoundToInt(size)} pt";
        if (previewTitleText != null) previewTitleText.fontSize = size;
        foreach (var txt in titleTextElements) if (txt != null) txt.fontSize = size;
        RefreshPreviewLayout();
    }

    public void OnBodyFontChanged(float size)
    {
        PlayerPrefs.SetFloat("Thesis_BodyFontSize", size);
        PlayerPrefs.Save();
        if (bodyValueText != null) bodyValueText.text = $"{Mathf.RoundToInt(size)} pt";
        if (previewBodyText != null) previewBodyText.fontSize = size;
        foreach (var txt in bodyTextElements) if (txt != null) txt.fontSize = size;
        Map2DController mapController = UnityEngine.Object.FindAnyObjectByType<Map2DController>(FindObjectsInactive.Include);
        mapController?.ApplyLegendFontSize(size);
        SetDropdownFontSize(explorationModeDropdown, Mathf.Min(size, 28f));
        SetDropdownFontSize(languageDropdown, Mathf.Min(size, 28f));
        RefreshPreviewLayout();
    }

    private static void SetDropdownFontSize(TMP_Dropdown dropdown, float size)
    {
        if (dropdown == null) return;
        if (dropdown.captionText != null) dropdown.captionText.fontSize = size;
        foreach (var text in dropdown.GetComponentsInChildren<TextMeshProUGUI>(true)) text.fontSize = size;
    }

    private void RefreshPreviewLayout()
    {
        SetPreviewRow(previewTitleText, 42f, 48f);
        SetPreviewRow(previewSubtitleText, 0f, 44f);
        SetPreviewRow(previewBodyText, -43f, 34f);
    }

    private static void SetPreviewRow(TextMeshProUGUI text, float y, float height)
    {
        if (text == null) return;

        RectTransform rect = text.rectTransform;
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.ForceMeshUpdate();
    }

    public void OnOpacityChanged(float alpha)
    {
        PlayerPrefs.SetFloat("Thesis_PanelOpacity", alpha);
        PlayerPrefs.Save();
        if (opacityValueText != null) opacityValueText.text = $"{Mathf.RoundToInt(alpha * 100)}%";
        if (previewPanelImage != null) previewPanelImage.color = new Color(0.12f, 0.15f, 0.20f, alpha);

        Color darkTranslucent = new Color(29f / 255f, 29f / 255f, 29f / 255f, alpha);
        foreach (var img in translucentPanelImages) if (img != null) img.color = darkTranslucent;
    }

    public void OnExplorationModeChanged(int index)
    {
        PlayerPrefs.SetInt("Thesis_ExplorationMode", index);
        pendingModeIndex = index;
    }

    public void OnLanguageChanged(int index)
    {
        if (uiManager != null)
        {
            string selectedLang = (index == 1) ? "german" : "english";
            uiManager.ChangeLanguage(selectedLang);
        }
        UpdatePanelLocalization();
    }

    public void UpdatePanelLocalization()
    {
        string lang = (uiManager != null) ? uiManager.SelectedLanguage : PlayerPrefs.GetString("Thesis_Language", "english");
        bool isGerman = string.Equals(lang, "german", System.StringComparison.OrdinalIgnoreCase);

        UpdateExplorationDropdownOptions(isGerman);

        Transform header = transform.Find("Header_Text") ?? transform.Find("Header_Bar/Text_Title") ?? transform.Find("Text_Header");
        if (header != null)
        {
            var txt = header.GetComponent<TextMeshProUGUI>();
            if (txt != null) txt.text = isGerman ? "Erweiterte Einstellungen" : "Advanced Settings";
        }

        SetLabelText("Label_ExplorationMode", isGerman ? "Erkundungsmodus:" : "Exploration Mode:");
        SetLabelText("Label_SubtitleFont", isGerman ? "Untertitel-Schriftgroeße:" : "Subtitle Font Size:");
        SetLabelText("Label_TitleFont", isGerman ? "Titel-Schriftgroeße:" : "Title Font Size:");
        SetLabelText("Label_BodyFont", isGerman ? "Inschrift-/Text-Schriftgroeße:" : "Inscription/Body Font Size:");
        SetLabelText("Label_Opacity", isGerman ? "Panel-Deckkraft:" : "Panel Opacity:");
        SetLabelText("Label_Language", isGerman ? "Sprache:" : "Language:");
        SetLabelText("Label_GDPRDataSharing", isGerman ? "Anonyme Sitzungsdaten teilen:" : "Share anonymous session data:");
        SetLabelText("Label_Diagnostic", isGerman ? "Diagnosemodus:" : "Diagnostic Mode:");
        SetLabelText("Label_Tutorial", isGerman ? "Tutorial anzeigen:" : "Show tutorial:");

        if (previewTitleText != null)
            previewTitleText.text = isGerman ? "Gedenkmal #4" : "Memorial Monument #4";

        if (previewSubtitleText != null)
            previewSubtitleText.text = isGerman ? "Fuehrungsnarration" : "Guidance narration";

        if (previewBodyText != null)
            previewBodyText.text = isGerman ? "Gewidmet dem Gedenken an die Opfer von Bergen-Belsen." : "Dedicated to the memory of victims of Bergen-Belsen.";

        RefreshPreviewLayout();

        if (openSurveyButton != null)
        {
            var surveyTxt = openSurveyButton.GetComponentInChildren<TextMeshProUGUI>();
            if (surveyTxt != null) surveyTxt.text = isGerman ? "Umfrage nach dem Besuch oeffnen" : "Open Post-Visit Survey";
        }

        if (restartTutorialButton != null)
        {
            var tutorialTxt = restartTutorialButton.GetComponentInChildren<TextMeshProUGUI>();
            if (tutorialTxt != null) tutorialTxt.text = isGerman ? "Tutorial wiederholen" : "Replay tutorial";
        }

        var allTexts = GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var t in allTexts)
        {
            if (t == null || string.IsNullOrEmpty(t.text)) continue;
            string current = t.text.Trim();
            if (current.StartsWith("Exploration Mode", System.StringComparison.OrdinalIgnoreCase)) t.text = isGerman ? "Erkundungsmodus:" : "Exploration Mode:";
            else if (current.StartsWith("Subtitle Font Size", System.StringComparison.OrdinalIgnoreCase)) t.text = isGerman ? "Untertitel-Schriftgroeße:" : "Subtitle Font Size:";
            else if (current.StartsWith("Title Font Size", System.StringComparison.OrdinalIgnoreCase)) t.text = isGerman ? "Titel-Schriftgroeße:" : "Title Font Size:";
            else if (current.StartsWith("Inscription/Body", System.StringComparison.OrdinalIgnoreCase)) t.text = isGerman ? "Inschrift-/Text-Schriftgroeße:" : "Inscription/Body Font Size:";
            else if (current.StartsWith("Panel Opacity", System.StringComparison.OrdinalIgnoreCase)) t.text = isGerman ? "Panel-Deckkraft:" : "Panel Opacity:";
            else if (current.StartsWith("Language", System.StringComparison.OrdinalIgnoreCase)) t.text = isGerman ? "Sprache:" : "Language:";
            else if (current.StartsWith("Share anonymous", System.StringComparison.OrdinalIgnoreCase) || current.StartsWith("Anonyme Sitzungsdaten", System.StringComparison.OrdinalIgnoreCase)) t.text = isGerman ? "Anonyme Sitzungsdaten teilen:" : "Share anonymous session data:";
            else if (current.StartsWith("Diagnostic Mode", System.StringComparison.OrdinalIgnoreCase)) t.text = isGerman ? "Diagnosemodus:" : "Diagnostic Mode:";
            else if (current.StartsWith("Advanced Settings", System.StringComparison.OrdinalIgnoreCase)) t.text = isGerman ? "Erweiterte Einstellungen" : "Advanced Settings";
        }
    }

    private void SetLabelText(string childName, string text)
    {
        Transform t = transform.Find(childName);
        if (t == null)
        {
            foreach (TextMeshProUGUI candidate in GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (candidate.name == childName)
                {
                    t = candidate.transform;
                    break;
                }
            }
        }
        if (t != null)
        {
            var tmp = t.GetComponent<TextMeshProUGUI>();
            if (tmp != null) tmp.text = text;
        }
    }

    private TextMeshProUGUI FindTextByName(string objectName)
    {
        foreach (TextMeshProUGUI text in GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text.name == objectName)
                return text;
        }

        return null;
    }

    private Button FindButtonByName(string objectName)
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            if (button.name == objectName)
                return button;
        }

        return null;
    }

    public void OnGDPRConsentChanged(bool isConsented)
    {
        PlayerPrefs.SetInt("Thesis_GDPRConsent", isConsented ? 1 : 0);
        PlayerPrefs.Save();
        if (ThesisManager.Instance != null) ThesisManager.Instance.UserConsentGDPR = isConsented;
    }

    public void OnTutorialEnabledChanged(bool isEnabled)
    {
        CoachmarkTutorialController tutorial = FindAnyObjectByType<CoachmarkTutorialController>(FindObjectsInactive.Include);
        if (tutorial == null) return;

        if (!isEnabled)
        {
            tutorial.SetTutorialFeatureEnabled(false);
            return;
        }

        if (uiManager != null) uiManager.RestartTutorialFromSettings();
        else tutorial.RestartTutorial();
    }

    public void OnRestartTutorialClicked()
    {
        if (uiManager != null) uiManager.RestartTutorialFromSettings();
        else FindAnyObjectByType<CoachmarkTutorialController>(FindObjectsInactive.Include)?.RestartTutorial();
    }

    public void SyncDiagnosticState()
    {
        bool isEnabled = PlayerPrefs.GetInt("Thesis_DiagnosticMode", 0) == 1;
        if (diagnosticToggle != null) diagnosticToggle.isOn = isEnabled;
        OnDiagnosticToggleChanged(isEnabled);
    }

    private GameObject FindGameObjectIncludingInactive(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go != null) return go;
        foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (g != null && g.name == name && g.scene.isLoaded && g.scene.name != null)
            {
                return g;
            }
        }
        return null;
    }

    public void OnDiagnosticToggleChanged(bool isEnabled)
    {
        PlayerPrefs.SetInt("Thesis_DiagnosticMode", isEnabled ? 1 : 0);
        PlayerPrefs.SetInt("Settings_UI_DeveloperMode", isEnabled ? 1 : 0);

        if (uiManager != null) uiManager.SyncDiagnosticUIState();
        else
        {
            var ui = UnityEngine.Object.FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
            if (ui != null) ui.SyncDiagnosticUIState();
        }
    }

    public void OnOpenSurveyClicked()
    {
        if (ThesisManager.Instance != null)
        {
            ThesisManager.Instance.OpenPostVisitSurvey();
        }
        else
        {
            Debug.LogWarning("[AdvancedSettingsPanel] Post-visit survey is unavailable because ThesisManager is missing.");
        }
    }

    private void UpdateExplorationDropdownOptions(bool isGerman)
    {
        if (explorationModeDropdown == null) return;

        int savedValue = explorationModeDropdown.value;
        explorationModeDropdown.options.Clear();
        explorationModeDropdown.options.Add(new TMP_Dropdown.OptionData(isGerman ? "Persönlich" : "Personal"));
        explorationModeDropdown.options.Add(new TMP_Dropdown.OptionData(isGerman ? "Intermediär" : "Intermediate"));
        explorationModeDropdown.options.Add(new TMP_Dropdown.OptionData(isGerman ? "Unpersönlich" : "Impersonal"));
        explorationModeDropdown.SetValueWithoutNotify(savedValue);
        explorationModeDropdown.RefreshShownValue();
    }
}

// Recompile trigger
