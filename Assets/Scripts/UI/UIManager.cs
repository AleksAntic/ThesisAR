using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Manages UI for displaying memorial information, handling application navigation dynamically,
/// and providing clean dynamic testing routes directly from the pre-loaded memory database.
/// Implements an advanced mobile-optimized instant search + token-based faceted filtering system.
/// Integrates programmatic hooks for localized English vocal narration playback controls.
/// All internal code, variables, and logs are strictly maintained in English.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Original UI References (Data Display)")]
    [SerializeField] private Canvas uiCanvas;
    [SerializeField] private GameObject memorialDetailPanel;
    [SerializeField] private CanvasGroup memorialDetailCanvasGroup;
    [SerializeField] private RectTransform memorialDetailRect;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI personsListText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button nextPersonButton;
    [SerializeField] private Button prevPersonButton;
    [SerializeField] private Button navigateHereButton;
    [SerializeField] private Button prevStoneButton;
    [SerializeField] private Button nextStoneButton;

    [Header("New Personalized UI Fields")]
    [SerializeField] private TextMeshProUGUI fullNameText;
    [SerializeField] private TextMeshProUGUI datesText;
    [SerializeField] private TextMeshProUGUI inmateNumberText;

    [Header("🏷️ Badge UI Tags")]
    [SerializeField] private GameObject symbolBadge1;
    [SerializeField] private TextMeshProUGUI symbolBadge1Text;
    [SerializeField] private GameObject symbolBadge2;
    [SerializeField] private TextMeshProUGUI symbolBadge2Text;

    [SerializeField] private float panelFadeDuration = 0.25f;
    [SerializeField] private float panelSlideDistance = 40f;

    [Header("New UI Panels References (Navigation)")]
    public GameObject onboardingPanel;
    public GameObject arExplorationHub;
    public GameObject sidebarMenuPanel;
    public GameObject databaseSearchPanel;
    public GameObject map2DPanel;
    public GameObject siteHistoryPanel;
    public GameObject memorialDetailPanelPublic => memorialDetailPanel;

    [Header("New UI Buttons References (Automation)")]
    [SerializeField] private Button startExperienceButton;
    [SerializeField] private Button hamburgerButton;
    [SerializeField] private Button closeSidebarButton;
    [SerializeField] private Button navDatabaseButton;
    [SerializeField] private Button navMapButton;
    [SerializeField] private Button navSettingsButton;
    [SerializeField] private Button closeDatabaseButton;
    [SerializeField] private Button closeMapButton;
    [SerializeField] private Button guideMeButton;

    [Header("🎵 Audio Narration & 3D Viewer Integration")]
    [Tooltip("The Play/Pause button component located inside the Memorial Detail Panel hierarchy.")]
    [SerializeField] private Button audioPlaybackButton;
    [Tooltip("The 3D Inspector trigger button component located inside the Memorial Detail Panel hierarchy.")]
    [SerializeField] private Button view3DModelButton;
    [Tooltip("The programmatic controller component handling the localized clip streaming interfaces.")]
    [SerializeField] private PopupAudioController audioController;

    [Header("🇬🇧 Default Onboarding Setup")]
    [Tooltip("The subtitles text box dedicated specifically to the virtual guide's spoken narration.")]
    [SerializeField] private TextMeshProUGUI guideSubtitleText;

    private string welcomeTextEN = "Welcome to the Bergen-Belsen Memorial AR Experience.\nThis application facilitates historical site exploration through three distinct guidance modes:\n\n• Personal: A 3D companion leads you step-by-step along the paths.\n• Intermediate: A 3D hologram avatar appears next to selected headstones on-demand.\n• Impersonal: Autonomous exploration guided by 2D map views and data streams.\n\nSelect your preferred mode using the buttons above and tap Start Experience. You can also change modes at any time from the sidebar menu.";
    private string welcomeTextDE = "Willkommen beim Bergen-Belsen Gedenkstätten AR-Erlebnis.\nDiese Anwendung ermöglicht die Erkundung des historischen Ortes über drei verschiedene Führungsmodi:\n\n• Personal: Ein 3D-Begleiter führt Sie Schritt für Schritt über die Wege.\n• Intermediär: Ein 3D-Hologramm-Avatar erscheint bei Bedarf neben den Gedenksteinen.\n• Impersonal: Autonome Erkundung mit 2D-Kartenansichten und Daten-Streams.\n\nWählen Sie oben Ihren bevorzugten Modus und tippen Sie auf Erlebnis starten. Sie können den Modus jederzeit über das Seitenmenü ändern.";


    [Header("🗺️ Route Action Button States")]
    [Tooltip("sprite for (+)")]
    [SerializeField] private Sprite addIconSprite;
    [Tooltip("sprite for CHECK (✓)")]
    [SerializeField] private Sprite addedIconSprite;

    [Header("🌐 Language UI Tabs (Popup Panel)")]
    [SerializeField] private Button buttonEnglish;
    [SerializeField] private Button buttonGerman;
    [SerializeField] private Button buttonHebrew;

    [Header("🌐 Onboarding Language Buttons")]
    [Tooltip("Drag Btn_English from Canvas/Onboarding_Panel/Mode_Buttons_Row here")]
    [SerializeField] private Button onboardingButtonEnglish;
    [Tooltip("Drag Btn_German from Canvas/Onboarding_Panel/Mode_Buttons_Row here")]
    [SerializeField] private Button onboardingButtonGerman;

    [Header("🧪 Debug UI Testing System (Real JSON Row Scanner)")]
    [SerializeField] private Button testLeftButton;
    [SerializeField] private Button testRightButton;

    [Header("🔍 Advanced Faceted Instant Search Components")]
    [SerializeField] private TMP_InputField searchInputField;
    [SerializeField] private TMP_Dropdown searchCategoryDropdown;
    [SerializeField] private TMP_Dropdown searchSymbolsDropdown;
    [SerializeField] private Button addFilterButton;
    [SerializeField] private Button clearAllFiltersButton;
    [SerializeField] private Transform filterBadgesContainer;
    [SerializeField] private GameObject filterBadgePrefab;
    [SerializeField] private Transform searchResultsContainer;
    [SerializeField] private GameObject searchResultButtonPrefab;
    [SerializeField] private TextMeshProUGUI searchCounterText;

    [Header("🗺️ Location Setup Dropdown")]
    [SerializeField] private TMP_Dropdown locationSetupDropdown;

    [Header("📱 Map Mini Popup UI Components")]
    [SerializeField] private GameObject mapMiniPopupPanel;
    [SerializeField] private TextMeshProUGUI miniPopupIDText;
    [SerializeField] private Button miniPopupViewDetailsButton;
    [SerializeField] private Button miniPopupCloseButton;
    [SerializeField] private Camera mapCamera;
    [SerializeField] private RectTransform mapDisplayRect;

    [Header("🎓 Onboarding Thesis Mode Selectors")]
    [SerializeField] private Button btnModePersonal;
    [SerializeField] private Button btnModeIntermediate;
    [SerializeField] private Button btnModeImpersonal;

    [Header("🔔 Toast Notification Components")]
    [SerializeField] private GameObject notificationToastPanel;
    [SerializeField] private TextMeshProUGUI notificationTitleText;
    [SerializeField] private TextMeshProUGUI notificationBodyText;
    private Coroutine activeToastTimer;
    private Vector2 toastBaseAnchoredPosition;
    private bool hasCachedToastBasePosition;
    private bool isModifyingSelectedTour;
    private string pendingToastTitle;
    private string pendingToastMessage;
    private float pendingToastDuration;
    private bool resumePersonalTourPending;

    public bool IsMemorialDetailOpen => (memorialDetailPanel != null && memorialDetailPanel.activeSelf);

    public void SummonGuideAvatar()
    {
        GuidanceSystemBase activeSystem = ThesisManager.Instance != null ? ThesisManager.Instance.CurrentGuidanceSystem : null;
        ThesisManager.GuidanceMode currentMode = ThesisManager.Instance != null ? ThesisManager.Instance.CurrentMode : ThesisManager.GuidanceMode.Personal;

        PersonalGuidance personalGuide = PersonalGuidance.Instance ?? UnityEngine.Object.FindAnyObjectByType<PersonalGuidance>(FindObjectsInactive.Include);
        IntermediateGuidance intermediateGuide = IntermediateGuidance.Instance ?? UnityEngine.Object.FindAnyObjectByType<IntermediateGuidance>(FindObjectsInactive.Include);

        if (activeSystem is IntermediateGuidance || currentMode == ThesisManager.GuidanceMode.Intermediate)
        {
            if (personalGuide != null) personalGuide.DespawnAvatar();
            if (intermediateGuide != null) intermediateGuide.SummonAvatarToUser();
        }
        else if (activeSystem is PersonalGuidance || currentMode == ThesisManager.GuidanceMode.Personal)
        {
            if (intermediateGuide != null) intermediateGuide.DespawnAvatar();
            if (resumePersonalTourPending && personalGuide != null)
            {
                personalGuide.ResumeCurrentTarget();
                resumePersonalTourPending = false;
                SetSummonGuideButtonLabel(false);
                return;
            }

            bool wasGuiding = personalGuide != null && personalGuide.IsTrackingMovement;
            if (personalGuide != null) personalGuide.SummonAvatarToUser();
            if (wasGuiding)
            {
                resumePersonalTourPending = true;
                SetSummonGuideButtonLabel(true);
            }
        }
        else
        {
            ShowNotificationToast("Tour Mode", selectedLanguage == "german" ? "Begleiter im automatischen Modus nicht verfuegbar." : "Guide avatar is not active in Impersonal Mode.");
        }

        Debug.Log($"[UIManager] SummonGuideAvatar - ActiveSystem: {(activeSystem != null ? activeSystem.GetType().Name : "NULL")}, Mode: {currentMode}");
    }

    public void UpdateSummonGuideVisibility(ThesisManager.GuidanceMode mode)
    {
        GameObject summonGo = FindGameObjectIncludingInactive("Btn_SummonGuide") ?? FindGameObjectIncludingInactive("BtnSummonGuide");
        if (summonGo != null) summonGo.SetActive(mode != ThesisManager.GuidanceMode.Impersonal);
    }

    private void SetSummonGuideButtonLabel(bool showResume)
    {
        GameObject summonGo = FindGameObjectIncludingInactive("Btn_SummonGuide") ?? FindGameObjectIncludingInactive("BtnSummonGuide");
        TextMeshProUGUI label = summonGo != null ? summonGo.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        if (label != null)
            label.text = showResume
                ? (selectedLanguage == "german" ? "Begleitung fortsetzen" : "Resume guide")
                : (selectedLanguage == "german" ? "Begleiter rufen" : "Summon Guide");
    }

    /// <summary>
    /// Displays a clean temporary UI notification toast banner across the top of the screen.
    /// </summary>
    public void ShowNotificationToast(string title, string message, float duration = 4.0f)
    {
        if (IsAnyModalPanelActive())
        {
            pendingToastTitle = title;
            pendingToastMessage = message;
            pendingToastDuration = duration;
            return;
        }

        DisplayNotificationToast(title, message, duration);
    }

    private void DisplayNotificationToast(string title, string message, float duration)
    {
        if (notificationToastPanel == null)
        {
            Transform toastTrans = transform.Find("NotificationToast_Panel");
            if (toastTrans != null) notificationToastPanel = toastTrans.gameObject;
        }

        if (notificationToastPanel != null)
        {
            if (notificationTitleText != null) notificationTitleText.text = title;
            if (notificationBodyText != null) notificationBodyText.text = message;

            notificationToastPanel.transform.SetAsLastSibling();
            CanvasGroup toastCanvasGroup = notificationToastPanel.GetComponent<CanvasGroup>();
            if (toastCanvasGroup == null) toastCanvasGroup = notificationToastPanel.AddComponent<CanvasGroup>();
            toastCanvasGroup.blocksRaycasts = false;
            toastCanvasGroup.interactable = false;
            notificationToastPanel.SetActive(true);

            NavigationHUD navigationHud = UnityEngine.Object.FindAnyObjectByType<NavigationHUD>(FindObjectsInactive.Include);
            UpdateToastPlacement(navigationHud != null ? navigationHud.HudRectTransform : null, navigationHud != null && navigationHud.IsHudVisible);

            if (activeToastTimer != null) StopCoroutine(activeToastTimer);
            activeToastTimer = StartCoroutine(HideNotificationToastRoutine(duration));
        }
        else
        {
            Debug.Log($"[UIManager Toast] {title}: {message}");
        }
    }

    private IEnumerator HideNotificationToastRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (notificationToastPanel != null) notificationToastPanel.SetActive(false);
    }

    
    public enum FilterCategory
    {
        AllFields = 0,
        FirstName = 1,
        Surname = 2,
        InmateNumbers = 3,
        BirthPlace = 4,
        DeathPlace = 5,
        BirthDate = 6,
        DeathDate = 7,
        InscriptionsNotes = 8,
        Symbols = 9
    }

    private string currentSelectedMapStoneID;
    private Transform activeMarkerTransform;

    private List<string> debugMemorialIDs = new List<string>();
    private int jsonDebugIndex = 0;
    private string selectedLanguage = "english";
    public string SelectedLanguage => selectedLanguage;
    private List<SearchCacheItem> searchCacheDatabase = new List<SearchCacheItem>();
    private Stack<GameObject> navigationStack = new Stack<GameObject>();
    private List<GameObject> buttonPool = new List<GameObject>();

    private Dictionary<FilterCategory, string> activeFilters = new Dictionary<FilterCategory, string>();
    private Dictionary<FilterCategory, GameObject> activeBadgeObjects = new Dictionary<FilterCategory, GameObject>();

    private int currentMaxDisplayedResults = 20;
    private const string SEARCH_HINT_MESSAGE = "Type to search instantly, or press <b>+</b> to lock keywords as combined filters.";

    [Header("📷 Camera Detection Prompt UI")]
    [SerializeField] private GameObject cameraDetectionBanner;
    [SerializeField] private TMPro.TMP_Text cameraDetectionText;
    [SerializeField] private UnityEngine.UI.Button cameraDetectionButton;
    private string activeDetectedMemorialID;

    private MemorialDataManager dataManager;
    private ThesisManager thesisManager;
    private RouteManager cachedRouteManager;
    private object currentMemorial;
    [SerializeField] private bool releaseMode = false;
    private string currentMemorialID;
    public string CurrentMemorialID => currentMemorialID;
    private int currentPersonIndex = 0;
    private Vector2 panelHiddenPos;
    private Vector2 panelShownPos;
    private Coroutine panelAnimationRoutine;
    private ModelInspectorUI cachedModelInspectorUI;
    private SiteHistoryDropdownController cachedSiteHistoryDropdown;
    private void OnEnable()
    {
        DialogueManager.OnSubtitleUpdated += HandleSubtitleUpdated;
    }

    private void OnDisable()
    {
        DialogueManager.OnSubtitleUpdated -= HandleSubtitleUpdated;
    }

    private void HandleSubtitleUpdated(string text)
    {
        DisplayGuideSubtitle(text);
    }

    private float currentSubtitleFontSize = 24f;
    private string pendingGuideSubtitle = string.Empty;
    private bool guideSubtitlesSuspended;
    public float CurrentSubtitleFontSize => currentSubtitleFontSize;

    public void SetSubtitleFontSize(float size)
    {
        currentSubtitleFontSize = size;
        if (guideSubtitleText != null)
        {
            guideSubtitleText.enableAutoSizing = false;
            guideSubtitleText.fontSize = currentSubtitleFontSize;
            guideSubtitleText.ForceMeshUpdate();

            // Auto-resize Subtitle_Panel background height in real-time sync with font size changes
            Transform subPanel = guideSubtitleText.transform.parent;
            if (subPanel != null)
            {
                var parentRt = subPanel.GetComponent<RectTransform>();
                if (parentRt != null)
                {
                    float preferredH = guideSubtitleText.preferredHeight;
                    float targetHeight = Mathf.Clamp(preferredH + 32f, 60f, 450f);
                    parentRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);

                    var le = subPanel.GetComponent<UnityEngine.UI.LayoutElement>();
                    if (le != null)
                    {
                        le.minHeight = targetHeight;
                        le.preferredHeight = targetHeight;
                    }

                    UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(parentRt);
                }
            }
        }
    }

    public void UpdateSubtitleScale(float scale)
    {
        SetSubtitleFontSize(24f * scale);
    }

    void Start()
    {
        dataManager = UnityEngine.Object.FindAnyObjectByType<MemorialDataManager>(FindObjectsInactive.Include);
        thesisManager = UnityEngine.Object.FindAnyObjectByType<ThesisManager>(FindObjectsInactive.Include);
        cachedRouteManager = UnityEngine.Object.FindAnyObjectByType<RouteManager>(FindObjectsInactive.Include);

        if (audioController == null)
        {
            audioController = GetComponent<PopupAudioController>() ?? UnityEngine.Object.FindAnyObjectByType<PopupAudioController>(FindObjectsInactive.Include);
        }

        if (dataManager == null)
        {
            GameObject managerObj = new GameObject("MemorialDataManager");
            dataManager = managerObj.AddComponent<MemorialDataManager>();
            dataManager.LoadData();
        }

        // --- RUNTIME AUTO-WIRING FALLBACK ---
        if (onboardingPanel == null)
        {
            var t = transform.Find("Onboarding_Panel");
            if (t != null) onboardingPanel = t.gameObject;
            else onboardingPanel = GameObject.Find("Onboarding_Panel");
        }

        // --- CENTRALIZED RUNTIME SELF-HEALING FALLBACKS ---
        if (titleText == null) titleText = FindComponentInScene<TextMeshProUGUI>("Text_Stone_ID") ?? FindComponentInScene<TextMeshProUGUI>("Text_Title");
        if (descriptionText == null) descriptionText = FindComponentInScene<TextMeshProUGUI>("Text_Coordinates") ?? FindComponentInScene<TextMeshProUGUI>("Text_Description");
        if (fullNameText == null) fullNameText = FindComponentInScene<TextMeshProUGUI>("Text_Fullname") ?? FindComponentInScene<TextMeshProUGUI>("Text_FullName");
        if (datesText == null) datesText = FindComponentInScene<TextMeshProUGUI>("Text_Dates");
        if (inmateNumberText == null) inmateNumberText = FindComponentInScene<TextMeshProUGUI>("Text_Inmate") ?? FindComponentInScene<TextMeshProUGUI>("Text_InmateNumber");
        if (personsListText == null) personsListText = FindComponentInScene<TextMeshProUGUI>("Text_Dynamic_Inscription") ?? FindComponentInScene<TextMeshProUGUI>("Text_PersonsList");

        if (nextPersonButton == null) nextPersonButton = FindComponentInScene<Button>("Btn_NextPerson");
        if (prevPersonButton == null) prevPersonButton = FindComponentInScene<Button>("Btn_PrevPerson");
        if (navigateHereButton == null) navigateHereButton = FindComponentInScene<Button>("Btn_Navigate_Here");

        if (closeSidebarButton == null) closeSidebarButton = FindComponentInScene<Button>("Btn_Close_Sidebar");
        if (closeDatabaseButton == null) closeDatabaseButton = FindComponentInScene<Button>("Btn_Close_Database");
        if (closeMapButton == null) closeMapButton = FindComponentInScene<Button>("Btn_Close_Map");
        if (guideMeButton == null) guideMeButton = FindComponentInScene<Button>("Btn_GuideMe");
        if (audioPlaybackButton == null) audioPlaybackButton = FindComponentInScene<Button>("Btn_Playback") ?? FindComponentInScene<Button>("AudioPlaybackButton");
        if (view3DModelButton == null) view3DModelButton = FindComponentInScene<Button>("Btn_View3D") ?? FindComponentInScene<Button>("Btn_InspectModel");
        if (hamburgerButton == null) hamburgerButton = FindComponentInScene<Button>("Btn_Hamburger") ?? FindComponentInScene<Button>("HamburgerButton") ?? FindComponentInScene<Button>("Btn_Menu") ?? FindComponentInScene<Button>("MENU");
        if (navDatabaseButton == null) navDatabaseButton = FindComponentInScene<Button>("Btn_Nav_Database") ?? FindComponentInScene<Button>("Btn_Search");
        if (navMapButton == null) navMapButton = FindComponentInScene<Button>("Btn_Nav_Map") ?? FindComponentInScene<Button>("Btn_Map");

        if (prevStoneButton == null) prevStoneButton = FindComponentInScene<Button>("Btn_PrevStone") ?? FindComponentInScene<Button>("button left") ?? FindComponentInScene<Button>("Btn_Left");
        if (nextStoneButton == null) nextStoneButton = FindComponentInScene<Button>("Btn_NextStone") ?? FindComponentInScene<Button>("button right") ?? FindComponentInScene<Button>("Btn_Right");

        if (symbolBadge1 == null) symbolBadge1 = FindGameObjectIncludingInactive("Badge_Symbol") ?? FindGameObjectIncludingInactive("Symbol_Badge_1");
        if (symbolBadge2 == null) symbolBadge2 = FindGameObjectIncludingInactive("Badge_Camp") ?? FindGameObjectIncludingInactive("Symbol_Badge_2");
        if (symbolBadge1 != null && symbolBadge1Text == null) symbolBadge1Text = symbolBadge1.GetComponentInChildren<TextMeshProUGUI>(true);
        if (symbolBadge2 != null && symbolBadge2Text == null) symbolBadge2Text = symbolBadge2.GetComponentInChildren<TextMeshProUGUI>(true);

        ResolveLanguageButtons();


        if (guideSubtitleText == null && uiCanvas != null)
        {
            Transform subTrans = uiCanvas.transform.Find("AR_Exploration_Hub/Subtitle_Panel/Text_Subtitle") ??
                                 uiCanvas.transform.Find("AR_Exploration_Hub/Subtitle_Panel");
            if (subTrans != null)
            {
                guideSubtitleText = subTrans.GetComponent<TextMeshProUGUI>() ?? subTrans.GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }

        if (guideSubtitleText != null)
        {
            if (string.Equals(guideSubtitleText.text, "New Text", System.StringComparison.OrdinalIgnoreCase))
            {
                guideSubtitleText.text = string.Empty;
            }
            if (string.IsNullOrEmpty(guideSubtitleText.text) && guideSubtitleText.transform.parent != null)
            {
                guideSubtitleText.transform.parent.gameObject.SetActive(false);
            }
        }

        if (uiCanvas == null)
        {
            uiCanvas = FindComponentInScene<Canvas>("Canvas");
        }

        if (memorialDetailRect == null && memorialDetailPanel != null)
        {
            memorialDetailRect = memorialDetailPanel.GetComponent<RectTransform>();
        }
        if (memorialDetailCanvasGroup == null && memorialDetailPanel != null)
        {
            memorialDetailCanvasGroup = memorialDetailPanel.GetComponent<CanvasGroup>() ?? memorialDetailPanel.AddComponent<CanvasGroup>();
        }

        if (searchResultsContainer == null && uiCanvas != null)
        {
            Transform found = uiCanvas.transform.Find("Database_Search_Panel/Search_Results_Scroll_View/Viewport/Content");
            if (found != null) searchResultsContainer = found;
        }

        if (filterBadgesContainer == null && uiCanvas != null)
        {
            Transform found = uiCanvas.transform.Find("Database_Search_Panel/Filters_Horizontal_Group");
            if (found != null) filterBadgesContainer = found;
        }

        if (filterBadgesContainer != null)
        {
            var scrollRect = filterBadgesContainer.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            if (scrollRect != null)
            {
                scrollRect.movementType = UnityEngine.UI.ScrollRect.MovementType.Elastic;
                scrollRect.elasticity = 0.1f;
            }
        }

        if (onboardingPanel != null)
        {
            if (btnModePersonal == null)
            {
                var t = onboardingPanel.transform.Find("Mode_Buttons_Row/Btn_Mode_Personal");
                if (t != null) btnModePersonal = t.GetComponent<Button>();
            }
            if (btnModeIntermediate == null)
            {
                var t = onboardingPanel.transform.Find("Mode_Buttons_Row/Btn_Mode_Intermediate");
                if (t != null) btnModeIntermediate = t.GetComponent<Button>();
            }
            if (btnModeImpersonal == null)
            {
                var t = onboardingPanel.transform.Find("Mode_Buttons_Row/Btn_Mode_Impersonal");
                if (t != null) btnModeImpersonal = t.GetComponent<Button>();
            }
            if (startExperienceButton == null)
            {
                var t = onboardingPanel.transform.Find("Btn_Start");
                if (t != null) startExperienceButton = t.GetComponent<Button>();
            }
        }


        // --- BUTTON LISTENER REGISTRATION ---
        if (closeButton != null) closeButton.onClick.AddListener(HideMemorialDetail);
        if (nextPersonButton != null) nextPersonButton.gameObject.GetComponent<Button>().onClick.AddListener(ShowNextPerson);
        if (prevPersonButton != null) prevPersonButton.gameObject.GetComponent<Button>().onClick.AddListener(ShowPreviousPerson);
        if (startExperienceButton != null)
        {
            startExperienceButton.onClick.RemoveAllListeners();
            startExperienceButton.onClick.AddListener(StartExperience);
        }

        if (navigateHereButton != null) navigateHereButton.onClick.AddListener(HandleNavigateHereClicked);

        if (guideSubtitleText != null)
        {
            guideSubtitleText.text = string.Empty;
            if (guideSubtitleText.transform.parent != null)
            {
                guideSubtitleText.transform.parent.gameObject.SetActive(false);
            }
            Debug.Log($"[UIManager] Start: guideSubtitleText resolved to '{guideSubtitleText.gameObject.name}' on parent '{guideSubtitleText.transform.parent?.gameObject.name}'");
        }
        else
        {
            Debug.LogWarning("[UIManager] Start: guideSubtitleText is NULL after all resolution attempts!");
        }

        if (hamburgerButton != null)
        {
            hamburgerButton.onClick.RemoveAllListeners();
            hamburgerButton.onClick.AddListener(ToggleSidebarMenu);
        }
        if (navDatabaseButton != null) navDatabaseButton.onClick.AddListener(() => OpenPanel(databaseSearchPanel));
        if (navMapButton != null) navMapButton.onClick.AddListener(() => OpenPanel(map2DPanel));

        // Auto-resolve navSettingsButton from SidebarMenuPanel (searches recursively including sub-containers)
        if (navSettingsButton == null && sidebarMenuPanel != null)
        {
            Button[] sidebarButtons = sidebarMenuPanel.GetComponentsInChildren<Button>(true);
            foreach (var btn in sidebarButtons)
            {
                if (btn.name.ToLower().Contains("setting"))
                {
                    navSettingsButton = btn;
                    Debug.Log($"[UIManager] Auto-resolved 'navSettingsButton' to: {btn.name}");
                    break;
                }
            }
        }

        if (navSettingsButton != null)
        {
            navSettingsButton.onClick.RemoveAllListeners();
            navSettingsButton.onClick.AddListener(OpenSettingsPanelFromSidebar);
        }

        if (closeSidebarButton != null)
        {
            closeSidebarButton.onClick = new Button.ButtonClickedEvent();
            closeSidebarButton.onClick.AddListener(CloseSidebar);
        }
        if (closeDatabaseButton != null) { closeDatabaseButton.onClick.RemoveAllListeners(); closeDatabaseButton.onClick.AddListener(CloseDatabaseAndReset); }
        if (closeMapButton != null) { closeMapButton.onClick.RemoveAllListeners(); closeMapButton.onClick.AddListener(CloseMapAndReset); }

        if (buttonEnglish != null) { buttonEnglish.onClick.RemoveAllListeners(); buttonEnglish.onClick.AddListener(() => SetPopupLanguage("english")); }
        if (buttonGerman != null) { buttonGerman.onClick.RemoveAllListeners(); buttonGerman.onClick.AddListener(() => SetPopupLanguage("german")); }

        // Explicitly re-wire Btn_SummonGuide in Start() to prevent scene UnityEvent hardcoding to PersonalGuidance
        GameObject summonGo = FindGameObjectIncludingInactive("Btn_SummonGuide") ?? FindGameObjectIncludingInactive("BtnSummonGuide");
        if (summonGo != null)
        {
            Button summonBtn = summonGo.GetComponent<Button>();
            if (summonBtn != null)
            {
                summonBtn.onClick.RemoveAllListeners();
                summonBtn.onClick.AddListener(SummonGuideAvatar);
                Debug.Log($"[UIManager] Start: Successfully wired '{summonGo.name}' onClick listener to UIManager.SummonGuideAvatar().");
            }
        }
        if (buttonHebrew != null) { buttonHebrew.onClick.RemoveAllListeners(); buttonHebrew.onClick.AddListener(() => SetPopupLanguage("hebrew")); }

        if (prevStoneButton != null)
        {
            prevStoneButton.onClick.RemoveAllListeners();
            prevStoneButton.onClick.AddListener(ShowPreviousDebugMemorial);
            prevStoneButton.gameObject.SetActive(true);
        }
        if (nextStoneButton != null)
        {
            nextStoneButton.onClick.RemoveAllListeners();
            nextStoneButton.onClick.AddListener(ShowNextDebugMemorial);
            nextStoneButton.gameObject.SetActive(true);
        }
        if (guideMeButton != null) { guideMeButton.onClick.RemoveAllListeners(); guideMeButton.onClick.AddListener(OnGuideMeButtonClicked); }

        if (audioPlaybackButton != null)
        {
            audioPlaybackButton.onClick.RemoveAllListeners();
            audioPlaybackButton.onClick.AddListener(HandleAudioPlaybackClicked);
        }
        if (view3DModelButton != null)
        {
            view3DModelButton.onClick.RemoveAllListeners();
            view3DModelButton.onClick.AddListener(Open3DModelInspector);
        }

        if (searchResultButtonPrefab != null) searchResultButtonPrefab.SetActive(false);

        if (memorialDetailRect != null)
        {
            panelShownPos = memorialDetailRect.anchoredPosition;
            panelHiddenPos = panelShownPos + new Vector2(0f, -panelSlideDistance);
        }

        // Initialize display layers to closed defaults to allow structural setup control
        if (memorialDetailPanel != null) memorialDetailPanel.SetActive(false);
        if (sidebarMenuPanel != null) sidebarMenuPanel.SetActive(false);
        if (databaseSearchPanel != null) databaseSearchPanel.SetActive(false);
        if (map2DPanel != null) map2DPanel.SetActive(false);
        if (mapMiniPopupPanel != null) mapMiniPopupPanel.SetActive(false);
        if (arExplorationHub != null) arExplorationHub.SetActive(false);
        if (siteHistoryPanel != null) siteHistoryPanel.SetActive(false);

        // Ensure Settings panels are closed on boot so they don't overlap onboarding
        GameObject settingsObj1 = GameObject.Find("Canvas/SettingsPanel") ?? GameObject.Find("SettingsPanel");
        if (settingsObj1 != null) settingsObj1.SetActive(false);

        GameObject settingsObj2 = GameObject.Find("Canvas/Advanced_Settings_Panel") ?? GameObject.Find("Advanced_Settings_Panel");
        if (settingsObj2 != null) settingsObj2.SetActive(false);

        if (onboardingPanel != null)
        {
            onboardingPanel.SetActive(true);
            onboardingPanel.transform.SetAsLastSibling();
        }

        navigationStack.Clear();

        // Force systemic language baseline execution straight to English at boot
        selectedLanguage = "english";

        // Execute localization barrier validation loop
        EvaluateUserGeofencingLocation();

        InitializeSearchCategoryDropdown();

        if (searchInputField != null)
        {
            searchInputField.onValueChanged.RemoveAllListeners();
            searchInputField.onValueChanged.AddListener((val) => ExecuteDynamicFacetedSearch());
            searchInputField.onSubmit.RemoveAllListeners();
            searchInputField.onSubmit.AddListener((val) => AddFilterFromUI());
        }

        if (addFilterButton != null) { addFilterButton.onClick.RemoveAllListeners(); addFilterButton.onClick.AddListener(AddFilterFromUI); }
        if (clearAllFiltersButton != null)
        {
            clearAllFiltersButton.onClick.RemoveAllListeners();
            clearAllFiltersButton.onClick.AddListener(ClearAllFilters);
            clearAllFiltersButton.gameObject.SetActive(false);
        }

        if (miniPopupViewDetailsButton != null) { miniPopupViewDetailsButton.onClick.RemoveAllListeners(); miniPopupViewDetailsButton.onClick.AddListener(ConfirmAndOpenFullDetails); }
        else Debug.LogError("[UIManager] 'Mini Popup View Details Button' is NOT assigned in the Inspector.");

        if (miniPopupCloseButton != null) { miniPopupCloseButton.onClick.RemoveAllListeners(); miniPopupCloseButton.onClick.AddListener(() => mapMiniPopupPanel.SetActive(false)); }
        else Debug.LogError("[UIManager] 'Mini Popup Close Button' is NOT assigned in the Inspector.");

        InitializeLocationDropdown();
        SyncDiagnosticUIState();

        // Onboarding Thesis Mode Selectors hooks
        if (btnModePersonal != null)
        {
            btnModePersonal.onClick.RemoveAllListeners();
            btnModePersonal.onClick.AddListener(() => SetOnboardingThesisMode(0));
        }
        if (btnModeIntermediate != null)
        {
            btnModeIntermediate.onClick.RemoveAllListeners();
            btnModeIntermediate.onClick.AddListener(() => SetOnboardingThesisMode(1));
        }
        if (btnModeImpersonal != null)
        {
            btnModeImpersonal.onClick.RemoveAllListeners();
            btnModeImpersonal.onClick.AddListener(() => SetOnboardingThesisMode(2));
        }


        // Dynamically override instructions text in onboarding panel to explain the modes
        GameObject instructionsGo = GameObject.Find("Text_Instructions");
        if (instructionsGo == null && onboardingPanel != null)
        {
            var t = onboardingPanel.transform.Find("Text_Instructions");
            if (t != null) instructionsGo = t.gameObject;
        }
        if (instructionsGo != null)
        {
            var txtComp = instructionsGo.GetComponent<TMPro.TextMeshProUGUI>();
            if (txtComp != null) txtComp.text = welcomeTextEN;
        }

        // Apply initial visual highlights
        int savedIndex = PlayerPrefs.GetInt("Thesis_GuidanceMode", 0);
        ThesisManager.GuidanceMode initialMode = ThesisManager.GuidanceMode.Personal;
        if (savedIndex == 1) initialMode = ThesisManager.GuidanceMode.Intermediate;
        else if (savedIndex == 2) initialMode = ThesisManager.GuidanceMode.Impersonal;
        UpdateOnboardingButtonsVisuals(initialMode);
        UpdateLanguageButtonsVisuals();

        var advancedSettings = UnityEngine.Object.FindAnyObjectByType<AdvancedSettingsPanel>(FindObjectsInactive.Include);
        if (advancedSettings != null) advancedSettings.ApplySavedAppearance();

        StartCoroutine(DeferredInitialization());
    }

    public void OpenSettingsPanelFromSidebar()
    {
        // Include inactive objects so inactive panels can be activated on-demand
        AdvancedSettingsPanel advController = UnityEngine.Object.FindAnyObjectByType<AdvancedSettingsPanel>(FindObjectsInactive.Include);
        if (advController != null)
        {
            SetGuideSubtitlesSuspended(true);
            OpenPanel(advController.gameObject);
            advController.ShowSettingsPanel();
            Debug.Log("[UIManager] Successfully opened AdvancedSettingsPanel from Sidebar.");
            return;
        }

        // Direct Canvas transform fallback for an unscripted settings panel.
        // SettingsUIController is legacy and is attached to the sidebar in the current scene,
        // so activating its GameObject here would reopen the drawer instead of settings.
        Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas != null)
        {
            Transform t = canvas.transform.Find("Advanced_Settings_Panel") ?? canvas.transform.Find("SettingsPanel");
            if (t != null)
            {
                SetGuideSubtitlesSuspended(true);
                OpenPanel(t.gameObject);
                Debug.Log($"[UIManager] Opened settings panel GameObject '{t.name}' through the navigation stack.");
                return;
            }
        }

        Debug.LogWarning("[UIManager] Could not locate any Advanced_Settings_Panel or SettingsPanel in Canvas hierarchy.");
    }


    /// <summary>
    /// Processes physical location constraints via geospatial tracking data.
    /// Spawns the AR guide immediately if on-site, or redirects to remote desktop views if at home.
    /// </summary>
    private void EvaluateUserGeofencingLocation()
    {
        bool isUserOnSite = true;

#if !UNITY_EDITOR
        GeospatialManager geoManager = UnityEngine.Object.FindAnyObjectByType<GeospatialManager>(FindObjectsInactive.Include);
        if (geoManager != null)
        {
            isUserOnSite = geoManager.IsUserInsideMemorialBoundaries(); 
        }
#endif

        if (isUserOnSite)
        {
            // Scenario A: User is physically on location. Activate Onboarding to choose mode
            if (arExplorationHub != null) arExplorationHub.SetActive(false); // Do not show AR hub until Start is clicked
            if (onboardingPanel != null) onboardingPanel.SetActive(true); // Open the 3 thesis buttons deck instantly
            
            // Prepare welcome subtitles (will be displayed when Start is pressed)
            if (guideSubtitleText != null) guideSubtitleText.text = welcomeTextEN;

            
            Debug.Log("[Geofencing] On-site state confirmed. English welcome narration sequence engaged.");
        }
        else
        {
            // Scenario B: User is remote. Hide AR elements and push directly into the 2D interactive Cesium map layout
            if (arExplorationHub != null) arExplorationHub.SetActive(false);
            if (onboardingPanel != null) onboardingPanel.SetActive(false);

            OpenPanel(map2DPanel);
            Debug.Log("[Geofencing] Remote state confirmed. Redirecting workflow directly onto the 2D navigation map.");
        }
    }

    private void InitializeSearchCategoryDropdown()
    {
        if (searchCategoryDropdown == null) return;

        bool isGerman = string.Equals(selectedLanguage, "german", System.StringComparison.OrdinalIgnoreCase);

        int currentVal = searchCategoryDropdown.value;

        searchCategoryDropdown.ClearOptions();
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>
        {
            new TMP_Dropdown.OptionData(isGerman ? "Alle Felder" : "All Fields"),
            new TMP_Dropdown.OptionData(isGerman ? "Vorname" : "First Name"),
            new TMP_Dropdown.OptionData(isGerman ? "Nachname" : "Surname"),
            new TMP_Dropdown.OptionData(isGerman ? "Häftlingsnummern" : "Inmate Numbers"),
            new TMP_Dropdown.OptionData(isGerman ? "Geburtsort" : "Birth Place"),
            new TMP_Dropdown.OptionData(isGerman ? "Sterbeort" : "Death Place"),
            new TMP_Dropdown.OptionData(isGerman ? "Geburtsdatum" : "Birth Date"),
            new TMP_Dropdown.OptionData(isGerman ? "Sterbedatum" : "Death Date"),
            new TMP_Dropdown.OptionData(isGerman ? "Inschriften & Notizen" : "Inscriptions & Notes"),
            new TMP_Dropdown.OptionData(isGerman ? "Symbole" : "Symbols")
        };
        searchCategoryDropdown.AddOptions(options);
        searchCategoryDropdown.value = Mathf.Clamp(currentVal, 0, options.Count - 1);
        searchCategoryDropdown.RefreshShownValue();

        if (searchSymbolsDropdown != null)
        {
            int symVal = searchSymbolsDropdown.value;
            searchSymbolsDropdown.ClearOptions();
            List<TMP_Dropdown.OptionData> symbolOptions = new List<TMP_Dropdown.OptionData>
            {
                new TMP_Dropdown.OptionData(isGerman ? "Symbol auswählen..." : "Select Symbol..."),
                new TMP_Dropdown.OptionData(isGerman ? "Kreuz" : "Cross"),
                new TMP_Dropdown.OptionData(isGerman ? "Davidstern" : "Star of David"),
                new TMP_Dropdown.OptionData(isGerman ? "Abgebrochener Baumstamm" : "Broken tree trunk"),
                new TMP_Dropdown.OptionData(isGerman ? "Polnisches Wappen" : "Polish coat of arms"),
                new TMP_Dropdown.OptionData(isGerman ? "Eingefügtes Foto" : "Inset photograph")
            };
            searchSymbolsDropdown.AddOptions(symbolOptions);
            searchSymbolsDropdown.value = Mathf.Clamp(symVal, 0, symbolOptions.Count - 1);
            searchSymbolsDropdown.RefreshShownValue();
            searchSymbolsDropdown.gameObject.SetActive(false);
        }

        StyleDropdownTemplate(searchCategoryDropdown);
        StyleDropdownTemplate(searchSymbolsDropdown);

        searchSymbolsDropdown.onValueChanged.RemoveAllListeners();
        searchCategoryDropdown.onValueChanged.RemoveAllListeners();

        searchSymbolsDropdown.onValueChanged.AddListener((idx) => {
            if (idx > 0) AddFilterFromUI();
        });

        searchCategoryDropdown.onValueChanged.AddListener(HandleSearchCategoryChanged);
    }

    private void StyleDropdownTemplate(TMP_Dropdown dropdown)
    {
        if (dropdown == null) return;

        Color darkBackground = new Color(0.12f, 0.15f, 0.20f, 0.98f);
        Color textWhite = new Color(0.95f, 0.95f, 0.95f, 1.0f);

        if (dropdown.captionText != null)
        {
            dropdown.captionText.color = textWhite;
            dropdown.captionText.fontSize = Mathf.Max(dropdown.captionText.fontSize, 15f);
        }

        Transform template = dropdown.transform.Find("Template");
        if (template != null)
        {
            Image templateBg = template.GetComponent<Image>();
            if (templateBg != null)
            {
                templateBg.color = darkBackground;
            }

            foreach (var txt in template.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (txt != null)
                {
                    txt.color = textWhite;
                    txt.fontSize = Mathf.Max(txt.fontSize, 15f);
                }
            }

            foreach (var img in template.GetComponentsInChildren<Image>(true))
            {
                if (img != null && img != templateBg && (img.name.Contains("Background") || img.name.Contains("Item")))
                {
                    img.color = new Color(0.18f, 0.22f, 0.28f, 0.9f);
                }
            }
        }
    }

    private void HandleSearchCategoryChanged(int index)
    {
        FilterCategory selected = (FilterCategory)index;
        bool isSymbolMode = (selected == FilterCategory.Symbols);

        if (searchInputField != null) searchInputField.gameObject.SetActive(!isSymbolMode);
        if (searchSymbolsDropdown != null) searchSymbolsDropdown.gameObject.SetActive(isSymbolMode);

        ExecuteDynamicFacetedSearch();
    }

    private void InitializeLocationDropdown()
    {
        if (locationSetupDropdown == null) return;

        locationSetupDropdown.ClearOptions();
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>
        {
            new TMP_Dropdown.OptionData("SDU Actual Soccer Field"),
            new TMP_Dropdown.OptionData("SDU Office"),
            new TMP_Dropdown.OptionData("Bergen-Belsen Real Site")
        };
        locationSetupDropdown.AddOptions(options);
        locationSetupDropdown.onValueChanged.RemoveAllListeners();
        locationSetupDropdown.onValueChanged.AddListener(OnLocationDropdownChanged);
    }

    private void OnLocationDropdownChanged(int index)
    {
        CesiumOriginSwitcher switcher = UnityEngine.Object.FindAnyObjectByType<CesiumOriginSwitcher>(FindObjectsInactive.Include);
        if (switcher == null) return;

        switch (index)
        {
            case 0:
                switcher.SetOriginToSoccerField();
                Debug.Log("[Location Hub] Core origin environment transformed to: SDU Actual Soccer Field");
                break;
            case 1:
                switcher.SetOriginToOffice();
                Debug.Log("[Location Hub] Core origin environment transformed to: SDU Office Area");
                break;
            case 2:
                switcher.SetOriginToGlb();
                Debug.Log("[Location Hub] Core origin environment transformed to: Bergen-Belsen Real Site");
                break;
        }
    }

    

    private float lastPopTime = 0f;

    public void CloseCurrentAndReturn()
    {
        // ⚡ UX STACK PROTECTION: Prevent accidental double-popping if a button has multiple close listeners 
        // (e.g., one from Unity Editor persistent event, one from code).
        if (Time.unscaledTime - lastPopTime < 0.2f)
        {
            Debug.LogWarning("[UI Navigation] Blocked double-pop request within 0.2s.");
            return;
        }
        lastPopTime = Time.unscaledTime;

        // ⚡ UX STACK PROTECTION: If the 3D Inspector is active and the user clicks a Close button wired
        // to CloseCurrentAndReturn in the Unity Editor, we MUST intercept it. The 3D Inspector is an overlay,
        // so popping the stack here would incorrectly close the underlying Memorial Detail Panel.
        var inspector = ModelInspectorUI.Instance;
        if (inspector != null && inspector.GetInspectorPanel() != null && inspector.GetInspectorPanel().activeSelf)
        {
            Debug.Log("[UI Navigation] Intercepted CloseCurrentAndReturn: Rerouting to 3D Inspector's own close logic.");
            inspector.CloseInspector();
            return;
        }

        if (navigationStack.Count > 0)
        {
            GameObject current = navigationStack.Pop();
            current.SetActive(false);
            Debug.Log($"[UI Navigation] Popped and closed panel: {current.name}");

            if (navigationStack.Count > 0)
            {
                GameObject previous = navigationStack.Peek();
                previous.SetActive(true);
                Debug.Log($"[UI Navigation] Restoring previous panel: {previous.name}");

                // FAILSAFE: If the panel being restored is the MemorialDetailPanel,
                // we MUST re-trigger its animation, otherwise it might be stuck at the hidden position
                // or just appear instantly without its layout being refreshed.
                if (memorialDetailPanel != null && previous == memorialDetailPanel)
                {
                    PlayPanelAnimation(true);
                }
            }
        }
        
        // Only show AR hub if no UI panels are active
        if (navigationStack.Count == 0 && arExplorationHub != null)
        {
            arExplorationHub.SetActive(true);
        }
    }

    IEnumerator DeferredInitialization()
    {
        while (dataManager == null || !dataManager.IsLoaded)
        {
            yield return null;
        }
        InitializeSearchCache();
        if (searchCounterText != null) searchCounterText.text = SEARCH_HINT_MESSAGE;
    }

    public void StartExperience()
    {
        if (onboardingPanel != null)
            onboardingPanel.SetActive(false);

        if (arExplorationHub != null)
        {
            arExplorationHub.SetActive(true);
            var advSettings = FindAnyObjectByType<AdvancedSettingsPanel>(FindObjectsInactive.Include);
            if (advSettings != null) advSettings.SyncDiagnosticState();
        }

        // Launch non-blocking step-by-step tutorial overlay if enabled
        var tutorial = UnityEngine.Object.FindAnyObjectByType<CoachmarkTutorialController>(FindObjectsInactive.Include);
        if (tutorial != null)
        {
            tutorial.TryStartTutorial(0);
        }

        // Officially start the experience session: spawns 3D avatar & triggers welcome audio for the first time
        if (thesisManager != null)
        {
            thesisManager.StartExperienceSession();
        }
        else
        {
            Debug.LogWarning("[UIManager] Failed to trigger welcome audio: ThesisManager instance reference is missing.");
        }
    }
    private float lastSidebarToggleTime = 0f;

    public void ToggleSidebarMenu()
    {
        if (sidebarMenuPanel == null) return;
        
        // Prevent double-triggering if multiple listeners are attached (e.g. persistent and script)
        if (Time.unscaledTime - lastSidebarToggleTime < 0.2f) return;
        lastSidebarToggleTime = Time.unscaledTime;

        if (sidebarMenuPanel.activeSelf)
        {
            CloseSidebar();
        }
        else
        {
            OpenSidebar();
        }
    }

    public void OpenSidebar()
    {
        if (sidebarMenuPanel != null)
        {
            sidebarMenuPanel.SetActive(true);
            sidebarMenuPanel.transform.SetAsLastSibling();
            
            // Auto-wire close button ('X') inside sidebar if unassigned
            if (closeSidebarButton == null)
            {
                Transform t = sidebarMenuPanel.transform.Find("Btn_Close") ?? sidebarMenuPanel.transform.Find("BtnClose") ?? sidebarMenuPanel.transform.Find("Header_Bar/Btn_Close");
                if (t != null) closeSidebarButton = t.GetComponent<Button>();
                if (closeSidebarButton == null)
                {
                    foreach (var b in sidebarMenuPanel.GetComponentsInChildren<Button>(true))
                    {
                        if (b.name.ToLower().Contains("close") || b.name.ToLower().Contains("exit")) { closeSidebarButton = b; break; }
                    }
                }
            }

            if (closeSidebarButton != null)
            {
                closeSidebarButton.onClick = new Button.ButtonClickedEvent();
                closeSidebarButton.onClick.AddListener(CloseSidebar);
            }
        }
        Debug.Log("[UI Event Trace] Sidebar_Menu_Panel OPENED");
    }

    public void CloseSidebar()
    {
        if (sidebarMenuPanel != null) sidebarMenuPanel.SetActive(false);
        Debug.Log("[UI Event Trace] Sidebar_Menu_Panel CLOSED");
    }

    /// <summary>
    /// Called by the Tutorial button inside Sidebar_Menu_Panel.
    /// Closes the sidebar first so the tutorial can start from Step 0 (the hamburger button).
    /// </summary>
    public void RestartTutorialFromSidebar()
    {
        var tutorial = FindAnyObjectByType<CoachmarkTutorialController>(FindObjectsInactive.Include);
        if (tutorial != null)
        {
            RestartTutorialFromSettings();
        }
        Debug.Log("[UI Event Trace] Tutorial restarted from Sidebar button.");
    }

    public void UpdateToastPlacement(RectTransform navigationHud, bool navigationHudVisible)
    {
        if (notificationToastPanel == null) return;
        RectTransform toastRect = notificationToastPanel.GetComponent<RectTransform>();
        if (toastRect == null) return;

        if (!hasCachedToastBasePosition)
        {
            toastBaseAnchoredPosition = toastRect.anchoredPosition;
            hasCachedToastBasePosition = true;
        }

        float offset = navigationHudVisible && navigationHud != null ? navigationHud.rect.height + 16f : 0f;
        toastRect.anchoredPosition = toastBaseAnchoredPosition + Vector2.down * offset;
    }

    public void RestartTutorialFromSettings()
    {
        ReturnToExplorationHub();
        var tutorial = FindAnyObjectByType<CoachmarkTutorialController>(FindObjectsInactive.Include);
        if (tutorial != null) tutorial.RestartTutorial();
    }

    public void OpenSiteHistoryPanel()
    {
        if (siteHistoryPanel == null)
        {
            siteHistoryPanel = GameObject.Find("Panel_SiteHistory") ?? GameObject.Find("SiteHistoryPanel");
        }

        if (siteHistoryPanel != null)
        {
            OpenPanel(siteHistoryPanel);
            var dropdownCtrl = siteHistoryPanel.GetComponent<SiteHistoryDropdownController>();
            if (dropdownCtrl != null)
            {
                dropdownCtrl.OpenDropdown();
            }
        }
        Debug.Log("[UI Event Trace] SiteHistoryPanel OPENED");
    }

    public void CloseSiteHistoryPanel()
    {
        CloseCurrentAndReturn();
    }

    private void PopulateSiteHistoryContent()
    {
        if (siteHistoryPanel == null) return;

        // Prioritize ScrollRect's content property as it is the authoritative UI container
        var scrollRect = siteHistoryPanel.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);
        Transform contentContainer = scrollRect != null ? scrollRect.content : null;

        if (contentContainer == null)
        {
            contentContainer = siteHistoryPanel.transform.Find("Scroll View/Viewport/Content") ??
                               siteHistoryPanel.transform.Find("Viewport/Content") ??
                               siteHistoryPanel.transform.Find("Content") ??
                               siteHistoryPanel.transform;
        }

        // Ensure contentContainer has VerticalLayoutGroup and ContentSizeFitter so cards align vertically and scroll properly
        var containerLayout = contentContainer.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        if (containerLayout == null)
        {
            containerLayout = contentContainer.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            containerLayout.padding = new RectOffset(15, 15, 15, 15);
            containerLayout.spacing = 12f;
            containerLayout.childControlWidth = true;
            containerLayout.childControlHeight = false;
            containerLayout.childForceExpandWidth = true;
            containerLayout.childForceExpandHeight = false;
        }

        var containerFitter = contentContainer.GetComponent<UnityEngine.UI.ContentSizeFitter>();
        if (containerFitter == null)
        {
            containerFitter = contentContainer.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            containerFitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        }

        // If cards already generated, skip re-generation
        if (contentContainer.Find("Card_Area_WomensCamp") != null) return;

        var chapters = new[]
        {
            new { Id = "Area_WomensCamp", TitleEN = "Former Women's Camp", TitleDE = "Ehemaliges Frauenlager", Desc = "Established August 1944. Housed thousands of women from Warsaw Uprising and Auschwitz." },
            new { Id = "Area_ExchangeCamp", TitleEN = "Exchange Camp", TitleDE = "Austauschlager", Desc = "Established 1943 to hold Jewish hostages for potential international exchange." },
            new { Id = "Area_MassGraveL", TitleEN = "L-Shaped Symbolic Mass Grave", TitleDE = "L-förmiges Massengrab", Desc = "Discovered after French survivor Georges Bonnet noted remaining ashes in 1964." },
            new { Id = "Walking_LiberationDrama", TitleEN = "The Liberation (April 15, 1945)", TitleDE = "Die Befreiung (15. April 1945)", Desc = "British Army entry encountering ten thousand unburied bodies and urgent medical aid." },
            new { Id = "Walking_RegistryDestruction", TitleEN = "Registry Destruction", TitleDE = "Vernichtung der Kartei", Desc = "52,000 dead, only 11,000 known by name due to SS registry destruction." },
            new { Id = "Stone_AnneFrank", TitleEN = "Anne & Margot Frank Memorial", TitleDE = "Gedenkstein Anne & Margot Frank", Desc = "Symbolic memorial stone erected in June 1999 by cousin Buddy Elias." },
            new { Id = "Stone_Obelisk", TitleEN = "26-Meter Obelisk", TitleDE = "26-Meter-Obelisk", Desc = "Dedicated in 1952. President Theodor Heuss quote: 'A thorn to always remind society'." }
        };

        bool isDE = selectedLanguage == "german";

        foreach (var ch in chapters)
        {
            GameObject card = new GameObject($"Card_{ch.Id}", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.VerticalLayoutGroup), typeof(UnityEngine.UI.LayoutElement));
            card.transform.SetParent(contentContainer, false);
            
            var img = card.GetComponent<UnityEngine.UI.Image>();
            img.color = new Color(0.12f, 0.15f, 0.18f, 0.95f);

            var le = card.GetComponent<UnityEngine.UI.LayoutElement>();
            le.minHeight = 110f;
            le.preferredHeight = 120f;
            le.flexibleWidth = 1f;

            var vlg = card.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            vlg.padding = new RectOffset(15, 15, 12, 12);
            vlg.spacing = 6f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            // Title Text
            GameObject titleGo = new GameObject("Text_Title", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            titleGo.transform.SetParent(card.transform, false);
            var titleTxt = titleGo.GetComponent<TMPro.TextMeshProUGUI>();
            titleTxt.text = isDE ? ch.TitleDE : ch.TitleEN;
            titleTxt.fontSize = 18f;
            titleTxt.fontStyle = TMPro.FontStyles.Bold;
            titleTxt.color = new Color(0.3f, 0.85f, 1f, 1f);

            // Description Text
            GameObject descGo = new GameObject("Text_Desc", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            descGo.transform.SetParent(card.transform, false);
            var descTxt = descGo.GetComponent<TMPro.TextMeshProUGUI>();
            descTxt.text = ch.Desc;
            descTxt.fontSize = 14f;
            descTxt.color = new Color(0.85f, 0.88f, 0.92f, 1f);

            // Play Audio Button
            GameObject btnGo = new GameObject("Btn_PlayAudio", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            btnGo.transform.SetParent(card.transform, false);
            var btnImg = btnGo.GetComponent<UnityEngine.UI.Image>();
            btnImg.color = new Color(0f, 0.5f, 0.9f, 1f);

            var btn = btnGo.GetComponent<UnityEngine.UI.Button>();
            string clipToPlay = ch.Id;
            btn.onClick.AddListener(() => PlaySiteHistoryChapter(clipToPlay));

            GameObject btnTxtGo = new GameObject("Text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            btnTxtGo.transform.SetParent(btnGo.transform, false);
            var btnTxt = btnTxtGo.GetComponent<TMPro.TextMeshProUGUI>();
            btnTxt.text = isDE ? "▶ Audio abspielen" : "▶ Play Audio Narration";
            btnTxt.alignment = TMPro.TextAlignmentOptions.Center;
            btnTxt.fontSize = 14f;
            btnTxt.color = Color.white;
        }

        var rectTransform = contentContainer as RectTransform;
        if (rectTransform != null)
        {
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }
    }

    public void PlaySiteHistoryChapter(string clipId)
    {
        string langSuffix = selectedLanguage == "german" ? "DE" : "EN";
        string fullClipId = clipId.EndsWith("_EN") || clipId.EndsWith("_DE") ? clipId : $"{clipId}_{langSuffix}";

        if (NarrationManager.Instance != null)
        {
            NarrationManager.Instance.PlayNarration(fullClipId, null);
        }
        Debug.Log($"[Site History] Streaming chapter narration clip: {fullClipId}");
    }

    public void ChangeLanguage(string lang)
    {
        selectedLanguage = lang;
        AppLanguage langEnum = lang.ToAppLanguage();

        if (currentMemorial != null)
        {
            UpdateDetailDisplay();
        }

        var tutorial = UnityEngine.Object.FindAnyObjectByType<CoachmarkTutorialController>(FindObjectsInactive.Include);
        if (tutorial != null) tutorial.SetLanguage(langEnum);
        else Debug.LogWarning("[UIManager] CoachmarkTutorialController non trovato — il tutorial non verra tradotto.");

        var askMore = UnityEngine.Object.FindAnyObjectByType<AskMoreButtonController>(FindObjectsInactive.Include);
        if (askMore != null) askMore.SetLanguage(langEnum);
        else Debug.LogWarning("[UIManager] AskMoreButtonController non trovato — i bottoni Ask-More non verranno tradotti.");

        var siteHistory = UnityEngine.Object.FindAnyObjectByType<SiteHistoryDropdownController>(FindObjectsInactive.Include);
        if (siteHistory != null) siteHistory.SetLanguage(langEnum);
        else Debug.LogWarning("[UIManager] SiteHistoryDropdownController non trovato — il dropdown storia non verra tradotto.");

        var intermediate = UnityEngine.Object.FindAnyObjectByType<IntermediateGuidance>(FindObjectsInactive.Include);
        if (intermediate != null)
        {
            intermediate.SetLanguage(langEnum);
        }

        UpdateAllPanelsLocalization();
        UpdateLanguageButtonsVisuals();
    }

    public void UpdateAllPanelsLocalization()
    {
        bool isGerman = string.Equals(selectedLanguage, "german", System.StringComparison.OrdinalIgnoreCase);

        if (buttonEnglish != null)
        {
            var cg = buttonEnglish.GetComponent<CanvasGroup>();
            if (cg == null) cg = buttonEnglish.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = isGerman ? 0.6f : 1.0f;
        }

        if (buttonGerman != null)
        {
            var cg = buttonGerman.GetComponent<CanvasGroup>();
            if (cg == null) cg = buttonGerman.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = isGerman ? 1.0f : 0.6f;
        }

        // 0. Onboarding Panel
        if (onboardingPanel != null)
        {
            Transform welcomeTextObj = onboardingPanel.transform.Find("Text_Instructions") ??
                                      onboardingPanel.transform.Find("Text_WelcomeDescription") ?? 
                                      onboardingPanel.transform.Find("Text_Welcome") ?? 
                                      onboardingPanel.transform.Find("Text_Description");
            if (welcomeTextObj == null)
            {
                var found = GameObject.Find("Text_Instructions");
                if (found != null) welcomeTextObj = found.transform;
            }

            if (welcomeTextObj != null)
            {
                var txt = welcomeTextObj.GetComponent<TextMeshProUGUI>();
                if (txt != null) txt.text = isGerman ? welcomeTextDE : welcomeTextEN;
            }

            if (startExperienceButton != null)
            {
                var txt = startExperienceButton.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null) txt.text = isGerman ? "Erlebnis starten" : "Start Experience";
            }

            var headerTitle = onboardingPanel.transform.Find("Header_Bar/Text_Title") ?? onboardingPanel.transform.Find("Text_Title");
            if (headerTitle != null)
            {
                var txt = headerTitle.GetComponent<TextMeshProUGUI>();
                if (txt != null) txt.text = isGerman ? "Willkommen bei ThesisAR" : "Welcome to ThesisAR";
            }
        }

        // Refresh mode button labels (Persönlich / Intermediär / Unpersönlich)
        UpdateOnboardingButtonsVisuals(thesisManager != null
            ? thesisManager.CurrentMode
            : (ThesisManager.GuidanceMode)PlayerPrefs.GetInt("Thesis_GuidanceMode", 0));

        // 1. Sidebar Menu Panel
        if (sidebarMenuPanel != null)
        {
            var allTMPs = sidebarMenuPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var txt in allTMPs)
            {
                if (txt == null || string.IsNullOrEmpty(txt.text)) continue;
                string clean = txt.text.Trim();
                if (clean.Equals("Search Database", System.StringComparison.OrdinalIgnoreCase) || clean.Equals("Datenbank durchsuchen", System.StringComparison.OrdinalIgnoreCase))
                    txt.text = isGerman ? "Datenbank durchsuchen" : "Search Database";
                else if (clean.Equals("2D Map", System.StringComparison.OrdinalIgnoreCase) || clean.Equals("2D-Karte", System.StringComparison.OrdinalIgnoreCase))
                    txt.text = isGerman ? "2D-Karte" : "2D Map";
                else if (clean.Equals("Site History", System.StringComparison.OrdinalIgnoreCase) || clean.Equals("Ortsgeschichte", System.StringComparison.OrdinalIgnoreCase))
                    txt.text = isGerman ? "Ortsgeschichte" : "Site History";
                else if (clean.Equals("Settings", System.StringComparison.OrdinalIgnoreCase) || clean.Equals("Einstellungen", System.StringComparison.OrdinalIgnoreCase))
                    txt.text = isGerman ? "Einstellungen" : "Settings";
                else if (clean.Equals("MENU", System.StringComparison.OrdinalIgnoreCase) || clean.Equals("MENÜ", System.StringComparison.OrdinalIgnoreCase))
                    txt.text = isGerman ? "MENÜ" : "MENU";
            }
        }

        // 2. AR Exploration Hub (Btn_SummonGuide & GPS Quality Badge)
        if (arExplorationHub != null)
        {
            Transform summonBtn = arExplorationHub.transform.Find("Btn_SummonGuide") ?? arExplorationHub.transform.Find("BtnSummonGuide");
            if (summonBtn == null)
            {
                foreach (var b in arExplorationHub.GetComponentsInChildren<Button>(true))
                {
                    if (b.name.Contains("Summon") || b.name.Contains("Guide")) { summonBtn = b.transform; break; }
                }
            }
            if (summonBtn != null)
            {
                var tmp = summonBtn.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmp != null) tmp.text = isGerman ? "Begleiter rufen" : "Summon Guide";

                var btnComp = summonBtn.GetComponent<Button>();
                if (btnComp != null)
                {
                    btnComp.onClick.RemoveAllListeners();
                    btnComp.onClick.AddListener(SummonGuideAvatar);
                    Debug.Log($"[UIManager] Successfully wired '{summonBtn.name}' onClick listener to UIManager.SummonGuideAvatar().");
                }
            }

            Transform gpsBadge = arExplorationHub.transform.Find("GPS_Quality_Badge") ?? arExplorationHub.transform.Find("GPSQualityBadge");
            if (gpsBadge != null)
            {
                var tmp = gpsBadge.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmp != null && (tmp.text.Contains("GPS") || tmp.text.Contains("Signal") || tmp.text.Contains("Aktiv") || tmp.text.Contains("Active")))
                {
                    tmp.text = isGerman ? "GPS Aktiv" : "GPS Active";
                }
            }
        }

        // 3. 2D Map Panel (Map2DController buttons & headers)
        if (map2DPanel != null)
        {
            var allTMPs = map2DPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var txt in allTMPs)
            {
                if (txt == null || string.IsNullOrEmpty(txt.text)) continue;
                string clean = txt.text.Trim();
                if ((clean.StartsWith("Select", System.StringComparison.OrdinalIgnoreCase) || clean.StartsWith("Tour auswählen", System.StringComparison.OrdinalIgnoreCase)) && clean.Contains("Tour"))
                    txt.text = isGerman ? "Tour auswählen" : "Select a Tour";
                else if ((clean.StartsWith("Create", System.StringComparison.OrdinalIgnoreCase) || clean.StartsWith("Route erstellen", System.StringComparison.OrdinalIgnoreCase)) && clean.Contains("Route"))
                    txt.text = isGerman ? "Route erstellen" : "Create a Route";
                else if (clean.Equals("Guide Me", System.StringComparison.OrdinalIgnoreCase) || clean.Equals("Führe mich", System.StringComparison.OrdinalIgnoreCase))
                    txt.text = isGerman ? "Führe mich" : "Guide Me";
                else if (clean.Equals("2D Map", System.StringComparison.OrdinalIgnoreCase) || clean.Equals("2D-Karte", System.StringComparison.OrdinalIgnoreCase))
                    txt.text = isGerman ? "2D-Karte" : "2D Map";
            }
        }

        // 4. Database Search Panel
        if (databaseSearchPanel != null)
        {
            InitializeSearchCategoryDropdown();

            if (searchInputField != null && searchInputField.placeholder != null)
            {
                var placeholderTmp = searchInputField.placeholder.GetComponent<TextMeshProUGUI>();
                if (placeholderTmp != null)
                {
                    placeholderTmp.text = isGerman ? "Hier nach Name o. Nummer suchen..." : "Search by name or number here...";
                }
            }

            if (clearAllFiltersButton != null)
            {
                var txt = clearAllFiltersButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (txt != null) txt.text = isGerman ? "Alle Filter löschen" : "Clear All Filters";
            }

            var headerTitle = databaseSearchPanel.transform.Find("Header_Bar/Text_Title") ?? databaseSearchPanel.transform.Find("Header_Text");
            if (headerTitle != null)
            {
                var txt = headerTitle.GetComponent<TextMeshProUGUI>();
                if (txt != null) txt.text = isGerman ? "Datenbanksuche" : "Database Search";
            }
        }

        // 5. Advanced Settings Panel
        var advSettings = UnityEngine.Object.FindAnyObjectByType<AdvancedSettingsPanel>(FindObjectsInactive.Include);
        if (advSettings != null) advSettings.UpdatePanelLocalization();

        // 6. UILocalizationManager Global Sync
        if (UILocalizationManager.Instance != null) UILocalizationManager.Instance.CurrentLanguage = selectedLanguage;

        // 7. Refresh Faceted Search dynamic texts if search panel is loaded
        if (searchCacheDatabase != null && searchCacheDatabase.Count > 0)
        {
            ExecuteDynamicFacetedSearch();
        }
    }

    public void ShowMemorialDetail(string memorialID)
    {
        LoadMemorialData(memorialID, 0);
    }

    public void ShowCameraDetectionPrompt(string memorialID, string personName)
    {
        activeDetectedMemorialID = memorialID;

        if (cameraDetectionBanner != null)
        {
            cameraDetectionBanner.SetActive(true);
        }

        if (cameraDetectionText != null)
        {
            string displayName = !string.IsNullOrEmpty(personName) ? personName : memorialID;
            bool isGerman = selectedLanguage == "german";
            
            cameraDetectionText.text = isGerman 
                ? $"<b>{displayName}</b> inquadrata. Tippen für Details" 
                : $"Target: <b>{displayName}</b>. Tap for details";
        }
    }

    public void HideCameraDetectionPrompt()
    {
        activeDetectedMemorialID = null;
        if (cameraDetectionBanner != null)
        {
            cameraDetectionBanner.SetActive(false);
        }
    }

    public void OnCameraDetectionPromptClicked()
    {
        if (!string.IsNullOrEmpty(activeDetectedMemorialID))
        {
            string idToOpen = activeDetectedMemorialID;
            HideCameraDetectionPrompt();
            ShowMemorialDetail(idToOpen);
        }
    }

    public void RestoreMemorialDetailAfterInspector()
    {
        // ⚡ UX FIX: Instead of blindly popping the navigation stack, we just make sure the 
        // memorial panel becomes visible again if it's the one we expect to return to.
        if (memorialDetailPanel != null && navigationStack.Count > 0 && navigationStack.Peek() == memorialDetailPanel)
        {
            memorialDetailPanel.SetActive(true);
            PlayPanelAnimation(true);
            Debug.Log("[UI Navigation] Restored Memorial Detail Panel safely without popping stack.");
        }
        else if (navigationStack.Count > 0)
        {
            // If something else was on top, just activate it
            navigationStack.Peek().SetActive(true);
        }
    }

    public void HideMemorialDetail()
    {
        bool continueNarrationInHub = audioController != null && audioController.IsPlaybackActive;
        if (continueNarrationInHub)
        {
            currentMemorial = null;
            currentMemorialID = null;
            ReturnToExplorationHub();
            return;
        }

        if (audioController != null) audioController.ForceStopPlayback();

        PlayPanelAnimation(false);
        ClearDetailTexts();
        
        // FAILSAFE: We MUST pop the memorial panel from the stack when explicitly closed,
        // otherwise it stays in memory and corrupts future "CloseCurrentAndReturn" logic
        // which makes the user see the wrong panel.
        if (navigationStack.Count > 0 && navigationStack.Peek() == memorialDetailPanel)
        {
            navigationStack.Pop();
            
            // Optionally, restore the previous panel so we don't end up with an empty screen
            if (navigationStack.Count > 0)
            {
                navigationStack.Peek().SetActive(true);
            }
        }

        thesisManager?.LogEvent("ui_closed", currentMemorialID ?? string.Empty, "detail_panel");
        currentMemorial = null;
        currentMemorialID = null;
        SetGuideSubtitlesSuspended(false);
    }

    public void ClosePanelAndReturn(GameObject panel)
    {
        if (panel != null && navigationStack.Count > 0 && navigationStack.Peek() == panel)
        {
            CloseCurrentAndReturn();
        }
        else if (panel != null)
        {
            panel.SetActive(false);
        }

        if (memorialDetailPanel == null || !memorialDetailPanel.activeSelf)
        {
            SetGuideSubtitlesSuspended(false);
        }
    }

    private void ConfirmAndOpenFullDetails()
    {
        Debug.Log($"[UI Diagnostic] ConfirmAndOpenFullDetails event triggered. Target ID to load: '{currentSelectedMapStoneID}'");

        if (string.IsNullOrEmpty(currentSelectedMapStoneID))
        {
            Debug.LogError("[UI Diagnostic] Aborting screen navigation: currentSelectedMapStoneID is completely null or empty.");
            return;
        }

        if (mapMiniPopupPanel != null) mapMiniPopupPanel.SetActive(false);
        if (map2DPanel != null) map2DPanel.SetActive(false);

        // Execute operational layout loading sequence
        LoadMemorialData(currentSelectedMapStoneID, 0);
    }

    private void LoadMemorialData(string stoneID, int personIndex)
    {
        Debug.Log($"[UI Diagnostic] LoadMemorialData invoking pipeline sequence for ID: '{stoneID}' at personIndex: {personIndex}");

        if (string.IsNullOrWhiteSpace(stoneID))
        {
            Debug.LogWarning("[UI Diagnostic] Ignored a detail-panel request with an empty memorial ID.");
            return;
        }

        if (dataManager == null) dataManager = UnityEngine.Object.FindAnyObjectByType<MemorialDataManager>(FindObjectsInactive.Include);
        if (dataManager == null)
        {
            Debug.LogError("[UI Diagnostic] Critical Dependency Failure: MemorialDataManager instance is missing from the active Scene context.");
            return;
        }

        // Verify if database reporting initialized correctly
        Debug.Log($"[UI Diagnostic] Checking DataManager status... Is Database Loaded in memory? {dataManager.IsLoaded}");

        currentMemorial = dataManager.GetDataByID(stoneID);

        if (currentMemorial != null)
        {
            Debug.Log($"[UI Diagnostic] Success! Core data structure resolved matching type: {currentMemorial.GetType().Name}");
            currentMemorialID = stoneID;
            currentPersonIndex = personIndex;

            SynchronizeDebugIndex(stoneID);

            if (memorialDetailPanel != null)
            {
                SetGuideSubtitlesSuspended(true);
                memorialDetailPanel.SetActive(true);
                OpenPanel(memorialDetailPanel);
                Debug.Log("[UI Diagnostic] Master descriptive card panel layout successfully opened and pushed to navigation stack.");

                var tutorial = UnityEngine.Object.FindAnyObjectByType<CoachmarkTutorialController>(FindObjectsInactive.Include);
                if (tutorial != null)
                {
                    tutorial.OnMemorialDetailPanelOpened();
                }
            }
            else
            {
                Debug.LogError("[UI Diagnostic] Component reference missing: memorialDetailPanel slot is null inside Inspector variables.");
            }

            UpdateDetailDisplay();
            Refresh3DModelInspectorAvailability();
            PlayPanelAnimation(true);
            thesisManager?.LogEvent("ui_opened", stoneID, "detail_panel");
        }
        else
        {
            Debug.LogError($"[UI Diagnostic] Core Content Failure: The database returned NULL for identifier string: '{stoneID}'. The detail canvas layout cannot display empty rows.");
        }
    }

    private void SynchronizeDebugIndex(string stoneID)
    {
        if (debugMemorialIDs != null && debugMemorialIDs.Contains(stoneID))
        {
            jsonDebugIndex = debugMemorialIDs.IndexOf(stoneID);
        }
    }

    private void UpdateDetailDisplay()
    {
        if (currentMemorial is MemorialDataManager.MemorialStone stone) DisplayStoneDetail(stone);
        else if (currentMemorial is MemorialDataManager.MassGrave grave) DisplayGraveDetail(grave);
        else if (currentMemorial is MemorialDataManager.OtherMemorial memorial) DisplayOtherMemorialDetail(memorial);
    }

    private static readonly Dictionary<string, (string en, string de, string he)> DetailPanelStrings = new Dictionary<string, (string, string, string)>
    {
        ["memorial_stone"]          = ("Memorial Stone", "Gedenkstein", "אבן זיכרון"),
        ["mass_grave"]              = ("Mass Grave", "Massengrab", "קבר אחים"),
        ["label_location"]          = ("Location", "Standort", "מיקום"),
        ["label_scanned"]           = ("Scanned", "Gescannt", "נסרק"),
        ["yes"]                     = ("Yes", "Ja", "כן"),
        ["no"]                      = ("No", "Nein", "לא"),
        ["label_estimated_deaths"]  = ("Estimated Deaths", "Geschätzte Todesfälle", "מספר הרוגים משוער"),
        ["label_notes"]             = ("Notes", "Notizen", "הערות"),
        ["no_notes"]                = ("No additional notes archived.", "Keine weiteren Notizen archiviert.", "לא נשמרו הערות נוספות."),
    };

    private string DP(string key)
    {
        if (!DetailPanelStrings.TryGetValue(key, out var entry))
        {
            Debug.LogWarning($"[UIManager] Missing DetailPanelStrings entry for key '{key}'. Falling back to key itself.");
            return key;
        }

        AppLanguage lang = selectedLanguage.ToAppLanguage();
        string value = lang switch
        {
            AppLanguage.DE => entry.de,
            AppLanguage.HE => entry.he,
            _ => entry.en
        };

        return string.IsNullOrEmpty(value) ? entry.en : value;
    }

    private void DisplayStoneDetail(MemorialDataManager.MemorialStone stone)
    {
        if (titleText != null)
        {
            titleText.text = releaseMode ? DP("memorial_stone") : $"{DP("memorial_stone")} {stone.id}";
        }

        bool diagMode = PlayerPrefs.GetInt("Thesis_DiagnosticMode", 0) == 1;
        string desc = diagMode
            ? $"<b>{DP("label_location")}:</b> ({stone.latitude:F6}, {stone.longitude:F6})\n<b>{DP("label_scanned")}:</b> {(stone.scan ? DP("yes") : DP("no"))}"
            : "";

        if (descriptionText != null)
        {
            descriptionText.text = desc;
            descriptionText.gameObject.SetActive(diagMode);
        }

        bool multiple = stone.persons != null && stone.persons.Count > 1;
        if (nextPersonButton != null) nextPersonButton.gameObject.SetActive(multiple);
        if (prevPersonButton != null) prevPersonButton.gameObject.SetActive(multiple);

        if (prevStoneButton != null) prevStoneButton.gameObject.SetActive(true);
        if (nextStoneButton != null) nextStoneButton.gameObject.SetActive(true);

        if (stone.persons != null && stone.persons.Count > 0)
        {
            if (currentPersonIndex >= stone.persons.Count) currentPersonIndex = 0;
            DisplayPersonDetail(stone.persons[currentPersonIndex], currentPersonIndex, stone.persons.Count, stone);
        }
    }

    private void DisplayGraveDetail(MemorialDataManager.MassGrave grave)
    {
        if (titleText != null)
        {
            titleText.text = releaseMode ? DP("mass_grave") : $"{DP("mass_grave")} {grave.id}";
        }

        if (descriptionText != null)
        {
            descriptionText.text = releaseMode 
                ? $"<b>{DP("label_estimated_deaths")}:</b> {grave.death_count:N0}" 
                : $"<b>{DP("label_location")}:</b> ({grave.latitude:F6}, {grave.longitude:F6})\n<b>{DP("label_estimated_deaths")}:</b> {grave.death_count:N0}";
        }

        if (fullNameText != null) 
        {
            fullNameText.text = releaseMode
                ? (string.IsNullOrEmpty(grave.description) ? DP("mass_grave") : grave.description)
                : (string.IsNullOrEmpty(grave.description) ? $"{DP("mass_grave")} {grave.id}" : grave.description);
        }

        if (datesText != null) datesText.text = string.Empty;
        if (inmateNumberText != null) inmateNumberText.text = string.Empty;

        if (personsListText != null) personsListText.text = $"<b>{DP("label_notes")}:</b>\n{(string.IsNullOrEmpty(grave.notes) ? DP("no_notes") : grave.notes)}";

        if (nextPersonButton != null) nextPersonButton.gameObject.SetActive(false);
        if (prevPersonButton != null) prevPersonButton.gameObject.SetActive(false);
        if (symbolBadge1 != null) symbolBadge1.SetActive(false);
        if (symbolBadge2 != null) symbolBadge2.SetActive(false);
    }

    private void DisplayOtherMemorialDetail(MemorialDataManager.OtherMemorial memorial)
    {
        if (titleText != null) titleText.text = $"{memorial.description}";
        
        if (descriptionText != null)
        {
            descriptionText.text = releaseMode 
                ? string.Empty 
                : $"<b>ID:</b> {memorial.id}\n<b>{DP("label_location")}:</b> ({memorial.latitude:F6}, {memorial.longitude:F6})";
        }
        
        if (fullNameText != null) fullNameText.text = memorial.description;

        if (datesText != null) datesText.text = string.Empty;
        if (inmateNumberText != null) inmateNumberText.text = string.Empty;
        if (personsListText != null)
        {
            string notes = string.IsNullOrEmpty(memorial.notes) ? DP("no_notes") : memorial.notes;
            // OM0 stores the authentic inscription in both languages in one field.
            // Show the appropriate half rather than presenting German to English users.
            const string englishMarker = "THE GRAVESTONES";
            int englishStart = notes.IndexOf(englishMarker, System.StringComparison.OrdinalIgnoreCase);
            if (englishStart >= 0 && selectedLanguage != "german") notes = notes.Substring(englishStart).Trim();
            else if (englishStart >= 0) notes = notes.Substring(0, englishStart).Trim();
            personsListText.text = $"<b>{DP("label_notes")}:</b>\n{notes}";
        }

        if (nextPersonButton != null) nextPersonButton.gameObject.SetActive(false);
        if (prevPersonButton != null) prevPersonButton.gameObject.SetActive(false);
        if (symbolBadge1 != null) symbolBadge1.SetActive(false);
        if (symbolBadge2 != null) symbolBadge2.SetActive(false);
    }

    private void DisplayPersonDetail(MemorialDataManager.Person person, int index, int total, MemorialDataManager.MemorialStone stone)
    {
        // Re-resolve badge targets directly inside active memorialDetailPanel to guarantee correct references
        if (memorialDetailPanel != null)
        {
            Transform bg = memorialDetailPanel.transform.Find("Badges_Horizontal_Group") ?? memorialDetailPanel.transform.Find("Person_Info_Area/Badges_Horizontal_Group");
            if (bg == null)
            {
                foreach (var t in memorialDetailPanel.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Badges_Horizontal_Group") { bg = t; break; }
                }
            }
            if (bg != null)
            {
                Transform b1 = bg.Find("Badge_Symbol");
                Transform b2 = bg.Find("Badge_Camp");
                if (b1 != null)
                {
                    symbolBadge1 = b1.gameObject;
                    symbolBadge1Text = b1.GetComponentInChildren<TextMeshProUGUI>(true);
                }
                if (b2 != null)
                {
                    symbolBadge2 = b2.gameObject;
                    symbolBadge2Text = b2.GetComponentInChildren<TextMeshProUGUI>(true);
                }
            }
        }

        bool isGerman = string.Equals(selectedLanguage, "german", System.StringComparison.OrdinalIgnoreCase);

        if (fullNameText != null)
            fullNameText.text = $"{person.forename} {person.surname}";

        if (datesText != null)
        {
            datesText.enableAutoSizing = false;
            datesText.fontSize = 24f;
            string bDay = FormatNormalizedDate(person.date_of_birth);
            string dDay = FormatNormalizedDate(person.date_of_death);
            string ageWord = isGerman ? "Alter" : "Age";
            string age = person.age_at_death.HasValue ? $" ({ageWord} {person.age_at_death})" : "";
            datesText.text = isGerman
                ? $"<b>Geboren:</b> {bDay}\n<b>Gestorben:</b> {dDay}{age}"
                : $"<b>Born:</b> {bDay}\n<b>Died:</b> {dDay}{age}";
        }

        if (inmateNumberText != null)
        {
            inmateNumberText.enableAutoSizing = false;
            inmateNumberText.fontSize = 24f;
            string num = !string.IsNullOrEmpty(person.inmate_number) ? person.inmate_number : "N/A";
            inmateNumberText.text = isGerman ? $"<b>Häftlingsnr.:</b> {num}" : $"<b>Inmate N°:</b> {num}";
        }

        // Split stone symbols by comma; assign one symbol per badge slot
        string rawSymbols = !string.IsNullOrEmpty(stone.symbols) ? stone.symbols.Trim()
                          : (!string.IsNullOrEmpty(person.religion) ? person.religion : "");

        string[] symbolTokens = string.IsNullOrEmpty(rawSymbols)
            ? System.Array.Empty<string>()
            : System.Array.ConvertAll(rawSymbols.Split(','), s => s.Trim());

        PopulateSymbolBadge(symbolBadge1, symbolBadge1Text, symbolTokens.Length > 0 ? symbolTokens[0] : "");
        // Badge2: second symbol if present (clean individual string)
        string badge2Val = symbolTokens.Length > 1 ? symbolTokens[1] : "";
        PopulateSymbolBadge(symbolBadge2, symbolBadge2Text, badge2Val);
        EnsureSymbolAudioHint(symbolTokens.Length > 0);


        if (personsListText != null)
        {
            personsListText.fontSize = 36f;
            personsListText.enableAutoSizing = false;

            string contextLine = isGerman ? $"<b>Person {index + 1} von {total}</b>\n\n" : $"<b>Person {index + 1} of {total}</b>\n\n";
            string inscription = person.english_inscription;

            string activePopLang = string.IsNullOrEmpty(popupSelectedLanguage) ? selectedLanguage : popupSelectedLanguage;

            if (string.Equals(activePopLang, "german", System.StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(person.german_inscription))
                inscription = person.german_inscription;
            else if (string.Equals(activePopLang, "hebrew", System.StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(person.hebrew_inscription))
                inscription = person.hebrew_inscription;

            if (string.IsNullOrEmpty(inscription))
                inscription = stone.book_text;

            personsListText.text = contextLine + inscription;
        }
    }

    // Runtime affordance for the interactive symbol badges.
    private void EnsureSymbolAudioHint(bool visible)
    {
        Transform badgeGroup = symbolBadge1 != null ? symbolBadge1.transform.parent : null;
        if (badgeGroup == null) return;

        Transform existing = badgeGroup.Find("Symbol_Audio_Hint");
        if (!visible)
        {
            if (existing != null) existing.gameObject.SetActive(false);
            return;
        }

        if (existing == null)
        {
            GameObject hint = new GameObject("Symbol_Audio_Hint", typeof(RectTransform), typeof(TextMeshProUGUI));
            hint.transform.SetParent(badgeGroup, false);
            RectTransform rect = hint.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -8f);
            rect.sizeDelta = new Vector2(0f, 28f);
            existing = hint.transform;
        }

        TextMeshProUGUI text = existing.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = 18f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 1f, 1f, 0.8f);
        text.text = selectedLanguage == "german" ? "Symbole antippen, um sie anzuhören" : "Tap a symbol to hear its meaning";
        existing.gameObject.SetActive(true);
    }

    private void PopulateSymbolBadge(GameObject badge, TextMeshProUGUI badgeText, string symbolName)
    {
        if (badge == null || badgeText == null) return;
        if (string.IsNullOrWhiteSpace(symbolName))
        {
            badge.SetActive(false);
            return;
        }
        badge.SetActive(true);
        badgeText.enableAutoSizing = false;
        badgeText.fontSize = 18f;

        bool isGerman = string.Equals(selectedLanguage, "german", System.StringComparison.OrdinalIgnoreCase);
        string lower = symbolName.ToLower();
        string displaySymbolName = symbolName;
        if (isGerman)
        {
            if (lower.Contains("star") || lower.Contains("david") || lower.Contains("magen")) displaySymbolName = "Davidstern";
            else if (lower.Contains("cross") || lower.Contains("christ")) displaySymbolName = "Kreuz";
            else if (lower.Contains("broken") || lower.Contains("tree")) displaySymbolName = "Gebrauchter Baum";
            else if (lower.Contains("political")) displaySymbolName = "Politischer Häftling";
        }
        badgeText.text = displaySymbolName;

        var btn = badge.GetComponent<UnityEngine.UI.Button>() ?? badge.AddComponent<UnityEngine.UI.Button>();
        btn.onClick.RemoveAllListeners();

        string key = "";
        if (lower.Contains("star") || lower.Contains("david") || lower.Contains("magen") || lower.Contains("jew")) key = "StarOfDavid";
        else if (lower.Contains("cross") || lower.Contains("christ")) key = "Cross";
        else if (lower.Contains("broken") || lower.Contains("tree")) key = "BrokenTreeTrunk";
        else if (lower.Contains("menorah")) key = "Menorah";
        else if (lower.Contains("polish") || lower.Contains("coat of arms")) key = "PolishCoatOfArms";
        else if (lower.Contains("belgian")) key = "BelgianFlag";
        else if (lower.Contains("flag")) key = "NationalFlag";
        else if (lower.Contains("blessing") || lower.Contains("kohanim") || lower.Contains("kohen")) key = "BlessingHandsKohen";
        else if (lower.Contains("photo")) key = "InsetPhoto";

        btn.interactable = !string.IsNullOrEmpty(key);

        if (!string.IsNullOrEmpty(key))
        {
            string capturedKey = key;
            btn.onClick.AddListener(() =>
            {
                string suffix = selectedLanguage.ToAppLanguage().ToFileSuffix();
                if (NarrationManager.Instance != null)
                    NarrationManager.Instance.PlayNarration($"Symbol_{capturedKey}_{suffix}");
                else PlaySymbolFallback(capturedKey);
            });
        }
    }

    /// <summary>
    /// Intercepts button triggers to route play/pause actions straight into the active audio pipeline.
    /// </summary>

    private void HandleAudioPlaybackClicked()
    {
        if (!string.IsNullOrEmpty(currentMemorialID))
        {
            if (audioController == null)
            {
                audioController = GetComponent<PopupAudioController>() ?? UnityEngine.Object.FindAnyObjectByType<PopupAudioController>(FindObjectsInactive.Include);
            }
            if (audioController != null)
            {
                audioController.ToggleAudioPlayback(currentMemorialID);
            }
        }
    }

    public void Open3DModelInspector()
    {
        var tutorial = UnityEngine.Object.FindAnyObjectByType<CoachmarkTutorialController>(FindObjectsInactive.Include);
        if (tutorial != null && tutorial.IsTutorialEnabled && tutorial.CurrentStepIndex == 6)
        {
            return; // Intercepted during Step 6: tutorial advances to Step 7 without opening 3D Inspector modal
        }

        var inspector = ModelInspectorUI.Instance;
        if (!HasInspectableModel()) return;
        if (inspector != null && !string.IsNullOrEmpty(currentMemorialID))
        {
            if (mapMiniPopupPanel != null) mapMiniPopupPanel.SetActive(false);
            if (inspector.GetInspectorPanel() != null)
            {
                // ⚡ UX FIX: Do NOT push the inspector into the UIManager navigation stack!
                // It is an overlay that closes itself. Pushing it corrupts the stack.
                inspector.GetInspectorPanel().SetActive(true);
                
                // Hide the memorial detail panel underneath it
                if (memorialDetailPanel != null) memorialDetailPanel.SetActive(false);
            }
            inspector.OpenInspector(currentMemorialID);
        }
        else
        {
            Debug.LogWarning($"[UIManager] Cannot open 3D Model Inspector: Instance={inspector != null}, stoneID='{currentMemorialID}'");
        }
    }

    public void ShowNextPerson()
    {
        if (currentMemorial is MemorialDataManager.MemorialStone stone && stone.persons != null && stone.persons.Count > 0)
        {
            currentPersonIndex = (currentPersonIndex + 1) % stone.persons.Count;
            DisplayPersonDetail(stone.persons[currentPersonIndex], currentPersonIndex, stone.persons.Count, stone);
        }
    }

    public void ShowPreviousPerson()
    {
        if (currentMemorial is MemorialDataManager.MemorialStone stone && stone.persons != null && stone.persons.Count > 0)
        {
            currentPersonIndex = (currentPersonIndex - 1 + stone.persons.Count) % stone.persons.Count;
            DisplayPersonDetail(stone.persons[currentPersonIndex], currentPersonIndex, stone.persons.Count, stone);
        }
    }

    public void ShowNextDebugMemorial()
    {
        if (debugMemorialIDs.Count == 0) return;
        jsonDebugIndex = (jsonDebugIndex + 1) % debugMemorialIDs.Count;
        LoadMemorialData(debugMemorialIDs[jsonDebugIndex], 0);
    }

    public void ShowPreviousDebugMemorial()
    {
        if (debugMemorialIDs.Count == 0) return;
        jsonDebugIndex = (jsonDebugIndex - 1 + debugMemorialIDs.Count) % debugMemorialIDs.Count;
        LoadMemorialData(debugMemorialIDs[jsonDebugIndex], 0);
    }

    private void InitializeSearchCache()
    {
        if (dataManager == null) dataManager = UnityEngine.Object.FindAnyObjectByType<MemorialDataManager>(FindObjectsInactive.Include);
        if (dataManager != null && !dataManager.IsLoaded) dataManager.LoadData();
        if (dataManager == null || !dataManager.IsLoaded) return;
        if (searchCacheDatabase != null && searchCacheDatabase.Count > 0) return;

        debugMemorialIDs = new List<string>();
        searchCacheDatabase = new List<SearchCacheItem>();

        foreach (var stone in dataManager.GetAllMemorialStones())
        {
            if (string.IsNullOrEmpty(stone.id)) continue;
            if (!debugMemorialIDs.Contains(stone.id)) debugMemorialIDs.Add(stone.id);

            if (stone.persons == null || stone.persons.Count == 0)
            {
                SearchCacheItem fallbackItem = new SearchCacheItem
                {
                    stoneID = stone.id,
                    personIndex = 0,
                    correctForename = string.Empty,
                    correctSurname = string.Empty,
                    birthDateRaw = string.Empty,
                    deathDateRaw = string.Empty,
                    inmateNumber = string.Empty,
                    placeOfBirth = string.Empty,
                    placeOfDeath = string.Empty,
                    symbolsRaw = stone.symbols ?? string.Empty
                };

                fallbackItem.namesSearchText = string.Empty;
                fallbackItem.inmateSearchText = string.Empty;
                fallbackItem.birthPlaceSearchText = string.Empty;
                fallbackItem.deathPlaceSearchText = string.Empty;
                fallbackItem.birthDateSearchText = string.Empty;
                fallbackItem.deathDateSearchText = string.Empty;
                fallbackItem.symbolsSearchText = (stone.symbols ?? string.Empty).ToLower().Trim();
                fallbackItem.inscriptionDisplayText = ((stone.book_text ?? string.Empty) + " " + (stone.notes ?? string.Empty)).Trim();
                fallbackItem.inscriptionsSearchText = fallbackItem.inscriptionDisplayText.ToLower();
                fallbackItem.globalSearchText = (stone.id + " " + fallbackItem.inscriptionsSearchText + " " + fallbackItem.symbolsSearchText).ToLower();

                searchCacheDatabase.Add(fallbackItem);
                continue;
            }

            for (int p = 0; p < stone.persons.Count; p++)
            {
                var person = stone.persons[p];
                SearchCacheItem item = new SearchCacheItem
                {
                    stoneID = stone.id,
                    personIndex = p,
                    correctForename = person.forename ?? "",
                    correctSurname = person.surname ?? "",
                    birthDateRaw = person.date_of_birth ?? "",
                    deathDateRaw = person.date_of_death ?? "",
                    inmateNumber = person.inmate_number ?? "",
                    placeOfBirth = person.place_of_birth ?? "",
                    placeOfDeath = person.place_of_death ?? "",
                    symbolsRaw = person.other_links ?? ""
                };

                item.namesSearchText = (item.correctForename + " " + item.correctSurname).ToLower().Trim();
                item.inmateSearchText = item.inmateNumber.ToLower().Trim();
                item.birthPlaceSearchText = item.placeOfBirth.ToLower().Trim();
                item.deathPlaceSearchText = item.placeOfDeath.ToLower().Trim();
                item.birthDateSearchText = item.birthDateRaw.Replace('.', '/').ToLower().Trim();
                item.deathDateSearchText = item.deathDateRaw.Replace('.', '/').ToLower().Trim();
                item.symbolsSearchText = item.symbolsRaw.ToLower().Trim();

                string tEng = person.english_inscription ?? stone.book_text ?? "";
                string tGer = person.german_inscription ?? "";
                string notes = stone.notes ?? "";
                item.inscriptionDisplayText = (tEng + " " + tGer + " " + notes).Trim();
                item.inscriptionsSearchText = item.inscriptionDisplayText.ToLower();

                item.globalSearchText = (stone.id + " " + item.namesSearchText + " " + item.inmateSearchText + " " +
                                         item.birthPlaceSearchText + " " + item.deathPlaceSearchText + " " +
                                         item.birthDateSearchText + " " + item.deathDateSearchText + " " +
                                         item.inscriptionsSearchText + " " + item.symbolsSearchText).ToLower();

                searchCacheDatabase.Add(item);
            }
        }

        foreach (var grave in dataManager.GetAllMassGraves())
        {
            if (!string.IsNullOrEmpty(grave.id) && !debugMemorialIDs.Contains(grave.id))
                debugMemorialIDs.Add(grave.id);
        }

        foreach (var memorial in dataManager.GetAllOtherMemorials())
        {
            if (!string.IsNullOrEmpty(memorial.id) && !debugMemorialIDs.Contains(memorial.id))
                debugMemorialIDs.Add(memorial.id);
        }

    }

    public void AddFilterFromUI()
    {
        FilterCategory selectedCategory = (FilterCategory)searchCategoryDropdown.value;
        string filterValue = string.Empty;

        if (selectedCategory == FilterCategory.Symbols)
        {
            if (searchSymbolsDropdown == null || searchSymbolsDropdown.value == 0) return;
            filterValue = searchSymbolsDropdown.options[searchSymbolsDropdown.value].text;
            searchSymbolsDropdown.value = 0;
        }
        else
        {
            if (searchInputField == null || string.IsNullOrEmpty(searchInputField.text)) return;
            filterValue = searchInputField.text.Trim();
            if (filterValue.Length < 2) return;
            searchInputField.text = string.Empty;
            
            // Release focus to restore keyboard WASD movement immediately
            searchInputField.DeactivateInputField();
            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            }
        }

        AddFilterToken(selectedCategory, filterValue);
    }

    public void AddFilterToken(FilterCategory category, string value)
    {
        if (activeFilters.ContainsKey(category))
        {
            RemoveFilterToken(category, false);
        }

        activeFilters[category] = value.ToLower();

        if (filterBadgePrefab != null && filterBadgesContainer != null)
        {
            GameObject badge = Instantiate(filterBadgePrefab, filterBadgesContainer);
            badge.SetActive(true); // Fix Bug 1: Ensure filter badge is active
            activeBadgeObjects[category] = badge;

            Image badgeImage = badge.GetComponent<Image>();
            if (badgeImage != null)
            {
                badgeImage.color = GetCategoryColor(category);
            }

            var badgeText = badge.GetComponentInChildren<TextMeshProUGUI>();
            if (badgeText != null)
            {
                string friendlyLabel = category.ToString().Replace("Place", " Place").Replace("Date", " Date");
                badgeText.text = $"{friendlyLabel.ToLower()}: <b>{value}</b>";
            }

            var closeBtn = badge.GetComponentInChildren<Button>();
            if (closeBtn != null)
            {
                closeBtn.onClick.AddListener(() => RemoveFilterToken(category, true));
            }
        }

        ExecuteDynamicFacetedSearch();
    }

    public void RemoveFilterToken(FilterCategory category, bool triggerReSearch)
    {
        if (activeFilters.ContainsKey(category)) activeFilters.Remove(category);

        if (activeBadgeObjects.TryGetValue(category, out GameObject badgeObj))
        {
            if (badgeObj != null) Destroy(badgeObj);
            activeBadgeObjects.Remove(category);
        }

        if (triggerReSearch)
        {
            ExecuteDynamicFacetedSearch();
        }
    }

    public void ClearAllFilters()
    {
        List<FilterCategory> categoriesToWipe = new List<FilterCategory>(activeFilters.Keys);
        foreach (FilterCategory cat in categoriesToWipe)
        {
            RemoveFilterToken(cat, false);
        }

        if (searchInputField != null) searchInputField.text = string.Empty;
        ExecuteDynamicFacetedSearch();
    }

    private Color GetCategoryColor(FilterCategory category)
    {
        switch (category)
        {
            case FilterCategory.FirstName: return new Color(0.18f, 0.48f, 0.93f, 0.9f);
            case FilterCategory.Surname: return new Color(0.18f, 0.48f, 0.93f, 0.9f);
            case FilterCategory.InmateNumbers: return new Color(0.95f, 0.57f, 0.08f, 0.9f);
            case FilterCategory.BirthPlace: return new Color(0.04f, 0.67f, 0.72f, 0.9f);
            case FilterCategory.DeathPlace: return new Color(0.85f, 0.22f, 0.22f, 0.9f);
            case FilterCategory.BirthDate: return new Color(0.13f, 0.68f, 0.38f, 0.9f);
            case FilterCategory.DeathDate: return new Color(0.55f, 0.11f, 0.25f, 0.9f);
            case FilterCategory.Symbols: return new Color(0.61f, 0.34f, 0.82f, 0.9f);
            case FilterCategory.InscriptionsNotes: return new Color(0.45f, 0.45f, 0.48f, 0.9f);
            default: return new Color(0.3f, 0.3f, 0.3f, 0.9f);
        }
    }

    private static string GetGameObjectPath(GameObject obj)
    {
        if (obj == null) return "null";
        string path = obj.name;
        while (obj.transform.parent != null)
        {
            obj = obj.transform.parent.gameObject;
            path = obj.name + "/" + path;
        }
        return path;
    }

    public void ExecuteDynamicFacetedSearch()
    {
        bool isGerman = string.Equals(selectedLanguage, "german", System.StringComparison.OrdinalIgnoreCase);

        if (searchCacheDatabase == null || searchCacheDatabase.Count == 0)
        {
            InitializeSearchCache();
        }

        Debug.Log($"[SEARCH-DEBUG] searchCacheDatabase count = {(searchCacheDatabase != null ? searchCacheDatabase.Count : -1)}");

        foreach (GameObject b in buttonPool) b.SetActive(false);

        if (clearAllFiltersButton != null)
        {
            clearAllFiltersButton.gameObject.SetActive(activeFilters.Count > 0);
        }
        string liveTypingText = searchInputField != null ? searchInputField.text.ToLower().Trim() : "";
        FilterCategory liveTypingCategory = searchCategoryDropdown != null ? (FilterCategory)searchCategoryDropdown.value : FilterCategory.AllFields;
        bool hasLiveTyping = !string.IsNullOrEmpty(liveTypingText) && liveTypingText.Length >= 2;

        Debug.Log($"[SEARCH-DEBUG] liveTypingText='{liveTypingText}', hasLiveTyping={hasLiveTyping}, activeFilters={activeFilters.Count}");

        if (activeFilters.Count == 0 && !hasLiveTyping)
        {
            if (searchCounterText != null) searchCounterText.text = SEARCH_HINT_MESSAGE;
            Debug.Log("[SEARCH-DEBUG] EARLY RETURN: no filters and no live typing (need >= 2 chars)");
            return;
        }

        int matchingCount = 0;
        int activeLayoutIndex = 0;

        if (databaseSearchPanel == null)
        {
            var t = transform.Find("Database_Search_Panel");
            if (t != null) databaseSearchPanel = t.gameObject;
            else databaseSearchPanel = FindGameObjectIncludingInactive("Database_Search_Panel") ?? FindGameObjectIncludingInactive("DatabaseSearchPanel");
        }

        if (searchResultsContainer == null && databaseSearchPanel != null)
        {
            foreach (var t in databaseSearchPanel.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Equals("Content", System.StringComparison.OrdinalIgnoreCase) && t.parent != null && t.parent.name.Equals("Viewport", System.StringComparison.OrdinalIgnoreCase))
                {
                    searchResultsContainer = t;
                    break;
                }
            }

            if (searchResultsContainer == null)
            {
                Transform searchScrollView = databaseSearchPanel.transform.Find("Search_Results_Scroll_View") ??
                                             databaseSearchPanel.transform.Find("Search_Results_ScrollView") ??
                                             databaseSearchPanel.transform.Find("ScrollView");
                if (searchScrollView != null)
                {
                    var sr = searchScrollView.GetComponent<UnityEngine.UI.ScrollRect>();
                    if (sr != null && sr.content != null) searchResultsContainer = sr.content;
                }
            }
        }

        if (searchResultsContainer == null)
        {
            Canvas canvas = uiCanvas ?? FindComponentInScene<Canvas>("Canvas");
            if (canvas != null)
            {
                Transform found = canvas.transform.Find("Database_Search_Panel/Search_Results_Scroll_View/Viewport/Content") ??
                                 canvas.transform.Find("Database_Search_Panel/Search_Results_ScrollView/Viewport/Content");
                if (found != null) searchResultsContainer = found;
            }
        }

        // If the user placed a prototype button directly inside searchResultsContainer in the scene, grab it!
        if (searchResultButtonPrefab == null && searchResultsContainer != null)
        {
            for (int i = 0; i < searchResultsContainer.childCount; i++)
            {
                var child = searchResultsContainer.GetChild(i).gameObject;
                if (child.name.ToLower().Contains("button") || child.name.ToLower().Contains("result"))
                {
                    searchResultButtonPrefab = child;
                    child.SetActive(false);
                    Debug.Log($"[SEARCH-DEBUG] Captured prototype button from scene container: {child.name}");
                    break;
                }
            }
        }

        if (searchResultButtonPrefab == null)
        {
            searchResultButtonPrefab = Resources.Load<GameObject>("Prefabs/searchResultButtonPrefab") ?? 
                                       Resources.Load<GameObject>("searchResultButtonPrefab") ??
                                       Resources.Load<GameObject>("SearchResultButtonPrefab") ??
                                       Resources.Load<GameObject>("UI/searchResultButtonPrefab");
        }

        if (searchResultsContainer != null)
        {
            Debug.Log($"[SEARCH-DEBUG] Target searchResultsContainer path: {searchResultsContainer.name}");
        }

        foreach (SearchCacheItem item in searchCacheDatabase)
        {
            bool isRowMatch = true;

            foreach (var filter in activeFilters)
            {
                bool badgeConditionSatisfied = false;
                string queryValue = filter.Value;

                switch (filter.Key)
                {
                    case FilterCategory.AllFields: if (item.globalSearchText.Contains(queryValue)) badgeConditionSatisfied = true; break;
                    case FilterCategory.FirstName: if (item.correctForename.ToLower().Contains(queryValue)) badgeConditionSatisfied = true; break;
                    case FilterCategory.Surname: if (item.correctSurname.ToLower().Contains(queryValue)) badgeConditionSatisfied = true; break;
                    case FilterCategory.InmateNumbers: if (item.inmateSearchText.Contains(queryValue)) badgeConditionSatisfied = true; break;
                    case FilterCategory.BirthPlace: if (item.birthPlaceSearchText.Contains(queryValue)) badgeConditionSatisfied = true; break;
                    case FilterCategory.DeathPlace: if (item.deathPlaceSearchText.Contains(queryValue)) badgeConditionSatisfied = true; break;
                    case FilterCategory.BirthDate: if (item.birthDateSearchText.Contains(queryValue)) badgeConditionSatisfied = true; break;
                    case FilterCategory.DeathDate: if (item.deathDateSearchText.Contains(queryValue)) badgeConditionSatisfied = true; break;
                    case FilterCategory.InscriptionsNotes: if (item.inscriptionsSearchText.Contains(queryValue)) badgeConditionSatisfied = true; break;
                    case FilterCategory.Symbols: if (item.symbolsSearchText.Contains(queryValue)) badgeConditionSatisfied = true; break;
                }

                if (!badgeConditionSatisfied) { isRowMatch = false; break; }
            }

            if (isRowMatch && hasLiveTyping)
            {
                bool liveConditionSatisfied = false;
                switch (liveTypingCategory)
                {
                    case FilterCategory.AllFields: if (item.globalSearchText.Contains(liveTypingText)) liveConditionSatisfied = true; break;
                    case FilterCategory.FirstName: if (item.correctForename.ToLower().Contains(liveTypingText)) liveConditionSatisfied = true; break;
                    case FilterCategory.Surname: if (item.correctSurname.ToLower().Contains(liveTypingText)) liveConditionSatisfied = true; break;
                    case FilterCategory.InmateNumbers: if (item.inmateSearchText.Contains(liveTypingText)) liveConditionSatisfied = true; break;
                    case FilterCategory.BirthPlace: if (item.birthPlaceSearchText.Contains(liveTypingText)) liveConditionSatisfied = true; break;
                    case FilterCategory.DeathPlace: if (item.deathPlaceSearchText.Contains(liveTypingText)) liveConditionSatisfied = true; break;
                    case FilterCategory.BirthDate: if (item.birthDateSearchText.Contains(liveTypingText)) liveConditionSatisfied = true; break;
                    case FilterCategory.DeathDate: if (item.deathDateSearchText.Contains(liveTypingText)) liveConditionSatisfied = true; break;
                    case FilterCategory.InscriptionsNotes: if (item.inscriptionsSearchText.Contains(liveTypingText)) liveConditionSatisfied = true; break;
                    case FilterCategory.Symbols: if (item.symbolsSearchText.Contains(liveTypingText)) liveConditionSatisfied = true; break;
                }
                if (!liveConditionSatisfied) isRowMatch = false;
            }

            if (isRowMatch)
            {
                matchingCount++;

                if (activeLayoutIndex < currentMaxDisplayedResults)
                {
                    if (activeLayoutIndex >= buttonPool.Count)
                    {
                        GameObject newBtn = null;
                        if (searchResultButtonPrefab != null)
                        {
                            newBtn = Instantiate(searchResultButtonPrefab, searchResultsContainer);
                        }
                        else
                        {
                            newBtn = UnityEngine.UI.DefaultControls.CreateButton(new UnityEngine.UI.DefaultControls.Resources());
                            if (searchResultsContainer != null) newBtn.transform.SetParent(searchResultsContainer, false);
                        }
                        if (newBtn != null) buttonPool.Add(newBtn);
                    }

                    GameObject btnObj = buttonPool[activeLayoutIndex];
                    if (btnObj != null)
                    {
                        btnObj.SetActive(true);
                        if (searchResultsContainer != null) btnObj.transform.SetParent(searchResultsContainer, false);
                        btnObj.transform.localScale = Vector3.one;
                    }

                    RectTransform btnRect = btnObj.GetComponent<RectTransform>();
                    if (btnRect != null)
                    {
                        btnRect.sizeDelta = new Vector2(0f, 65f);
                    }

                    string contextLabel = "";
                    string bornWord = isGerman ? "Geboren" : "Born";
                    string diedWord = isGerman ? "Gestorben" : "Died";
                    string numWord = isGerman ? "Nr." : "N°";

                    foreach (var filter in activeFilters)
                    {
                        if (filter.Key == FilterCategory.BirthPlace) contextLabel += $" <color=#00ffffff>({bornWord}: {item.placeOfBirth})</color>";
                        else if (filter.Key == FilterCategory.DeathPlace) contextLabel += $" <color=#ff4d4d>({diedWord}: {item.placeOfDeath})</color>";
                        else if (filter.Key == FilterCategory.BirthDate) contextLabel += $" <color=#00ffffff>({bornWord}: {FormatNormalizedDate(item.birthDateRaw)})</color>";
                        else if (filter.Key == FilterCategory.DeathDate) contextLabel += $" <color=#ff4d4d>({diedWord}: {FormatNormalizedDate(item.deathDateRaw)})</color>";
                        else if (filter.Key == FilterCategory.InmateNumbers) contextLabel += $" <color=orange>({numWord}: {item.inmateNumber})</color>";
                    }

                    if (hasLiveTyping)
                    {
                        if (liveTypingCategory == FilterCategory.BirthPlace && !contextLabel.Contains(bornWord)) contextLabel += $" <color=#00ffffff>({bornWord}: {item.placeOfBirth})</color>";
                        else if (liveTypingCategory == FilterCategory.DeathPlace && !contextLabel.Contains(diedWord)) contextLabel += $" <color=#ff4d4d>({diedWord}: {item.placeOfDeath})</color>";
                        else if (liveTypingCategory == FilterCategory.BirthDate && !contextLabel.Contains(bornWord)) contextLabel += $" <color=#00ffffff>({bornWord}: {FormatNormalizedDate(item.birthDateRaw)})</color>";
                        else if (liveTypingCategory == FilterCategory.DeathDate && !contextLabel.Contains(diedWord)) contextLabel += $" <color=#ff4d4d>({diedWord}: {FormatNormalizedDate(item.deathDateRaw)})</color>";
                        else if (liveTypingCategory == FilterCategory.InmateNumbers && !contextLabel.Contains(numWord)) contextLabel += $" <color=orange>({numWord}: {item.inmateNumber})</color>";
                    }

                    string matchSummary = hasLiveTyping
                        ? BuildSearchMatchSummary(item, liveTypingCategory, liveTypingText, isGerman)
                        : string.Empty;

                    bool alreadyInRoute = false;
                    if (cachedRouteManager != null)
                    {
                        alreadyInRoute = cachedRouteManager.GetSelectedStoneIDs().Contains(item.stoneID);
                    }

                    // Precise text component lookup to exclude Action button texts (which cause label invisibility)
                    TextMeshProUGUI btnText = null;
                    var allTexts = btnObj.GetComponentsInChildren<TextMeshProUGUI>(true);
                    foreach (var txt in allTexts)
                    {
                        if (txt.gameObject.name.Contains("ActionButton") || 
                            txt.gameObject.name.Contains("Route") || 
                            txt.transform.parent.name.Contains("Action") ||
                            txt.transform.parent.name.Contains("Route")) continue;
                        
                        btnText = txt;
                        break;
                    }
                    if (btnText == null && allTexts.Length > 0) btnText = allTexts[0];

                    if (btnText != null)
                    {
                        btnText.text = $"<b>{item.correctSurname}</b> {item.correctForename}{contextLabel}{matchSummary}";
                        btnText.color = Color.white;
                        btnText.gameObject.SetActive(true);
                    }

                    Image btnImg = btnObj.GetComponent<Image>();
                    if (btnImg != null)
                    {
                        btnImg.color = new Color(0.18f, 0.22f, 0.28f, 0.95f);
                        btnImg.raycastTarget = true;
                    }

                    // 1. CLICK SULLA RIGA PRINCIPALE (Dettagli)
                    Button mainRowButton = btnObj.GetComponent<Button>();
                    if (mainRowButton != null)
                    {
                        mainRowButton.onClick.RemoveAllListeners();
                        mainRowButton.onClick.AddListener(() => {
                            LoadMemorialData(item.stoneID, item.personIndex);
                            if (databaseSearchPanel != null) databaseSearchPanel.SetActive(false);
                        });
                    }

                    // 2. DYNAMIC ROUTING INTERFACE INTEGRATION LAYER
                    Transform actionBtnTransform = btnObj.transform.Find("RouteActionButton");
                    if (actionBtnTransform != null)
                    {
                        Button actionBtn = actionBtnTransform.GetComponent<Button>();
                        TextMeshProUGUI actionText = actionBtnTransform.GetComponentInChildren<TextMeshProUGUI>();
                        Image actionImage = actionBtnTransform.GetComponent<Image>();

                        string addedTextStr = isGerman ? "bereits zur Route hinzugefügt" : "added already to route";
                        string addTextStr = isGerman ? "Zur aktuellen Route hinzufügen" : "Add to current Route";

                        if (cachedRouteManager != null)
                        {
                            actionBtnTransform.gameObject.SetActive(true);

                            if (alreadyInRoute)
                            {
                                if (actionText != null) actionText.text = addedTextStr;
                                if (actionImage != null && addedIconSprite != null) actionImage.sprite = addedIconSprite;
                            }
                            else
                            {
                                if (actionText != null) actionText.text = addTextStr;
                                if (actionImage != null && addIconSprite != null) actionImage.sprite = addIconSprite;
                            }

                            if (actionBtn != null)
                            {
                                actionBtn.onClick.RemoveAllListeners();
                                actionBtn.onClick.AddListener(() => {
                                    if (!cachedRouteManager.IsInModalitaPercorso())
                                    {
                                        cachedRouteManager.ToggleRoutePlanningMode(true);
                                    }

                                    if (alreadyInRoute)
                                    {
                                        cachedRouteManager.RemoveWaypointFromCurrentRoute(item.stoneID);
                                    }
                                    else
                                    {
                                        cachedRouteManager.GestisciTappa(item.stoneID, null);
                                    }

                                    bool nextStateInRoute = !alreadyInRoute;
                                    if (actionText != null) actionText.text = nextStateInRoute ? addedTextStr : addTextStr;
                                    if (actionImage != null && addedIconSprite != null && addIconSprite != null)
                                        actionImage.sprite = nextStateInRoute ? addedIconSprite : addIconSprite;

                                    ExecuteDynamicFacetedSearch();
                                });
                            }
                        }
                        else
                        {
                            actionBtnTransform.gameObject.SetActive(false);
                        }
                    }

                    activeLayoutIndex++;
                }
            }
        }

        if (searchCounterText != null)
        {
            searchCounterText.text = isGerman 
                ? $"<b>{matchingCount}</b> Datensätze gefunden, die Ihren Kriterien entsprechen."
                : $"Found <b>{matchingCount}</b> records matching your criteria layout.";
        }

        Debug.Log($"[SEARCH-DEBUG] FINAL: matchingCount={matchingCount}, activeLayoutIndex={activeLayoutIndex}, buttonPool.Count={buttonPool.Count}, container.childCount={searchResultsContainer?.childCount ?? -1}");

        if (searchResultsContainer != null)
        {
            var contentRect = searchResultsContainer.GetComponent<RectTransform>();
            if (contentRect != null)
            {
                var viewport = searchResultsContainer.parent as RectTransform;
                if (viewport != null && viewport.rect.width > 0)
                {
                    contentRect.anchorMin = new Vector2(0f, 1f);
                    contentRect.anchorMax = new Vector2(1f, 1f);
                    contentRect.pivot = new Vector2(0.5f, 1f);
                    contentRect.offsetMin = new Vector2(0f, contentRect.offsetMin.y);
                    contentRect.offsetMax = new Vector2(0f, contentRect.offsetMax.y);
                    contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, viewport.rect.width);
                }
            }

            var vlg = searchResultsContainer.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            if (vlg == null)
            {
                vlg = searchResultsContainer.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            }
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.spacing = 8f;
            vlg.padding = new RectOffset(6, 6, 6, 6);

            var csf = searchResultsContainer.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            if (csf == null)
            {
                csf = searchResultsContainer.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            }
            csf.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

            for (int i = 0; i < activeLayoutIndex && i < buttonPool.Count; i++)
            {
                var btnObj = buttonPool[i];
                if (btnObj != null && btnObj.activeSelf)
                {
                    var le = btnObj.GetComponent<UnityEngine.UI.LayoutElement>();
                    if (le == null) le = btnObj.AddComponent<UnityEngine.UI.LayoutElement>();
                    le.minHeight = 60f;
                    le.preferredHeight = 65f;
                    le.flexibleWidth = 1f;

                    var bRect = btnObj.GetComponent<RectTransform>();
                    if (bRect != null)
                    {
                        bRect.anchorMin = new Vector2(0f, 0.5f);
                        bRect.anchorMax = new Vector2(1f, 0.5f);
                        bRect.sizeDelta = new Vector2(0f, 65f);
                    }
                }
            }

            if (contentRect != null)
            {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            }

            // Diagnostic: check first button dimensions
            if (buttonPool.Count > 0 && buttonPool[0].activeSelf)
            {
                var rt = buttonPool[0].GetComponent<RectTransform>();
                if (rt != null)
                {
                    Debug.Log($"[SEARCH-DEBUG] Button[0] anchorMin={rt.anchorMin}, anchorMax={rt.anchorMax}, sizeDelta={rt.sizeDelta}, rect={rt.rect}, pos={rt.anchoredPosition}");
                }
            }
        }
    }

    public void OpenPanel(GameObject panel)
    {
        if (panel == null) return;

        if (mapMiniPopupPanel != null && panel != map2DPanel && panel != mapMiniPopupPanel)
        {
            mapMiniPopupPanel.SetActive(false);
        }

        // ✅ CORRETTO: Cerca il componente Wayfinding in modo sicuro senza usare variabili globali inesistenti
        if (panel == databaseSearchPanel && cachedRouteManager != null && cachedRouteManager.GetSelectedStoneIDs().Count > 0)
        {
            ARWayfindingManager arWayfinding = UnityEngine.Object.FindAnyObjectByType<ARWayfindingManager>(FindObjectsInactive.Include);
            if (arWayfinding != null)
            {
                arWayfinding.StopNavigation();
            }
            Debug.Log("[UI Navigation] Route navigation paused silently to facilitate search inputs.");
        }

        if (panel == databaseSearchPanel && arExplorationHub != null)
        {
            arExplorationHub.SetActive(false);
        }

        if (navigationStack.Count > 0 && navigationStack.Peek() == panel)
        {
            panel.SetActive(true);
            return;
        }

        if (sidebarMenuPanel != null && sidebarMenuPanel.activeSelf && panel != sidebarMenuPanel)
        {
            sidebarMenuPanel.SetActive(false);
            Debug.Log("[UI Event Trace] Sidebar_Menu_Panel CLOSED automatically on opening new panel.");
            // Sidebar is an overlay: selecting a destination from it starts a
            // fresh panel context, so Close returns to exploration rather than a
            // stale panel that happened to be open before the drawer.
            while (navigationStack.Count > 0)
            {
                GameObject stalePanel = navigationStack.Pop();
                if (stalePanel != null) stalePanel.SetActive(false);
            }
        }
        Debug.Log($"[UI Event Trace] Panel OPENED: '{panel.name}'");

        if (navigationStack.Count > 0 && panel != sidebarMenuPanel)
        {
            GameObject currentTopPanel = navigationStack.Peek();
            if (currentTopPanel != null)
            {
                currentTopPanel.SetActive(false);
            }
        }

        panel.SetActive(true);
        navigationStack.Push(panel);
    }

    private void CloseDatabaseAndReset()
    {
        ClearAllFilters();
        if (searchCounterText != null) searchCounterText.text = SEARCH_HINT_MESSAGE;

        // ✅ CORRETTO: Al ritorno, se c'è un tragitto attivo, viene ridisegnato chiamando il metodo sbloccato public
        if (cachedRouteManager != null && cachedRouteManager.GetSelectedStoneIDs().Count > 0)
        {
            cachedRouteManager.TriggerRouteUpdate();
            Debug.Log("[UI Navigation] Database closed. Suspended active route resumed and redrawn.");
        }

        CloseCurrentAndReturn();
    }

    public string FormatNormalizedDate(string rawDate)
    {
        if (string.IsNullOrEmpty(rawDate) || rawDate.ToLower() == "unknown") return "Unknown";

        string cleanDate = rawDate.Replace('.', '/').Trim();
        string[] segments = cleanDate.Split('/');

        if (segments.Length == 3)
        {
            string day = segments[0];
            string month = segments[1];
            string year = segments[2];

            if (day == "00" && month == "00") return year;
            if (day == "00") return $"{month}/{year}";

            return $"{day}/{month}/{year}";
        }

        return rawDate;
    }

    public void OpenMemorialDetailFromMap(string stoneID) => LoadMemorialData(stoneID, 0);

    public void OpenMapMiniPopup(string stoneID, Transform markerTransform)
    {
        currentSelectedMapStoneID = stoneID;
        activeMarkerTransform = markerTransform;

        if (miniPopupIDText != null)
        {
            if (releaseMode)
            {
                var stoneData = dataManager.GetMemorialStoneByID(stoneID);
                if (stoneData != null && stoneData.persons != null && stoneData.persons.Count > 0)
                {
                    miniPopupIDText.text = $"{stoneData.persons[0].forename} {stoneData.persons[0].surname}";
                }
                else
                {
                    var graveData = dataManager.GetMassGraveByID(stoneID);
                    if (graveData != null)
                    {
                        miniPopupIDText.text = "Mass Grave";
                    }
                    else
                    {
                        var otherData = dataManager.GetOtherMemorialByID(stoneID);
                        miniPopupIDText.text = (otherData != null) ? otherData.description : "Memorial";
                    }
                }
            }
            else
            {
                miniPopupIDText.text = stoneID;
            }
        }

        if (mapMiniPopupPanel != null)
        {
            mapMiniPopupPanel.SetActive(true);
            mapMiniPopupPanel.transform.SetAsLastSibling();
            UpdatePopupPositionTracking();
        }
    }

    void LateUpdate()
    {
        if (mapMiniPopupPanel != null && mapMiniPopupPanel.activeSelf && activeMarkerTransform != null)
        {
            UpdatePopupPositionTracking();
        }

        if (guideMeButton != null && cachedRouteManager != null)
        {
            guideMeButton.interactable = (cachedRouteManager.GetSelectedStoneIDs().Count > 0);
        }
    }

    private void UpdatePopupPositionTracking()
    {
        if (mapCamera == null || mapDisplayRect == null || activeMarkerTransform == null) return;

        RectTransform popupRect = mapMiniPopupPanel.GetComponent<RectTransform>();
        if (popupRect != null)
        {
            popupRect.anchorMin = new Vector2(0.5f, 0.5f);
            popupRect.anchorMax = new Vector2(0.5f, 0.5f);

            Vector3 targetWorldPosition = activeMarkerTransform.position;
            Vector3 viewportPos = mapCamera.WorldToViewportPoint(targetWorldPosition);

            float localX = (viewportPos.x - 0.5f) * mapDisplayRect.rect.width;
            float localY = (viewportPos.y - 0.5f) * mapDisplayRect.rect.height;

            popupRect.anchoredPosition = new Vector2(localX, localY);
        }
    }

    

    public void OnRouteButtonClicked(TMP_Text buttonTextComponent)
    {
        RouteManager routeMgr = UnityEngine.Object.FindAnyObjectByType<RouteManager>(FindObjectsInactive.Include);
        if (routeMgr != null)
        {
            TourManager tourMgr = UnityEngine.Object.FindAnyObjectByType<TourManager>(FindObjectsInactive.Include);
            if (tourMgr != null && tourMgr.HasSelectedTour)
            {
                isModifyingSelectedTour = !isModifyingSelectedTour;
                routeMgr.SetRoutePlanningModeWithoutClearing(isModifyingSelectedTour);
                if (buttonTextComponent != null)
                {
                    bool isGerman = string.Equals(selectedLanguage, "german", System.StringComparison.OrdinalIgnoreCase);
                    buttonTextComponent.text = isModifyingSelectedTour
                        ? (isGerman ? "Bearbeitung beenden" : "Stop Modifying")
                        : (isGerman ? "Ändern" : "Modify");
                    buttonTextComponent.color = isModifyingSelectedTour ? Color.yellow : Color.white;
                }
                return;
            }

            bool newMode = !routeMgr.IsInModalitaPercorso();
            routeMgr.ToggleRoutePlanningMode(newMode);

            if (buttonTextComponent != null)
            {
                bool isGerman = string.Equals(selectedLanguage, "german", System.StringComparison.OrdinalIgnoreCase);
                buttonTextComponent.text = newMode
                    ? (isGerman ? "<b>Modus: Planung</b>" : "<b>Mode: Planning</b>")
                    : (isGerman ? "Route erstellen" : "Create Route");
                buttonTextComponent.color = newMode ? Color.yellow : Color.white;
            }
        }
    }

    private void PlayPanelAnimation(bool show)
    {
        if (memorialDetailCanvasGroup == null || memorialDetailRect == null) return;
        if (panelAnimationRoutine != null) StopCoroutine(panelAnimationRoutine);
        panelAnimationRoutine = StartCoroutine(AnimatePanel(show));
    }

    private IEnumerator AnimatePanel(bool show)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, panelFadeDuration);
        Vector2 startPos = show ? panelHiddenPos : panelShownPos;
        Vector2 endPos = show ? panelShownPos : panelHiddenPos;
        float startAlpha = show ? 0f : 1f;
        float endAlpha = show ? 1f : 0f;

        memorialDetailRect.anchoredPosition = startPos;
        memorialDetailCanvasGroup.alpha = startAlpha;
        memorialDetailCanvasGroup.interactable = show;
        memorialDetailCanvasGroup.blocksRaycasts = show;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            memorialDetailRect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            memorialDetailCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
            yield return null;
        }

        memorialDetailRect.anchoredPosition = endPos;
        memorialDetailCanvasGroup.alpha = endAlpha;
        if (!show && memorialDetailPanel != null) memorialDetailPanel.SetActive(false);
    }

    private void ClearDetailTexts()
    {
        if (titleText != null) titleText.text = string.Empty;
        if (descriptionText != null) descriptionText.text = string.Empty;
        if (personsListText != null) personsListText.text = string.Empty;
    }

    private void HandleNavigateHereClicked()
    {
        if (string.IsNullOrEmpty(currentMemorialID)) return;

        CloseCurrentAndReturn();
        OpenPanel(map2DPanel);

        Map2DController map2D = UnityEngine.Object.FindAnyObjectByType<Map2DController>(FindObjectsInactive.Include);
        if (map2D != null)
        {
            map2D.FocusAndZoomOnStone(currentMemorialID);
        }
    }

    private void UpdateGuideMeButtonInteractivity()
    {
        if (guideMeButton == null) return;

        RouteManager routeMgr = UnityEngine.Object.FindAnyObjectByType<RouteManager>(FindObjectsInactive.Include);
        TourManager tourMgr = UnityEngine.Object.FindAnyObjectByType<TourManager>(FindObjectsInactive.Include);

        bool hasStones = (routeMgr != null && routeMgr.GetSelectedStoneIDs().Count > 0);
        bool hasActiveTour = (tourMgr != null && tourMgr.IsTourActiveAndRunning);

        guideMeButton.interactable = hasStones || hasActiveTour;
    }

    public void OnGuideMeButtonClicked()
    {
        // The navigation objects live under this controller in the authored scene.
        // It may have been saved inactive, so enable it at the actual user action.
        GameObject arController = FindSceneObjectIncludingInactive("AR_Core_Controller");
        if (arController != null && !arController.activeSelf) arController.SetActive(true);

        RouteManager routeMgr = UnityEngine.Object.FindAnyObjectByType<RouteManager>(FindObjectsInactive.Include);
        if (routeMgr == null || routeMgr.GetSelectedStoneIDs().Count == 0)
        {
            Debug.LogWarning("[Guide Me] Cannot initiate routing: Waypoint queue is empty.");
            return;
        }

        TourManager tourMgr = UnityEngine.Object.FindAnyObjectByType<TourManager>(FindObjectsInactive.Include);
        if (tourMgr != null)
        {
            tourMgr.StartTourFromGuideMeButton();
        }

        string upcomingTargetID = tourMgr != null && tourMgr.IsTourActiveAndRunning
            ? tourMgr.GetCurrentTargetStoneID()
            : routeMgr.GetSelectedStoneIDs()[0];
        if (string.IsNullOrEmpty(upcomingTargetID)) return;

        ARWayfindingManager arWayfinding = UnityEngine.Object.FindAnyObjectByType<ARWayfindingManager>(FindObjectsInactive.Include);
        if (arWayfinding != null)
        {
            arWayfinding.NavigateTo(upcomingTargetID);
        }

        if (tourMgr == null || !tourMgr.IsTourActiveAndRunning)
        {
            GuidanceSystemBase guidance = ThesisManager.Instance != null ? ThesisManager.Instance.CurrentGuidanceSystem : null;
            if (guidance != null) guidance.OnMemorialSelected(upcomingTargetID);
        }

        ReturnToExplorationHub();
    }

    private bool HasInspectableModel()
    {
        MemorialSpawner spawner = UnityEngine.Object.FindAnyObjectByType<MemorialSpawner>(FindObjectsInactive.Include);
        GameObject memorialObject = spawner != null ? spawner.GetSpawnedMemorial(currentMemorialID) : null;
        StoneModelSpawner modelSpawner = memorialObject != null ? memorialObject.GetComponent<StoneModelSpawner>() : null;
        return modelSpawner == null || !modelSpawner.IsModelLoaded || modelSpawner.HasHighFidelityModel;
    }

    private void Refresh3DModelInspectorAvailability()
    {
        if (view3DModelButton != null) view3DModelButton.interactable = HasInspectableModel();
    }

    private void ReturnToExplorationHub()
    {
        if (sidebarMenuPanel != null) sidebarMenuPanel.SetActive(false);
        if (mapMiniPopupPanel != null) mapMiniPopupPanel.SetActive(false);

        while (navigationStack.Count > 0)
        {
            GameObject panel = navigationStack.Pop();
            if (panel != null) panel.SetActive(false);
        }

        SetGuideSubtitlesSuspended(false);
        if (arExplorationHub != null) arExplorationHub.SetActive(true);
    }

    [System.Serializable]
    public class SearchCacheItem
    {
        public string stoneID;
        public int personIndex;
        public string correctForename;
        public string correctSurname;
        public string birthDateRaw;
        public string deathDateRaw;
        public string inmateNumber;
        public string placeOfBirth;
        public string placeOfDeath;
        public string symbolsRaw;

        public string namesSearchText;
        public string inmateSearchText;
        public string birthPlaceSearchText;
        public string deathPlaceSearchText;
        public string birthDateSearchText;
        public string deathDateSearchText;
        public string symbolsSearchText;
        public string inscriptionsSearchText;
        public string inscriptionDisplayText;
        public string globalSearchText;
    }

    private static string BuildSearchMatchSummary(SearchCacheItem item, FilterCategory category, string query, bool isGerman)
    {
        string label = string.Empty;
        string source = string.Empty;
        bool Matches(string value) => !string.IsNullOrEmpty(value) && value.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0;

        if (category == FilterCategory.FirstName && Matches(item.correctForename)) label = isGerman ? "Vorname" : "first name";
        else if (category == FilterCategory.Surname && Matches(item.correctSurname)) label = isGerman ? "Nachname" : "surname";
        else if (category == FilterCategory.InmateNumbers && Matches(item.inmateNumber)) label = isGerman ? "Häftlingsnummer" : "inmate number";
        else if (category == FilterCategory.BirthPlace && Matches(item.placeOfBirth)) label = isGerman ? "Geburtsort" : "birth place";
        else if (category == FilterCategory.DeathPlace && Matches(item.placeOfDeath)) label = isGerman ? "Sterbeort" : "place of death";
        else if (category == FilterCategory.InscriptionsNotes && Matches(item.inscriptionDisplayText))
        {
            label = isGerman ? "Inschrift/Notiz" : "inscription/note";
            source = item.inscriptionDisplayText;
        }
        else if (category == FilterCategory.Symbols && Matches(item.symbolsRaw)) label = isGerman ? "Symbol" : "symbol";
        else if (category == FilterCategory.AllFields)
        {
            if (Matches(item.namesSearchText)) label = isGerman ? "Name" : "name";
            else if (Matches(item.inmateNumber)) label = isGerman ? "Häftlingsnummer" : "inmate number";
            else if (Matches(item.placeOfBirth)) label = isGerman ? "Geburtsort" : "birth place";
            else if (Matches(item.placeOfDeath)) label = isGerman ? "Sterbeort" : "place of death";
            else if (Matches(item.symbolsRaw)) label = isGerman ? "Symbol" : "symbol";
            else if (Matches(item.inscriptionDisplayText))
            {
                label = isGerman ? "Inschrift/Notiz" : "inscription/note";
                source = item.inscriptionDisplayText;
            }
        }

        if (string.IsNullOrEmpty(label)) return string.Empty;

        return $" <color=#AAB8C8>· {label}</color>";
    }

    

    private void CloseMapAndReset()
    {
        Map2DController mapController = UnityEngine.Object.FindAnyObjectByType<Map2DController>(FindObjectsInactive.Include);
        if (mapController != null)
        {
            mapController.ForceClosePopup();
        }

        CloseCurrentAndReturn();
    }

    /// <summary>
    /// UI Hook connected to the 'Optimize' button. Invokes the greedy Nearest Neighbor 
    /// sorting algorithm inside the RouteManager to re-order waypoints based on proximity.
    /// </summary>
    public void OptimizeActiveRoutePoints()
    {
        if (cachedRouteManager != null)
        {
            cachedRouteManager.OptimizeCurrentRouteDistances();
            // Aggiorna anche la lista visiva della ricerca se aperta per riflettere il nuovo ordine matematico
            if (databaseSearchPanel != null && databaseSearchPanel.activeSelf)
            {
                ExecuteDynamicFacetedSearch();
            }
        }
    }

    public void SetOnboardingThesisMode(int index, bool startGuidanceAfterChange = false)
    {
        ThesisManager.GuidanceMode mode = ThesisManager.GuidanceMode.Personal;
        if (index == 1) mode = ThesisManager.GuidanceMode.Intermediate;
        else if (index == 2) mode = ThesisManager.GuidanceMode.Impersonal;

        DespawnAllGuidanceAvatars();

        if (thesisManager != null)
        {
            thesisManager.SetGuidanceMode(mode, triggerAvatarAndAudio: startGuidanceAfterChange && thesisManager.IsExperienceStarted);
            PlayerPrefs.SetInt("Thesis_GuidanceMode", index);
            PlayerPrefs.Save();
        }

        UpdateOnboardingButtonsVisuals(mode);
    }

    public void UpdateOnboardingButtonsVisuals(ThesisManager.GuidanceMode mode)
    {
        bool isGerman = string.Equals(selectedLanguage, "german", System.StringComparison.OrdinalIgnoreCase);

        if (btnModePersonal != null)
        {
            var txt = btnModePersonal.GetComponentInChildren<TextMeshProUGUI>(true);
            if (txt != null) txt.text = isGerman ? "Persönlich" : "Personal";
        }

        if (btnModeIntermediate != null)
        {
            var txt = btnModeIntermediate.GetComponentInChildren<TextMeshProUGUI>(true);
            if (txt != null) txt.text = isGerman ? "Intermediär" : "Intermediate";
        }

        if (btnModeImpersonal != null)
        {
            var txt = btnModeImpersonal.GetComponentInChildren<TextMeshProUGUI>(true);
            if (txt != null) txt.text = isGerman ? "Unpersönlich" : "Impersonal";
        }

        // Custom premium colors for highlighting: selected = blue/cyan, unselected = darker gray/transparent overlay style
        Color selectedColor = new Color(0.18f, 0.48f, 0.93f, 1f); // Blue
        Color unselectedColor = new Color(0.2f, 0.2f, 0.2f, 0.75f); // Semi-transparent dark gray

        Color selectedTextColor = Color.white;
        Color unselectedTextColor = new Color(0.7f, 0.7f, 0.7f, 1f); // Muted gray

        UpdateButtonColors(btnModePersonal, mode == ThesisManager.GuidanceMode.Personal, selectedColor, unselectedColor, selectedTextColor, unselectedTextColor);
        UpdateButtonColors(btnModeIntermediate, mode == ThesisManager.GuidanceMode.Intermediate, selectedColor, unselectedColor, selectedTextColor, unselectedTextColor);
        UpdateButtonColors(btnModeImpersonal, mode == ThesisManager.GuidanceMode.Impersonal, selectedColor, unselectedColor, selectedTextColor, unselectedTextColor);
    }

    public void ResolveLanguageButtons()
    {
        if (onboardingPanel == null)
        {
            var t = transform.Find("Onboarding_Panel");
            if (t != null) onboardingPanel = t.gameObject;
            else onboardingPanel = FindGameObjectIncludingInactive("Onboarding_Panel") ?? FindGameObjectIncludingInactive("OnboardingPanel");
        }

        if (onboardingButtonEnglish == null || onboardingButtonGerman == null)
        {
            if (onboardingPanel != null)
            {
                foreach (var b in onboardingPanel.GetComponentsInChildren<Button>(true))
                {
                    string n = b.name.ToLower();
                    var tmp = b.GetComponentInChildren<TextMeshProUGUI>(true);
                    string t = tmp != null ? tmp.text.ToLower().Trim() : "";

                    if (onboardingButtonEnglish == null && (n.Contains("english") || n.Contains("btn_en") || t.Equals("english")))
                    {
                        onboardingButtonEnglish = b;
                    }
                    else if (onboardingButtonGerman == null && (n.Contains("deutsch") || n.Contains("german") || n.Contains("btn_de") || t.Equals("deutsch")))
                    {
                        onboardingButtonGerman = b;
                    }
                }
            }
        }

        if (onboardingButtonEnglish != null)
        {
            onboardingButtonEnglish.onClick.RemoveAllListeners();
            onboardingButtonEnglish.onClick.AddListener(() => {
                Debug.Log("[UIManager] Onboarding English Button CLICKED!");
                ChangeLanguage("english");
            });
            Debug.Log($"[UIManager] Connected Onboarding English Button: {onboardingButtonEnglish.name}");
        }

        if (onboardingButtonGerman != null)
        {
            onboardingButtonGerman.onClick.RemoveAllListeners();
            onboardingButtonGerman.onClick.AddListener(() => {
                Debug.Log("[UIManager] Onboarding German Button CLICKED!");
                ChangeLanguage("german");
            });
            Debug.Log($"[UIManager] Connected Onboarding German Button: {onboardingButtonGerman.name}");
        }

        if (buttonEnglish != null)
        {
            buttonEnglish.onClick.RemoveAllListeners();
            buttonEnglish.onClick.AddListener(() => SetPopupLanguage("english"));
        }

        if (buttonGerman != null)
        {
            buttonGerman.onClick.RemoveAllListeners();
            buttonGerman.onClick.AddListener(() => SetPopupLanguage("german"));
        }

        if (buttonHebrew != null)
        {
            buttonHebrew.onClick.RemoveAllListeners();
            buttonHebrew.onClick.AddListener(() => SetPopupLanguage("hebrew"));
        }

        UpdateLanguageButtonsVisuals();
    }

    private string popupSelectedLanguage = "english";

    public void SetPopupLanguage(string lang)
    {
        popupSelectedLanguage = lang;
        if (currentMemorial != null)
        {
            UpdateDetailDisplay();
        }
        UpdatePopupLanguageButtonsVisuals();
    }

    public void SyncDiagnosticUIState()
    {
        bool isDiagOn = releaseMode ? false : (PlayerPrefs.GetInt("Thesis_DiagnosticMode", 0) == 1);

        if (locationSetupDropdown != null) locationSetupDropdown.gameObject.SetActive(isDiagOn);
        if (testLeftButton != null) testLeftButton.gameObject.SetActive(isDiagOn);
        if (testRightButton != null) testRightButton.gameObject.SetActive(isDiagOn);

        // Hide/Show top-left location and testing zone dropdowns
        var testZoneObj = GameObject.Find("TestingZoneDropdown") ?? GameObject.Find("Dropdown_TestingZone") ?? GameObject.Find("Dropdown_Location");
        if (testZoneObj != null) testZoneObj.SetActive(isDiagOn);

        var originSelector = FindGameObjectIncludingInactive("Origin_Selector_Panel") ?? GameObject.Find("Origin_Selector_Panel");
        if (originSelector != null) originSelector.SetActive(isDiagOn);

        var dropZones = GameObject.Find("Dropdown_zones") ?? FindGameObjectIncludingInactive("Dropdown_zones");
        if (dropZones != null) dropZones.SetActive(isDiagOn);

        var dropCheckpoints = GameObject.Find("Dropdown_Checkpoints") ?? FindGameObjectIncludingInactive("Dropdown_Checkpoints");
        if (dropCheckpoints != null) dropCheckpoints.SetActive(isDiagOn);

        var fieldTestMgr = UnityEngine.Object.FindAnyObjectByType<FieldTestManager>(FindObjectsInactive.Include);
        if (fieldTestMgr != null)
        {
            var fieldTestDropdown = fieldTestMgr.GetComponentInChildren<TMP_Dropdown>(true);
            if (fieldTestDropdown != null) fieldTestDropdown.gameObject.SetActive(isDiagOn);
            fieldTestMgr.gameObject.SetActive(isDiagOn);
        }

        var diagPanel = FindGameObjectIncludingInactive("Diagnostic_Panel") ?? FindGameObjectIncludingInactive("DiagnosticPanel");
        if (diagPanel != null) diagPanel.SetActive(isDiagOn);

        var arDiag = UnityEngine.Object.FindAnyObjectByType<ArDiagnostic>(FindObjectsInactive.Include);
        if (arDiag != null) arDiag.gameObject.SetActive(isDiagOn);

        // Hide/Show SoccerFieldZone magenta debug rectangles on 2D map
        var zones = UnityEngine.Object.FindObjectsByType<SoccerFieldZone>(FindObjectsInactive.Include);
        foreach (var z in zones)
        {
            if (z != null) z.gameObject.SetActive(isDiagOn);
        }
    }

    public static void DespawnAllGuidanceAvatarsExcept(GuidanceSystemBase activeSystem)
    {
        var personal = UnityEngine.Object.FindAnyObjectByType<PersonalGuidance>(FindObjectsInactive.Include);
        if (personal != null && personal != activeSystem) personal.DespawnAvatar();

        var intermediate = UnityEngine.Object.FindAnyObjectByType<IntermediateGuidance>(FindObjectsInactive.Include);
        if (intermediate != null && intermediate != activeSystem) intermediate.DespawnAvatar();

        var allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        foreach (var go in allObjects)
        {
            if (go == null) continue;
            string n = go.name;
            bool isPersonalGo = n.Contains("GuideCharacter");
            bool isIntermediateGo = n.Contains("GuideAvatar") || n.Contains("HologramAvatar");

            if (isPersonalGo || isIntermediateGo)
            {
                go.SetActive(false);
                // Destroy only duplicate temporary clone instances, keep persistent SingleGuideAvatarInstance alive
                if (go.name.Contains("(Clone)") && !go.name.Equals("SingleGuideAvatarInstance", System.StringComparison.OrdinalIgnoreCase))
                {
                    UnityEngine.Object.Destroy(go);
                }
            }
        }
    }

    public static void DespawnAllGuidanceAvatars()
    {
        DespawnAllGuidanceAvatarsExcept(null);
    }

    public void UpdatePopupLanguageButtonsVisuals()
    {
        bool isEN = string.Equals(popupSelectedLanguage, "english", System.StringComparison.OrdinalIgnoreCase);
        bool isDE = string.Equals(popupSelectedLanguage, "german", System.StringComparison.OrdinalIgnoreCase);
        bool isHE = string.Equals(popupSelectedLanguage, "hebrew", System.StringComparison.OrdinalIgnoreCase);

        SetTabVisual(buttonEnglish, isEN);
        SetTabVisual(buttonGerman, isDE);
        SetTabVisual(buttonHebrew, isHE);
    }

    private void SetTabVisual(Button btn, bool isSelected)
    {
        if (btn == null) return;

        var cg = btn.GetComponent<CanvasGroup>();
        if (cg == null) cg = btn.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = isSelected ? 1.0f : 0.6f;

        var text = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.color = isSelected ? Color.white : new Color(0.75f, 0.75f, 0.75f, 1f);
        }
    }

    public void UpdateLanguageButtonsVisuals()
    {
        bool isGerman = string.Equals(selectedLanguage, "german", System.StringComparison.OrdinalIgnoreCase);

        Color selectedColor = new Color(0.18f, 0.48f, 0.93f, 1f); // Blue
        Color unselectedColor = new Color(0.2f, 0.2f, 0.2f, 0.75f); // Semi-transparent dark gray

        Color selectedTextColor = Color.white;
        Color unselectedTextColor = new Color(0.7f, 0.7f, 0.7f, 1f); // Muted gray

        UpdateButtonColors(onboardingButtonEnglish, !isGerman, selectedColor, unselectedColor, selectedTextColor, unselectedTextColor);
        UpdateButtonColors(onboardingButtonGerman, isGerman, selectedColor, unselectedColor, selectedTextColor, unselectedTextColor);
    }

    private void UpdateButtonColors(Button btn, bool isSelected, Color selBg, Color unselBg, Color selText, Color unselText)
    {
        if (btn == null) return;

        var hoverFeedback = btn.GetComponent<UIHoverFeedback>();
        if (hoverFeedback != null) hoverFeedback.enabled = false;

        btn.transition = Selectable.Transition.ColorTint;

        var colors = btn.colors;
        Color targetBg = isSelected ? selBg : unselBg;

        colors.normalColor = targetBg;
        colors.selectedColor = targetBg;
        colors.highlightedColor = targetBg * 1.15f;
        colors.pressedColor = targetBg * 0.85f;
        btn.colors = colors;

        Graphic mainGraphic = btn.targetGraphic ?? btn.GetComponent<Graphic>() ?? btn.GetComponentInChildren<Graphic>();
        if (mainGraphic != null)
        {
            mainGraphic.raycastTarget = true;
            btn.targetGraphic = mainGraphic;
        }

        var images = btn.GetComponentsInChildren<UnityEngine.UI.Image>(true);
        foreach (var img in images)
        {
            img.color = targetBg;
            img.raycastTarget = true;
        }

        var texts = btn.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var text in texts)
        {
            text.color = isSelected ? selText : unselText;
        }
    }

    public void DisplayGuideSubtitle(string text)
    {
        if (string.Equals(text, "New Text", System.StringComparison.OrdinalIgnoreCase))
        {
            text = string.Empty;
        }

        pendingGuideSubtitle = text;

        if (IsSubtitleBlockingPanelActive())
            return;

        Canvas canvas = uiCanvas ?? FindComponentInScene<Canvas>("Canvas");
        if (canvas != null)
        {
            Transform subPanel = canvas.transform.Find("AR_Exploration_Hub/Subtitle_Panel") ??
                                 canvas.transform.Find("Subtitle_Panel") ??
                                 FindDescendantByName(canvas.transform, "Subtitle_Panel");
            if (subPanel != null)
            {
                var allTMPs = subPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var tmp in allTMPs)
                {
                    tmp.text = text;
                }

                bool hasContent = !string.IsNullOrEmpty(text) && !string.IsNullOrWhiteSpace(text);
                subPanel.gameObject.SetActive(hasContent);

                if (hasContent)
                {
                    Transform curr = subPanel.parent;
                    while (curr != null && curr.GetComponent<Canvas>() == null)
                    {
                        if (!curr.gameObject.activeSelf) curr.gameObject.SetActive(true);
                        curr = curr.parent;
                    }

                    // Bring panel to front of container
                    subPanel.transform.SetAsLastSibling();

                    // Resolve guideSubtitleText if possible for legacy logic
                    if (guideSubtitleText == null)
                    {
                        guideSubtitleText = subPanel.GetComponent<TextMeshProUGUI>() ?? subPanel.GetComponentInChildren<TextMeshProUGUI>(true);
                    }
                    
                    if (guideSubtitleText != null)
                    {
                        var textRt = guideSubtitleText.rectTransform;
                        var parentRt = subPanel.GetComponent<RectTransform>();
                        
                        if (parentRt != null && textRt != null)
                        {
                            // Ensure textRt is anchored to stretch horizontally inside parentRt with 16px side padding
                            textRt.anchorMin = new Vector2(0f, 0f);
                            textRt.anchorMax = new Vector2(1f, 1f);
                            textRt.offsetMin = new Vector2(16f, 12f);
                            textRt.offsetMax = new Vector2(-16f, -12f);

                            guideSubtitleText.fontSize = currentSubtitleFontSize;
                            guideSubtitleText.ForceMeshUpdate();
                            float preferredH = guideSubtitleText.preferredHeight;

                            float targetHeight = Mathf.Clamp(preferredH + 32f, 60f, 450f);
                            parentRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);

                            var le = subPanel.GetComponent<UnityEngine.UI.LayoutElement>();
                            if (le != null)
                            {
                                le.minHeight = targetHeight;
                                le.preferredHeight = targetHeight;
                            }

                            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(textRt);
                            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(parentRt);
                        }
                    }
                }
            }
        }
        else
        {
            Debug.LogError($"[UIManager] DisplayGuideSubtitle FAILED: guideSubtitleText is NULL! Text was: '{(text != null && text.Length > 50 ? text.Substring(0, 50) + "..." : text)}'");
        }
    }

    private void SetGuideSubtitlesSuspended(bool suspended)
    {
        if (guideSubtitlesSuspended == suspended)
        {
            if (!suspended) DisplayGuideSubtitle(pendingGuideSubtitle);
            return;
        }

        guideSubtitlesSuspended = suspended;

        if (suspended)
        {
            if (guideSubtitleText != null && guideSubtitleText.transform.parent != null)
            {
                guideSubtitleText.transform.parent.gameObject.SetActive(false);
            }
            return;
        }

        DisplayGuideSubtitle(pendingGuideSubtitle);
    }

    public bool IsAnyModalPanelActive()
    {
        bool is3DInspectorActive = false;
        if (cachedModelInspectorUI == null)
        {
            cachedModelInspectorUI = UnityEngine.Object.FindAnyObjectByType<ModelInspectorUI>(FindObjectsInactive.Include);
        }
        ModelInspectorUI inspector = cachedModelInspectorUI;
        if (inspector != null && inspector.gameObject != null) is3DInspectorActive = inspector.gameObject.activeSelf;

        bool isSiteHistoryActive = (siteHistoryPanel != null && siteHistoryPanel.activeSelf);
        if (!isSiteHistoryActive)
        {
            if (cachedSiteHistoryDropdown == null)
            {
                cachedSiteHistoryDropdown = UnityEngine.Object.FindAnyObjectByType<SiteHistoryDropdownController>(FindObjectsInactive.Include);
            }
            var dropdownCtrl = cachedSiteHistoryDropdown;
            if (dropdownCtrl != null)
            {
                isSiteHistoryActive = dropdownCtrl.IsDropdownOpen();
            }
        }

        Transform tutorialOverlay = uiCanvas != null ? uiCanvas.transform.Find("TutorialOverlay") : null;
        bool isTutorialActive = tutorialOverlay != null && tutorialOverlay.gameObject.activeSelf;

        return (onboardingPanel != null && onboardingPanel.activeSelf) ||
               (databaseSearchPanel != null && databaseSearchPanel.activeSelf) ||
               (map2DPanel != null && map2DPanel.activeSelf) ||
               (memorialDetailPanel != null && memorialDetailPanel.activeSelf) ||
               (sidebarMenuPanel != null && sidebarMenuPanel.activeSelf) ||
               isTutorialActive ||
               isSiteHistoryActive ||
               is3DInspectorActive;
    }

    private bool IsSubtitleBlockingPanelActive()
    {
        bool sidebarWasOpen = sidebarMenuPanel != null && sidebarMenuPanel.activeSelf;
        if (sidebarWasOpen) sidebarMenuPanel.SetActive(false);
        bool blocking = IsAnyModalPanelActive();
        if (sidebarWasOpen) sidebarMenuPanel.SetActive(true);
        return blocking;
    }

    public void RefreshSubtitleVisibility()
    {
        if (guideSubtitleText != null && guideSubtitleText.transform.parent != null)
        {
            var parentPanel = guideSubtitleText.transform.parent.gameObject;
            if (parentPanel != null)
            {
                string text = guideSubtitleText.text;
                // Guide speech must remain readable even while the arrival detail
                // popup is open; the subtitle panel is explicitly brought to front.
                bool shouldBeActive = !IsSubtitleBlockingPanelActive() &&
                                      !string.IsNullOrEmpty(text) &&
                                      !string.IsNullOrWhiteSpace(text);
                if (parentPanel.activeSelf != shouldBeActive)
                {
                    parentPanel.SetActive(shouldBeActive);
                    var parentRt = parentPanel.GetComponent<RectTransform>();
                    if (parentRt != null && parentPanel.activeSelf)
                    {
                        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(parentRt);
                    }
                }
            }
        }
    }

    void Update()
    {
        RefreshSubtitleVisibility();

        if (!string.IsNullOrEmpty(pendingToastTitle) && !IsAnyModalPanelActive())
        {
            string title = pendingToastTitle;
            string message = pendingToastMessage;
            float duration = pendingToastDuration;
            pendingToastTitle = null;
            pendingToastMessage = null;
            DisplayNotificationToast(title, message, duration);
        }
    }

    public void RefreshRouteButtonLabel(bool isEditingTour)
    {
        Transform button = uiCanvas != null ? uiCanvas.transform.Find("Map_2D_Panel/Btn_Create_Route") : null;
        TMP_Text label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        if (label == null) return;

        isModifyingSelectedTour = false;
        label.text = isEditingTour ? "Modify" : "Create Route";
        label.color = isEditingTour ? Color.yellow : Color.white;
    }

    public void RefreshSearchResultsForRouteChange()
    {
        ExecuteDynamicFacetedSearch();
    }

    private void PlaySymbolFallback(string symbolKey)
    {
        string langSuffix = selectedLanguage == "german" ? "DE" : "EN";
        string assetPath = $"DialogueAssets/Generated/Symbol_{symbolKey}_{langSuffix}";
        DialogueSequence seq = Resources.Load<DialogueSequence>(assetPath);
        if (seq != null && seq.dialogueLines != null && seq.dialogueLines.Count > 0)
        {
            var line = seq.dialogueLines[0];
            if (line.voiceClip != null && audioController != null)
            {
                audioController.PlayClipDirectly(line.voiceClip);
            }
            if (!string.IsNullOrEmpty(line.subtitleText))
            {
                DisplayGuideSubtitle(line.subtitleText);
            }
        }
    }

    private T FindComponentInScene<T>(string name) where T : Component
    {
        GameObject go = GameObject.Find(name);
        if (go != null)
        {
            var comp = go.GetComponent<T>();
            if (comp != null) return comp;
        }

        foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (g != null && g.name == name && g.scene.name != null)
            {
                var comp = g.GetComponent<T>();
                if (comp != null) return comp;
            }
        }
        return null;
    }

    private static Transform FindDescendantByName(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == name) return child;
        return null;
    }

    private static GameObject FindSceneObjectIncludingInactive(string name)
    {
        foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            if (candidate != null && candidate.scene.IsValid() && candidate.name == name) return candidate;
        return null;
    }

    private GameObject FindGameObjectIncludingInactive(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go != null) return go;
        foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (g != null && g.name == name && g.scene.name != null)
            {
                return g;
            }
        }
        return null;
    }
}





