using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Data-driven coachmark tutorial overlay.
/// Steps 0-8 cover the full first-run user journey:
///   0  Hamburger menu
///   1  Sidebar (Database / Map / Site History branching)
///   2  Search input field
///   3  Search results list
///   4  2D Map → stone pin popup
///   5  Memorial Detail: Language tabs (EN / DE / HE)
///   6  Memorial Detail: Audio + 3D buttons
///   7  Memorial Detail: Person arrows ◄ ► + Guide me
///   8  Site History panel
/// </summary>
public class CoachmarkTutorialController : MonoBehaviour
{
    // RIMOSSO: usa AppLanguage (Core/AppLanguage.cs)

    [Serializable]
    public class CoachmarkTransition
    {
        [Tooltip("The real UI button the user must press for the tutorial to advance.")]
        public Button triggerButton;
        [Tooltip("Index into the 'steps' list to jump to. -1 ends the tutorial.")]
        public int nextStepIndex = -1;
    }

    [Serializable]
    public class CoachmarkStep
    {
        [Header("What to highlight (1 or more shown together)")]
        public RectTransform[] highlightTargets;

        [Header("Copy")]
        [TextArea(2, 4)] public string textEN;
        [TextArea(2, 4)] public string textDE;
        [TextArea(2, 4)] public string textHE;

        [Header("How this step advances")]
        [Tooltip("The tutorial advances automatically the moment the user presses any of these.")]
        public List<CoachmarkTransition> transitions = new List<CoachmarkTransition>();

        [Tooltip("If true, shown as a small non-blocking toast (no dark frame, auto-dismisses) instead of a full coachmark.")]
        public bool isToast = false;
        public float toastDuration = 4f;

        [Tooltip("If true, GoToNextStepManually() can advance this step.")]
        public bool allowManualAdvance = false;
    }

    // -------------------------------------------------------------------------
    // Inspector fields
    // -------------------------------------------------------------------------

    [Header("📋 Tutorial Script")]
    [SerializeField] private List<CoachmarkStep> steps = new List<CoachmarkStep>();

    [Tooltip("If the user already has stones in their route when the tutorial starts, jump straight to this step instead of step 0 (set to -1 to disable).")]
    [SerializeField] private int jumpToStepIndexIfRouteNonEmpty = -1;

    [Header("🖼️ Frame Overlay (4 dark panels forming a hole around the target)")]
    [SerializeField] private RectTransform overlayRoot;
    [SerializeField] private RectTransform frameTop;
    [SerializeField] private RectTransform frameBottom;
    [SerializeField] private RectTransform frameLeft;
    [SerializeField] private RectTransform frameRight;
    [Tooltip("Extra padding in pixels around the highlighted element's real bounds.")]
    [SerializeField] private float cutoutPadding = 12f;

    [Header("💬 Bubble")]
    [SerializeField] private RectTransform bubbleRoot;
    [SerializeField] private TextMeshProUGUI bubbleText;
    [SerializeField] private TextMeshProUGUI stepCounterText;
    [SerializeField] private TextMeshProUGUI arrowGlyph;
    [SerializeField] private Button skipButton;
    [SerializeField] private TextMeshProUGUI skipButtonLabel;

    [Header("💬 Arrow Indicators (GameObjects with directional sprites)")]
    [SerializeField] private GameObject arrowTop;
    [SerializeField] private GameObject arrowBottom;
    [SerializeField] private GameObject arrowLeft;
    [SerializeField] private GameObject arrowRight;

    [Header("🍞 Toast (soft non-blocking completion message)")]
    [SerializeField] private RectTransform toastRoot;
    [SerializeField] private TextMeshProUGUI toastText;

    [Header("🌐 Language")]
    [SerializeField] private AppLanguage currentLanguage = AppLanguage.EN;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    private Coroutine updateFrameRoutine;
    private const string PrefKeyEnabled   = "Tutorial_Enabled";
    private const string PrefKeyCompleted = "Tutorial_Completed";
    private int  currentStepIndex       = -1;
    private bool hasManualAdvancePending = false;
    private readonly List<(Button button, UnityEngine.Events.UnityAction action)> activeListeners = new();

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public int CurrentStepIndex => currentStepIndex;
    public bool IsTutorialCompleted => PlayerPrefs.GetInt(PrefKeyCompleted, 0) == 1;
    public bool IsTutorialEnabled => PlayerPrefs.GetInt(PrefKeyEnabled, 1) == 1 && !IsTutorialCompleted;
    public bool IsTutorialFeatureEnabled => PlayerPrefs.GetInt(PrefKeyEnabled, 1) == 1;

    /// <summary>Call once after the guidance mode is chosen and the relevant panels exist.</summary>
    public void TryStartTutorial(int selectedRouteStoneCount)
    {
        SanitizeAndSyncSteps();

        if (!IsTutorialEnabled)
        {
            HideEverything();
            return;
        }

        int startIndex = 0;
        if (jumpToStepIndexIfRouteNonEmpty >= 0 && selectedRouteStoneCount > 0)
            startIndex = jumpToStepIndexIfRouteNonEmpty;

        GoToStep(startIndex);
    }

    /// <summary>
    /// Jump directly to the Memorial Detail steps (Step 5) when opening a detail panel
    /// from search database or map pin.
    /// </summary>
    public void OnMemorialDetailPanelOpened()
    {
        if (IsTutorialEnabled && (currentStepIndex < 5 || currentStepIndex > 7))
        {
            GoToStep(5);
        }
    }

    /// <summary>Wire this to the Sidebar's "Show guidance tips" toggle.</summary>
    public void SetTutorialEnabled(bool isEnabled)
    {
        if (isEnabled)
        {
            RestartTutorial();
        }
        else
        {
            SetTutorialFeatureEnabled(false);
            EndTutorial(markCompleted: false);
        }
    }

    /// <summary>Enables or disables automatic tutorial presentation without starting it.</summary>
    public void SetTutorialFeatureEnabled(bool isEnabled)
    {
        PlayerPrefs.SetInt(PrefKeyEnabled, isEnabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>Starts the tutorial again from its first, visible navigation step.</summary>
    public void RestartTutorial()
    {
        PlayerPrefs.SetInt(PrefKeyEnabled, 1);
        PlayerPrefs.SetInt(PrefKeyCompleted, 0);
        PlayerPrefs.Save();
        GoToStep(0);
    }

    public void SetLanguage(AppLanguage language)
    {
        currentLanguage = language;
        if (currentStepIndex >= 0 && currentStepIndex < steps.Count)
            RefreshBubbleText(steps[currentStepIndex]);
    }

    private string GetStepText(CoachmarkStep step)
    {
        if (step == null) return string.Empty;
        switch (currentLanguage)
        {
            case AppLanguage.DE:
                return !string.IsNullOrEmpty(step.textDE) ? step.textDE : step.textEN;
            case AppLanguage.HE:
                return !string.IsNullOrEmpty(step.textHE) ? step.textHE : step.textEN;
            default:
                return step.textEN;
        }
    }

    /// <summary>
    /// Displays a temporary one-shot highlight bubble for a single UI element.
    /// Does not mutate the tutorial's step state.
    /// </summary>
    public void ShowOneShotHighlight(RectTransform target, string textEN, string textDE, string textHE = "", Action onDismissed = null)
    {
        int resumeIndex = currentStepIndex;
        ClearActiveListeners();

        var oneShot = new CoachmarkStep
        {
            highlightTargets = new[] { target },
            textEN = textEN,
            textDE = textDE,
            textHE = textHE,
        };

        DisplayFrameAndBubble(oneShot, stepNumberLabel: null);

        if (skipButton != null)
        {
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(() =>
            {
                HideEverything();
                onDismissed?.Invoke();
                if (resumeIndex >= 0) GoToStep(resumeIndex);
            });
        }

        Button targetButton = target != null ? target.GetComponent<Button>() : null;
        if (targetButton != null)
        {
            void Handler()
            {
                targetButton.onClick.RemoveListener(Handler);
                HideEverything();
                onDismissed?.Invoke();
                if (resumeIndex >= 0) GoToStep(resumeIndex);
            }
            targetButton.onClick.AddListener(Handler);
            activeListeners.Add((targetButton, Handler));
        }
    }

    public void SkipTutorial() => EndTutorial(markCompleted: true);

    /// <summary>
    /// Advances the current step from a non-button trigger (e.g. typing into a search field).
    /// Only the first call after a step starts has any effect.
    /// </summary>
    public void GoToNextStepManually()
    {
        if (currentStepIndex < 0 || currentStepIndex >= steps.Count) return;
        if (!hasManualAdvancePending) return;
        hasManualAdvancePending = false;
        GoToStep(currentStepIndex + 1);
    }

    // -------------------------------------------------------------------------
    // Step wiring — resolves real scene references via UIManager field reflection
    // -------------------------------------------------------------------------

    public void SanitizeAndSyncSteps()
    {
        var uiMgr = UnityEngine.Object.FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
        Canvas canvasComp = UnityEngine.Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
        Transform canvasTrans = canvasComp != null ? canvasComp.transform : null;

        // UIManager fields resolved by name via reflection
        Button hamburgerBtn   = GetRef<Button>(uiMgr, "hamburgerButton");
        Button navDbBtn       = GetRef<Button>(uiMgr, "navDatabaseButton");
        Button navMapBtn      = GetRef<Button>(uiMgr, "navMapButton");
        Button guideMeBtn     = GetRef<Button>(uiMgr, "guideMeButton");
        Button audioPbBtn     = GetRef<Button>(uiMgr, "audioPlaybackButton");
        Button view3DBtn      = GetRef<Button>(uiMgr, "view3DModelButton");
        Button nextPersonBtn  = GetRef<Button>(uiMgr, "nextPersonButton");
        Button prevPersonBtn  = GetRef<Button>(uiMgr, "prevPersonButton");
        Button btnEN          = GetRef<Button>(uiMgr, "buttonEnglish");
        Button btnDE          = GetRef<Button>(uiMgr, "buttonGerman");
        Button btnHE          = GetRef<Button>(uiMgr, "buttonHebrew");
        TMP_InputField searchInput = GetRef<TMP_InputField>(uiMgr, "searchInputField");
        Button miniPopupViewBtn    = GetRef<Button>(uiMgr, "miniPopupViewDetailsButton");

        // Fallback scene searches when Inspector wiring is missing
        if (hamburgerBtn == null && canvasTrans != null)
            hamburgerBtn = FindChildButton(canvasTrans, "Btn_Hamburger", "Btn_Menu", "MENU", "HamburgerButton");
        if (navDbBtn == null && canvasTrans != null)
            navDbBtn = FindChildButton(canvasTrans, "Btn_Nav_Database", "Btn_Search");
        if (navMapBtn == null && canvasTrans != null)
            navMapBtn = FindChildButton(canvasTrans, "Btn_Nav_Map", "Btn_Map");
        if (guideMeBtn == null)
            guideMeBtn = FindSceneButton("Btn_GuideMe");
        if (audioPbBtn == null)
            audioPbBtn = FindSceneButton("Btn_Playback", "AudioPlaybackButton");
        if (view3DBtn == null)
            view3DBtn = FindSceneButton("Btn_View3D", "Btn_InspectModel");
        if (nextPersonBtn == null)
            nextPersonBtn = FindSceneButton("Btn_NextPerson");
        if (prevPersonBtn == null)
            prevPersonBtn = FindSceneButton("Btn_PrevPerson");
        if (btnEN == null)
            btnEN = FindSceneButton("Btn_EN", "Button_EN", "Btn_English");
        if (btnDE == null)
            btnDE = FindSceneButton("Btn_DE", "Button_DE", "Btn_German");
        if (btnHE == null)
            btnHE = FindSceneButton("Btn_HE", "Button_HE", "Btn_Hebrew");
        if (searchInput == null)
            searchInput = UnityEngine.Object.FindAnyObjectByType<TMP_InputField>(FindObjectsInactive.Include);

        // Site history panel — found through its controller component
        var siteHistCtrl = UnityEngine.Object.FindAnyObjectByType<SiteHistoryDropdownController>(FindObjectsInactive.Include);
        RectTransform siteHistRect = siteHistCtrl != null ? siteHistCtrl.GetComponent<RectTransform>() : null;
        // Sidebar button that opens the Site History panel
        Button navSiteHistBtn = FindChildButton(canvasTrans, "Btn_Nav_SiteHistory", "Btn_SiteHistory", "Btn_Nav_History");

        // Map 2D raw image target
        RectTransform mapTarget = null;
        if (canvasTrans != null)
        {
            foreach (Transform t in canvasTrans.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Equals("Belsen Map Graphic", StringComparison.OrdinalIgnoreCase) ||
                    t.name.Equals("BelsenMapGraphic",   StringComparison.OrdinalIgnoreCase) ||
                    t.name.Equals("Map_RawImage",       StringComparison.OrdinalIgnoreCase))
                {
                    mapTarget = t.GetComponent<RectTransform>();
                    break;
                }
            }
            if (mapTarget == null)
            {
                Transform mapPanel = canvasTrans.Find("Map_2D_Panel") ?? canvasTrans.Find("Map2DPanel");
                if (mapPanel != null)
                {
                    var rawImg = mapPanel.GetComponentInChildren<UnityEngine.UI.RawImage>(true);
                    if (rawImg != null) mapTarget = rawImg.GetComponent<RectTransform>();
                }
            }
        }

        // Ensure list has exactly 9 steps and reset volatile per-step state
        // so stale Unity Inspector data (isToast, allowManualAdvance from old versions)
        // cannot break the tutorial flow.
        if (steps == null) steps = new List<CoachmarkStep>();
        while (steps.Count < 9) steps.Add(new CoachmarkStep());
        foreach (var s in steps)
        {
            s.isToast = false;
            s.allowManualAdvance = false;
        }

        // ------------------------------------------------------------------
        // STEP 0 — Hamburger / main menu button
        // ------------------------------------------------------------------
        steps[0].textEN = "Tap here to open the main menu.";
        steps[0].textDE = "Tippe hier, um das Hauptmenü zu öffnen.";
        SetStepTargets(steps[0], hamburgerBtn);
        steps[0].transitions = new List<CoachmarkTransition>();
        if (hamburgerBtn != null)
            steps[0].transitions.Add(new CoachmarkTransition { triggerButton = hamburgerBtn, nextStepIndex = 1 });

        // ------------------------------------------------------------------
        // STEP 1 — Sidebar: choose Database / Map / Site History
        // ------------------------------------------------------------------
        steps[1].textEN = "From here you can search for a person in the Database, explore the 2D Map, or listen to the Site History audio chapters. Choose one!";
        steps[1].textDE = "Suche nach einer Person, erkunde die 2D-Karte oder höre die historischen Audio-Kapitel. Wähle eine Option!";
        SetStepTargets(steps[1], navDbBtn, navMapBtn, navSiteHistBtn);
        steps[1].transitions = new List<CoachmarkTransition>();
        if (navDbBtn     != null) steps[1].transitions.Add(new CoachmarkTransition { triggerButton = navDbBtn,      nextStepIndex = 2 });
        if (navMapBtn    != null) steps[1].transitions.Add(new CoachmarkTransition { triggerButton = navMapBtn,     nextStepIndex = 4 });
        if (navSiteHistBtn != null) steps[1].transitions.Add(new CoachmarkTransition { triggerButton = navSiteHistBtn, nextStepIndex = 8 });

        // ------------------------------------------------------------------
        // STEP 2 — Search input field
        // ------------------------------------------------------------------
        steps[2].textEN = "Type a name to search, for example \"Frank\". The results will appear below.";
        steps[2].textDE = "Gib einen Namen ein, z. B. \"Frank\". Die Ergebnisse erscheinen darunter.";
        steps[2].allowManualAdvance = true;
        SetStepTargets(steps[2], searchInput);

        // ------------------------------------------------------------------
        // STEP 3 — Search results
        // ------------------------------------------------------------------
        steps[3].textEN = "Tap a name to open their full memorial, or tap '+' to add this person to your walking route.";
        steps[3].textDE = "Tippe auf einen Namen, um das Memorial zu öffnen, oder auf '+', um die Person zur Route hinzuzufügen.";
        // Target is resolved dynamically at runtime when results are populated (no target wired here)
        SetStepTargets(steps[3], (Component[])null);

        // ------------------------------------------------------------------
        // STEP 4 — 2D Map: find and tap a stone pin
        // ------------------------------------------------------------------
        steps[4].textEN = "Explore the map and tap any stone pin. A popup will appear — from there you can read their story or start a walking route.";
        steps[4].textDE = "Erkunde die Karte und tippe auf einen Stein-Pin. Im Popup kannst du die Geschichte lesen oder eine Route starten.";
        SetStepTargets(steps[4], mapTarget);
        steps[4].transitions = new List<CoachmarkTransition>();
        if (miniPopupViewBtn != null)
            steps[4].transitions.Add(new CoachmarkTransition { triggerButton = miniPopupViewBtn, nextStepIndex = 5 });

        // ------------------------------------------------------------------
        // STEP 5 — Memorial Detail: Language tabs EN / DE / HE
        // ------------------------------------------------------------------
        steps[5].textEN = "Select the EN, DE, or HE tab to read the epigraph inscription in English, German, or Hebrew.";
        steps[5].textDE = "Wähle EN, DE oder HE, um die Inschrift auf Englisch, Deutsch oder Hebräisch zu lesen.";
        SetStepTargets(steps[5], btnEN, btnDE, btnHE);
        steps[5].transitions = new List<CoachmarkTransition>();
        if (btnEN != null) steps[5].transitions.Add(new CoachmarkTransition { triggerButton = btnEN, nextStepIndex = 6 });
        if (btnDE != null) steps[5].transitions.Add(new CoachmarkTransition { triggerButton = btnDE, nextStepIndex = 6 });
        if (btnHE != null) steps[5].transitions.Add(new CoachmarkTransition { triggerButton = btnHE, nextStepIndex = 6 });

        // ------------------------------------------------------------------
        // STEP 6 — Memorial Detail: Audio narration + 3D Model viewer
        // ------------------------------------------------------------------
        steps[6].textEN = "Tap 🔊 to listen to an audio narration about this person, or tap 🔍 to open an interactive 3D model of the memorial stone.";
        steps[6].textDE = "Tippe auf 🔊 für eine Audio-Erzählung oder auf 🔍, um das 3D-Modell des Gedenksteins zu öffnen.";
        SetStepTargets(steps[6], audioPbBtn, view3DBtn);
        steps[6].transitions = new List<CoachmarkTransition>();
        if (audioPbBtn != null) steps[6].transitions.Add(new CoachmarkTransition { triggerButton = audioPbBtn, nextStepIndex = 7 });
        if (view3DBtn  != null) steps[6].transitions.Add(new CoachmarkTransition { triggerButton = view3DBtn,  nextStepIndex = 7 });

        // ------------------------------------------------------------------
        // STEP 7 — Memorial Detail: Person arrows + Guide me
        // ------------------------------------------------------------------
        steps[7].textEN = "Use the ◄ ► arrows to switch between victims buried at this stone. Tap 'Guide me' to start walking to this location!";
        steps[7].textDE = "Nutze die Pfeile ◄ ►, um zwischen den Opfern zu wechseln. Tippe auf 'Guide me', um die Navigation zu starten!";
        steps[7].allowManualAdvance = true;
        SetStepTargets(steps[7], prevPersonBtn, nextPersonBtn, guideMeBtn);
        steps[7].transitions = new List<CoachmarkTransition>();
        if (guideMeBtn != null)
            steps[7].transitions.Add(new CoachmarkTransition { triggerButton = guideMeBtn, nextStepIndex = -1 });

        // ------------------------------------------------------------------
        // STEP 8 — Site History: toast so it never clips against a full-screen panel
        // ------------------------------------------------------------------
        steps[8].textEN = "Here you can explore the historical audio chapters of Bergen-Belsen. Tap a chapter to play and learn about the camp's history.";
        steps[8].textDE = "Hier kannst du die historischen Audio-Kapitel von Bergen-Belsen hören. Tippe auf ein Kapitel, um es abzuspielen.";
        steps[8].isToast = true;
        steps[8].toastDuration = 5f;
        SetStepTargets(steps[8], (Component[])null);
    }

    // -------------------------------------------------------------------------
    // Internal step flow
    // -------------------------------------------------------------------------

    private void GoToStep(int index)
    {
        ClearActiveListeners();

        if (index < 0 || index >= steps.Count)
        {
            EndTutorial(markCompleted: true);
            return;
        }

        // For steps that target UI elements inside panels that open AFTER tutorial start
        // (sidebar, detail panel buttons, site history), re-resolve all targets now that the
        // panel is actually open and in the scene.
        if (index == 1 || index >= 5)
        {
            var uiMgr = UnityEngine.Object.FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
            if (uiMgr != null && index == 1) uiMgr.OpenSidebar();
            SanitizeAndSyncSteps();
        }

        currentStepIndex = index;
        CoachmarkStep step = steps[index];
        hasManualAdvancePending = step.allowManualAdvance;

        if (step.isToast)
        {
            ShowToast(step);
            EndTutorial(markCompleted: true);
            return;
        }

        DisplayFrameAndBubble(step, stepNumberLabel: $"{index + 1}/{CountRealSteps()}");

        var transitionButtons = new HashSet<Button>();

        // Step 0: hamburger transitions are wired by the generic transition loop below.
        // UIManager's own onClick listener already calls OpenPanel(sidebarMenuPanel).
        // We only need GoToStep(1) to also call OpenSidebar so the coachmark overlay can
        // highlight the sidebar buttons — that is done at the top of GoToStep when index==1.

        // Auto-wire typing trigger for search input step
        if (index == 2)
        {
            var searchInputComp = UnityEngine.Object.FindAnyObjectByType<TMP_InputField>(FindObjectsInactive.Include);
            if (searchInputComp != null)
            {
                UnityEngine.Events.UnityAction<string> onTypeHandler = null;
                onTypeHandler = (str) =>
                {
                    if (!string.IsNullOrEmpty(str) && str.Length >= 2)
                    {
                        searchInputComp.onValueChanged.RemoveListener(onTypeHandler);
                        searchInputComp.onSubmit.RemoveListener(onTypeHandler);
                        GoToNextStepManually();
                    }
                };
                searchInputComp.onValueChanged.AddListener(onTypeHandler);
                searchInputComp.onSubmit.AddListener(onTypeHandler);
            }
        }
        else if (index == 3)
        {
            var uiMgr = UnityEngine.Object.FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
            Transform resultsContainerTrans = GetRef<Transform>(uiMgr, "searchResultsContainer");
            TMP_InputField searchInputComp = GetRef<TMP_InputField>(uiMgr, "searchInputField")
                                          ?? UnityEngine.Object.FindAnyObjectByType<TMP_InputField>(FindObjectsInactive.Include);

            void RefreshStep3State()
            {
                if (resultsContainerTrans == null || searchInputComp == null) return;

                Button[] resultButtons = resultsContainerTrans.GetComponentsInChildren<Button>(true);

                if (resultButtons.Length > 0)
                {
                    ScrollRect scrollRect = resultsContainerTrans.GetComponentInParent<ScrollRect>();
                    RectTransform targetRect = scrollRect != null ? scrollRect.GetComponent<RectTransform>() : (RectTransform)resultsContainerTrans;
                    step.highlightTargets = new[] { targetRect };

                    step.textEN = "Tap a name to open their full memorial, or tap '+' to add this person to your walking route.";
                    step.textDE = "Tippe auf einen Namen, um das Memorial zu öffnen, oder auf '+', um die Person zur Route hinzuzufügen.";
                    RefreshBubbleText(step);

                    foreach (Button b in resultButtons)
                    {
                        transitionButtons.Add(b); // Prevent generic auto-wiring loop from attaching GoToStep(4)
                        void ResultHandler()
                        {
                            b.onClick.RemoveListener(ResultHandler);
                            GoToStep(5);
                        }
                        b.onClick.AddListener(ResultHandler);
                        activeListeners.Add((b, ResultHandler));
                    }
                }
                else
                {
                    step.highlightTargets = new[] { searchInputComp.GetComponent<RectTransform>() };
                    step.textEN = "No matching records found. Try typing a name like \"Fela\" or \"Hartmann\" in the search field above.";
                    step.textDE = "Keine Ergebnisse gefunden. Versuche einen Namen wie \"Fela\" oder \"Hartmann\" oben einzugeben.";
                    RefreshBubbleText(step);
                }

                PositionFrameAroundTargets(step.highlightTargets);
                PositionBubbleAndArrow(step.highlightTargets);
            }

            RefreshStep3State();

            if (searchInputComp != null)
            {
                UnityEngine.Events.UnityAction<string> onSearchChangeHandler = (str) =>
                {
                    StartCoroutine(DeferredRefreshStep3(RefreshStep3State));
                };
                searchInputComp.onValueChanged.AddListener(onSearchChangeHandler);
            }
        }
        else if (index == 4)
        {
            // Auto-advance when the user opens a stone detail from the map popup
            var uiMgr = UnityEngine.Object.FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
            Button viewDetailsBtn = GetRef<Button>(uiMgr, "miniPopupViewDetailsButton");
            if (viewDetailsBtn != null)
            {
                void PopupHandler()
                {
                    viewDetailsBtn.onClick.RemoveListener(PopupHandler);
                    GoToStep(5);
                }
                viewDetailsBtn.onClick.AddListener(PopupHandler);
                activeListeners.Add((viewDetailsBtn, PopupHandler));
            }
        }
        else if (index == 7)
        {
            var uiMgr = UnityEngine.Object.FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
            Button guideMeBtn    = GetRef<Button>(uiMgr, "guideMeButton");

            if (guideMeBtn != null && guideMeBtn.gameObject.activeInHierarchy)
            {
                void GuideMeHandler()
                {
                    guideMeBtn.onClick.RemoveListener(GuideMeHandler);
                    GoToStep(-1);
                }
                guideMeBtn.onClick.AddListener(GuideMeHandler);
                activeListeners.Add((guideMeBtn, GuideMeHandler));
            }
        }

        // Wire explicit transitions
        if (step.transitions != null)
        {
            foreach (CoachmarkTransition transition in step.transitions)
            {
                if (transition.triggerButton == null) continue;
                transitionButtons.Add(transition.triggerButton);
                int capturedNext = transition.nextStepIndex;
                void Handler() => GoToStep(capturedNext);
                transition.triggerButton.onClick.AddListener(Handler);
                activeListeners.Add((transition.triggerButton, Handler));
            }
        }

        // Auto-wire highlighted target buttons that have no explicit transition
        if (step.highlightTargets != null)
        {
            foreach (RectTransform target in step.highlightTargets)
            {
                if (target == null) continue;
                Button btn = target.GetComponent<Button>() ?? target.GetComponentInChildren<Button>();
                if (btn != null && !transitionButtons.Contains(btn))
                {
                    int nextIdx = index + 1;
                    void Handler() => GoToStep(nextIdx);
                    btn.onClick.AddListener(Handler);
                    activeListeners.Add((btn, Handler));
                }
            }
        }

        if (skipButton != null)
        {
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(SkipTutorial);
        }
    }

    private int CountRealSteps()
    {
        int count = 0;
        foreach (var s in steps) if (!s.isToast) count++;
        return count;
    }

    private void DisplayFrameAndBubble(CoachmarkStep step, string stepNumberLabel)
    {
        if (overlayRoot != null) overlayRoot.gameObject.SetActive(true);
        if (bubbleRoot  != null) bubbleRoot.gameObject.SetActive(true);

        RefreshBubbleText(step);

        // Open the first cutout immediately so the first hamburger tap reaches its target.
        Canvas.ForceUpdateCanvases();
        PositionFrameAroundTargets(step.highlightTargets);
        PositionBubbleAndArrow(step.highlightTargets);

        if (stepCounterText != null)
        {
            stepCounterText.gameObject.SetActive(stepNumberLabel != null);
            if (stepNumberLabel != null) stepCounterText.text = stepNumberLabel;
        }

        if (updateFrameRoutine != null) StopCoroutine(updateFrameRoutine);
        updateFrameRoutine = StartCoroutine(UpdateFrameDeferred(step));
    }

    private IEnumerator UpdateFrameDeferred(CoachmarkStep step)
    {
        Canvas.ForceUpdateCanvases();
        PositionFrameAroundTargets(step.highlightTargets);
        PositionBubbleAndArrow(step.highlightTargets);
        yield return null;
        Canvas.ForceUpdateCanvases();
        PositionFrameAroundTargets(step.highlightTargets);
        PositionBubbleAndArrow(step.highlightTargets);
    }

    private void RefreshBubbleText(CoachmarkStep step)
    {
        if (bubbleText == null) return;
        bubbleText.text = GetStepText(step);
        if (skipButtonLabel != null)
        {
            skipButtonLabel.text = currentLanguage switch
            {
                AppLanguage.DE => "Tutorial überspringen",
                AppLanguage.HE => "דלג על המדריך",
                _ => "Skip tutorial"
            };
        }
    }

    // -------------------------------------------------------------------------
    // Frame & bubble positioning
    // -------------------------------------------------------------------------

    private void PositionFrameAroundTargets(RectTransform[] targets)
    {
        if (overlayRoot == null) return;

        bool hasValidTarget = false;
        Rect combined = Rect.zero;

        if (targets != null)
        {
            foreach (RectTransform t in targets)
            {
                if (t == null) continue;
                Rect r = GetScreenRect(t);
                if (r.width > 0 && r.height > 0)
                {
                    combined = hasValidTarget ? Encapsulate(combined, r) : r;
                    hasValidTarget = true;
                }
            }
        }

        float screenW = overlayRoot.rect.width;
        float screenH = overlayRoot.rect.height;

        if (!hasValidTarget)
        {
            SetPanel(frameTop,    0f, 0f, screenW, screenH);
            SetPanel(frameBottom, 0f, 0f, 0f, 0f);
            SetPanel(frameLeft,   0f, 0f, 0f, 0f);
            SetPanel(frameRight,  0f, 0f, 0f, 0f);
            return;
        }

        combined.xMin -= cutoutPadding;
        combined.yMin -= cutoutPadding;
        combined.xMax += cutoutPadding;
        combined.yMax += cutoutPadding;

        SetPanel(frameTop,    0f,             combined.yMax, screenW,                   screenH - combined.yMax);
        SetPanel(frameBottom, 0f,             0f,            screenW,                   combined.yMin);
        SetPanel(frameLeft,   0f,             combined.yMin, combined.xMin,             combined.height);
        SetPanel(frameRight,  combined.xMax,  combined.yMin, screenW - combined.xMax,   combined.height);
    }

    private void SetPanel(RectTransform panel, float x, float y, float width, float height)
    {
        if (panel == null) return;
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.zero;
        panel.pivot     = Vector2.zero;
        panel.anchoredPosition = new Vector2(Mathf.Max(0f, x),     Mathf.Max(0f, y));
        panel.sizeDelta        = new Vector2(Mathf.Max(0f, width),  Mathf.Max(0f, height));
    }

    private Rect GetScreenRect(RectTransform target)
    {
        if (target == null || overlayRoot == null) return Rect.zero;

        Canvas parentCanvas = overlayRoot.GetComponentInParent<Canvas>();
        Camera cam = (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? parentCanvas.worldCamera : null;

        Vector3[] corners = new Vector3[4];
        target.GetWorldCorners(corners);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRoot,
            RectTransformUtility.WorldToScreenPoint(cam, corners[0]), cam, out Vector2 min);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRoot,
            RectTransformUtility.WorldToScreenPoint(cam, corners[2]), cam, out Vector2 max);

        float xMin = Mathf.Min(min.x, max.x);
        float xMax = Mathf.Max(min.x, max.x);
        float yMin = Mathf.Min(min.y, max.y);
        float yMax = Mathf.Max(min.y, max.y);

        Vector2 pivotOffset = new Vector2(
            overlayRoot.rect.width  * overlayRoot.pivot.x,
            overlayRoot.rect.height * overlayRoot.pivot.y);
        return new Rect(xMin + pivotOffset.x, yMin + pivotOffset.y, xMax - xMin, yMax - yMin);
    }

    private static Rect Encapsulate(Rect a, Rect b) =>
        Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin),
                        Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

    private void PositionBubbleAndArrow(RectTransform[] targets)
    {
        if (bubbleRoot == null || targets == null || targets.Length == 0) return;

        Rect combined = GetScreenRect(targets[0]);
        for (int i = 1; i < targets.Length; i++)
        {
            if (targets[i] != null)
                combined = Encapsulate(combined, GetScreenRect(targets[i]));
        }

        float screenH      = overlayRoot != null ? overlayRoot.rect.height : Screen.height;
        float overlayWidth = overlayRoot != null ? overlayRoot.rect.width  : Screen.width;
        float bubbleHalfW  = bubbleRoot.rect.width * 0.5f;

        bool targetInLowerHalf = combined.center.y < screenH * 0.5f;
        bool targetOnRightEdge = combined.xMin      > overlayWidth * 0.5f;

        float bubbleX, bubbleY;
        if (targetOnRightEdge)
        {
            bubbleX = Mathf.Max(bubbleHalfW + 16f, combined.xMin - bubbleHalfW - 24f);
            bubbleY = Mathf.Clamp(combined.center.y,
                bubbleRoot.rect.height * 0.5f + 16f,
                screenH - bubbleRoot.rect.height * 0.5f - 16f);
            SetArrowDirection("RIGHT");
        }
        else
        {
            bubbleX = Mathf.Clamp(combined.center.x, bubbleHalfW + 16f, overlayWidth - bubbleHalfW - 16f);
            bubbleY = targetInLowerHalf
                ? combined.yMax + cutoutPadding + bubbleRoot.rect.height * 0.5f + 24f
                : combined.yMin - cutoutPadding - bubbleRoot.rect.height * 0.5f - 24f;
            SetArrowDirection(targetInLowerHalf ? "BOTTOM" : "TOP");
        }

        bubbleRoot.anchorMin = new Vector2(0.5f, 0f);
        bubbleRoot.anchorMax = new Vector2(0.5f, 0f);
        bubbleRoot.pivot     = new Vector2(0.5f, 0.5f);
        bubbleRoot.anchoredPosition = new Vector2(bubbleX - overlayWidth * 0.5f, bubbleY);

        if (arrowGlyph != null)
        {
            var arrowRt = arrowGlyph.GetComponent<RectTransform>();
            if (arrowRt != null)
            {
                if (targetOnRightEdge)
                {
                    arrowRt.anchorMin = new Vector2(0.95f, 0.4f);
                    arrowRt.anchorMax = new Vector2(1.08f, 0.6f);
                }
                else
                {
                    arrowRt.anchorMin = new Vector2(0.45f, targetInLowerHalf ? -0.08f : 0.92f);
                    arrowRt.anchorMax = new Vector2(0.55f, targetInLowerHalf ?  0.05f : 1.05f);
                }
            }
        }
    }

    private void SetArrowDirection(string dir)
    {
        if (arrowTop    != null) arrowTop.SetActive(dir    == "TOP");
        if (arrowBottom != null) arrowBottom.SetActive(dir == "BOTTOM");
        if (arrowLeft   != null) arrowLeft.SetActive(dir   == "LEFT");
        if (arrowRight  != null) arrowRight.SetActive(dir  == "RIGHT");

        if (arrowGlyph != null)
        {
            bool hasCustomObjects = arrowTop != null || arrowBottom != null || arrowLeft != null || arrowRight != null;
            arrowGlyph.gameObject.SetActive(!hasCustomObjects);
            if (!hasCustomObjects)
            {
                arrowGlyph.text = dir switch
                {
                    "TOP"    => "▲",
                    "BOTTOM" => "▼",
                    "LEFT"   => "◄",
                    "RIGHT"  => "►",
                    _        => arrowGlyph.text
                };
            }
        }
    }

    // -------------------------------------------------------------------------
    // Toast
    // -------------------------------------------------------------------------

    private void ShowToast(CoachmarkStep step)
    {
        if (toastRoot == null || toastText == null) return;
        toastText.text = GetStepText(step);
        StartCoroutine(ToastRoutine(step.toastDuration));
    }

    private IEnumerator ToastRoutine(float duration)
    {
        toastRoot.gameObject.SetActive(true);
        yield return new WaitForSeconds(duration);
        toastRoot.gameObject.SetActive(false);
    }

    private IEnumerator DeferredRefreshStep3(Action refreshAction)
    {
        yield return null; // Wait one frame for UI search results to populate
        refreshAction?.Invoke();
    }

    // -------------------------------------------------------------------------
    // Listener management & lifecycle
    // -------------------------------------------------------------------------

    private void ClearActiveListeners()
    {
        foreach (var (button, action) in activeListeners)
            if (button != null) button.onClick.RemoveListener(action);
        activeListeners.Clear();
    }

    private void HideEverything()
    {
        ClearActiveListeners();
        if (overlayRoot != null) overlayRoot.gameObject.SetActive(false);
        if (bubbleRoot  != null) bubbleRoot.gameObject.SetActive(false);
        if (toastRoot   != null) toastRoot.gameObject.SetActive(false);
    }

    private void EndTutorial(bool markCompleted)
    {
        ClearActiveListeners();
        HideEverything();
        currentStepIndex = -1;
        if (markCompleted)
        {
            PlayerPrefs.SetInt(PrefKeyCompleted, 1);
            PlayerPrefs.Save();
        }
    }

    // -------------------------------------------------------------------------
    // Reflection helpers
    // -------------------------------------------------------------------------

    private void SetStepTargets(CoachmarkStep step, params Component[] comps)
    {
        if (step == null) return;
        var list = new List<RectTransform>();
        if (comps != null)
        {
            foreach (Component c in comps)
            {
                if (c == null) continue;
                RectTransform rt = c.GetComponent<RectTransform>();
                if (rt != null && !list.Contains(rt)) list.Add(rt);
            }
        }
        step.highlightTargets = list.ToArray();
    }

    private T GetRef<T>(object target, string fieldName) where T : class
    {
        if (target == null) return null;
        var field = target.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance  |
            System.Reflection.BindingFlags.Public);
        return field?.GetValue(target) as T;
    }

    private Button FindChildButton(Transform parent, params string[] names)
    {
        if (parent == null) return null;
        foreach (string n in names)
        {
            Transform child = parent.Find(n);
            if (child != null)
            {
                Button btn = child.GetComponent<Button>();
                if (btn != null) return btn;
            }
            // Deep search
            foreach (Button btn in parent.GetComponentsInChildren<Button>(true))
            {
                if (btn.name.Equals(n, StringComparison.OrdinalIgnoreCase) ||
                    btn.name.Contains(n)) return btn;
            }
        }
        return null;
    }

    private Button FindSceneButton(params string[] names)
    {
        foreach (string n in names)
        {
            GameObject go = GameObject.Find(n);
            if (go != null)
            {
                Button btn = go.GetComponent<Button>() ?? go.GetComponentInChildren<Button>();
                if (btn != null) return btn;
            }
        }
        return null;
    }
}
