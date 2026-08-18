using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central orchestrator for the thesis guide modes and user experience session logging.
/// </summary>
public class ThesisManager : MonoBehaviour
{
    [Header("🎯 Guidance System References - Fixed Scene GameObjects")]
    [SerializeField] private PersonalGuidance personalGuidanceInstance;
    [SerializeField] private IntermediateGuidance intermediateGuidanceInstance;
    [SerializeField] private ImpersonalGuidance impersonalGuidanceInstance;

    [Header("🤖 Companion Settings")]
    [SerializeField] private GameObject guideAvatarPrefab; // Trascina qui il PREFAB del MODELLO 3D del tuo Avatar/Guida
    [SerializeField] private GameObject guideAvatarInstance; // L'oggetto avatar 3D FISSO e PERSISTENTE della scena

    private bool isExperienceStarted = false;
    public bool IsExperienceStarted => isExperienceStarted;

    public void SetGuidanceMode(GuidanceMode mode, bool triggerAvatarAndAudio = true)
    {
        Debug.Log($"[LIFECYCLE TRACE] ThesisManager.SetGuidanceMode({mode}, triggerAvatarAndAudio={triggerAvatarAndAudio}) - isExperienceStarted={isExperienceStarted}");
        currentMode = mode;

        if (currentGuidanceSystem != null)
        {
            if (currentGuidanceSystem is IntermediateGuidance outgoingInter) outgoingInter.DespawnAvatar();
            if (currentGuidanceSystem is PersonalGuidance outgoingPers) outgoingPers.DespawnAvatar();

            currentGuidanceSystem.gameObject.SetActive(false);
            currentGuidanceSystem = null;
        }

        // Global scene cleanup of any orphaned avatar GameObjects during mode transition
        UIManager.DespawnAllGuidanceAvatarsExcept(null);

        // Auto-resolve fixed scene instances if missing from Inspector assignment
        ResolveGuidanceInstances();

        switch (mode)
        {
            case GuidanceMode.Impersonal:
                if (impersonalGuidanceInstance == null)
                {
                    Debug.LogError("[ThesisManager] ❌ impersonalGuidanceInstance is null in scene!");
                    return;
                }
                currentGuidanceSystem = impersonalGuidanceInstance;
                break;

            case GuidanceMode.Intermediate:
                if (intermediateGuidanceInstance == null)
                {
                    Debug.LogError("[ThesisManager] ❌ intermediateGuidanceInstance is null in scene!");
                    return;
                }
                intermediateGuidanceInstance.ConfigureAvatarPrefab(guideAvatarPrefab);
                currentGuidanceSystem = intermediateGuidanceInstance;
                break;

            case GuidanceMode.Personal:
                if (personalGuidanceInstance == null)
                {
                    Debug.LogError("[ThesisManager] ❌ personalGuidanceInstance is null in scene!");
                    return;
                }
                personalGuidanceInstance.ConfigureAvatarPrefab(guideAvatarPrefab);
                currentGuidanceSystem = personalGuidanceInstance;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }

        currentGuidanceSystem.gameObject.SetActive(true);
        currentGuidanceSystem.Initialize(uiManager, wayfindingManager, this);
        LogEvent("guidance_mode_changed", string.Empty, mode.ToString());
        TelemetryLogger.Instance?.LogModeSwitched(mode.ToString());

        // A running tour, rather than the last manually selected memorial, owns the active target.
        string restoredTargetID = activeMemorialID;
        TourManager activeTour = GetTourManager();
        if (activeTour != null && activeTour.IsTourActiveAndRunning)
            restoredTargetID = activeTour.GetCurrentTargetStoneID();

        // Restore active selected memorial state ONLY if experience is active or explicitly triggered
        if (isExperienceStarted && triggerAvatarAndAudio)
        {
            Debug.Log($"[LIFECYCLE TRACE] ThesisManager invoking OnMemorialSelected('{restoredTargetID ?? ""}') for {currentGuidanceSystem.GetType().Name}");
            currentGuidanceSystem.OnMemorialSelected(restoredTargetID ?? "");
        }
        else
        {
            Debug.Log($"[LIFECYCLE TRACE] ThesisManager skipping OnMemorialSelected because isExperienceStarted={isExperienceStarted}, triggerAvatarAndAudio={triggerAvatarAndAudio}");
        }

        // Synchronize Settings dropdown selection and Onboarding button highlights
        SettingsUIController settingsUI = UnityEngine.Object.FindAnyObjectByType<SettingsUIController>(FindObjectsInactive.Include);
        if (settingsUI != null)
        {
            settingsUI.SyncDropdownSelection(mode);
        }

        if (uiManager != null)
        {
            uiManager.UpdateOnboardingButtonsVisuals(mode);
            uiManager.UpdateSummonGuideVisibility(mode);
        }
    }

    /// <summary>
    /// Officially starts the active experience session after the user completes Onboarding.
    /// Spawns the 3D avatar and triggers welcome narration audio for the first time.
    /// </summary>
    public void StartExperienceSession()
    {
        isExperienceStarted = true;
        Debug.Log($"[LIFECYCLE TRACE] ThesisManager.StartExperienceSession() called. Active mode: {currentMode}");

        if (currentGuidanceSystem != null)
        {
            string welcomeClip = currentMode switch
            {
                GuidanceMode.Intermediate => "WELCOME_INTERMEDIATE_EN",
                GuidanceMode.Personal => "WELCOME_PERSONAL_EN",
                _ => "WELCOME_IMPERSONAL_EN"
            };

            Debug.Log($"[LIFECYCLE TRACE] StartExperienceSession triggering OnMemorialSelected('{welcomeClip}') on {currentGuidanceSystem.GetType().Name}");
            currentGuidanceSystem.OnMemorialSelected(welcomeClip);
        }
    }


    public enum GuidanceMode
    {
        Impersonal,
        Intermediate,
        Personal
    }

    [Serializable]
    public class UserInteractionEvent
    {
        public float timestamp;
        public string eventType;
        public string memorialID;
        public string details;
    }

    [Serializable]
    public class TestSession
    {
        public string userID;
        public GuidanceMode mode;
        public string startTime;
        public float totalSessionDuration;
        public bool gdprConsent;
        public List<UserInteractionEvent> events = new List<UserInteractionEvent>();
    }

    public static ThesisManager Instance { get; private set; }

    [SerializeField] private GuidanceMode currentMode = GuidanceMode.Personal;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private ARWayfindingManager wayfindingManager;
    [SerializeField] private MemorialSpawner memorialSpawner;

    private GuidanceSystemBase currentGuidanceSystem;
    private TestSession currentSession;
    private float sessionStartTime;
    private string activeMemorialID = null; // Stays null until the experience begins or a stone is selected


    public GuidanceMode CurrentMode => currentMode;
    public GuidanceSystemBase CurrentGuidanceSystem => currentGuidanceSystem;

    public GameObject GuideAvatarPrefab => guideAvatarPrefab;
    public GameObject GuideAvatarInstance => guideAvatarInstance;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ResolveGuidanceInstances();
    }

    void Start()
    {
        GameObject arController = GameObject.Find("AR_Core_Controller");
        if (arController != null && !arController.activeSelf) arController.SetActive(true);
#if UNITY_ANDROID
        RequestAndroidPermissions();
#endif

        if (uiManager == null)
            uiManager = UnityEngine.Object.FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
        if (wayfindingManager == null)
            wayfindingManager = UnityEngine.Object.FindAnyObjectByType<ARWayfindingManager>(FindObjectsInactive.Include);
        if (memorialSpawner == null)
            memorialSpawner = UnityEngine.Object.FindAnyObjectByType<MemorialSpawner>(FindObjectsInactive.Include);

        if (wayfindingManager != null)
            wayfindingManager.OnMemorialReached += HandleMemorialReached;

        // Advance the tour only once the just-arrived stone has actually finished narrating

        // Centralized self-healing check to make sure RuntimeStonePopulator and TelemetryLogger are present in the scene
        if (UnityEngine.Object.FindAnyObjectByType<RuntimeStonePopulator>(FindObjectsInactive.Include) == null)
        {
            GameObject controller = GameObject.Find("AR_Core_Controller");
            if (controller != null)
            {
                controller.AddComponent<RuntimeStonePopulator>();
                Debug.Log("[Self-Healing] Automatically added missing RuntimeStonePopulator component to 'AR_Core_Controller'.");
            }
            else
            {
                GameObject managers = GameObject.Find("_Managers");
                if (managers != null)
                {
                    managers.AddComponent<RuntimeStonePopulator>();
                    Debug.Log("[Self-Healing] Automatically added missing RuntimeStonePopulator component to '_Managers'.");
                }
            }
        }

        if (UnityEngine.Object.FindAnyObjectByType<TelemetryLogger>(FindObjectsInactive.Include) == null)
        {
            GameObject managers = GameObject.Find("_Managers") ?? GameObject.Find("AR_Core_Controller") ?? gameObject;
            managers.AddComponent<TelemetryLogger>();
            Debug.Log("[Self-Healing] Automatically added missing TelemetryLogger component.");
        }

        if (UnityEngine.Object.FindAnyObjectByType<SurveyReminderManager>(FindObjectsInactive.Include) == null)
        {
            GameObject managers = GameObject.Find("_Managers") ?? GameObject.Find("AR_Core_Controller") ?? gameObject;
            managers.AddComponent<SurveyReminderManager>();
            Debug.Log("[Self-Healing] Automatically added missing SurveyReminderManager component.");
        }

        // Load saved mode from PlayerPrefs, default to index 0 which corresponds to GuidanceMode.Personal
        int savedIndex = PlayerPrefs.GetInt("Thesis_GuidanceMode", 0);
        GuidanceMode mode;
        if (savedIndex == 0) mode = GuidanceMode.Personal;
        else if (savedIndex == 1) mode = GuidanceMode.Intermediate;
        else mode = GuidanceMode.Impersonal;

        currentMode = mode;

        // Align GDPR consent from PlayerPrefs before session begins (Default 0 = FALSE for opt-in compliance)
        UserConsentGDPR = PlayerPrefs.GetInt("Thesis_GDPRConsent", 0) == 1;

        BeginSession(currentMode);
        SetGuidanceMode(currentMode, triggerAvatarAndAudio: false);
    }

    private void RequestAndroidPermissions()
    {
#if UNITY_ANDROID
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
        }
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.FineLocation))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.FineLocation);
        }
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.CoarseLocation))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.CoarseLocation);
        }
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission("android.permission.RECORD_AUDIO"))
        {
            UnityEngine.Android.Permission.RequestUserPermission("android.permission.RECORD_AUDIO");
        }
#endif
    }


    void Update()
    {
        // Debug keybinds for testing evaluation modes on the fly using the New Input System architecture
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            if (UnityEngine.InputSystem.Keyboard.current[UnityEngine.InputSystem.Key.I].wasPressedThisFrame)
                SetGuidanceMode(GuidanceMode.Impersonal);

            if (UnityEngine.InputSystem.Keyboard.current[UnityEngine.InputSystem.Key.O].wasPressedThisFrame)
                SetGuidanceMode(GuidanceMode.Intermediate);

            if (UnityEngine.InputSystem.Keyboard.current[UnityEngine.InputSystem.Key.P].wasPressedThisFrame)
                SetGuidanceMode(GuidanceMode.Personal);
        }
    }



    public void OnMemorialSelected(string memorialID)
    {
        activeMemorialID = memorialID;
        currentGuidanceSystem?.OnMemorialSelected(memorialID);

        if (wayfindingManager != null)
            wayfindingManager.NavigateTo(memorialID);
    }

    public void OnMemorialDeselected()
    {
        if (!string.IsNullOrEmpty(activeMemorialID))
        {
            TelemetryLogger.Instance?.OnStoneExited(activeMemorialID);
        }
        activeMemorialID = null;
        currentGuidanceSystem?.OnMemorialDeselected();
        if (wayfindingManager != null)
            wayfindingManager.StopNavigation();
    }

    private TourManager cachedTourManager;
    private TourManager GetTourManager()
    {
        if (cachedTourManager == null) cachedTourManager = UnityEngine.Object.FindAnyObjectByType<TourManager>(FindObjectsInactive.Include);
        return cachedTourManager;
    }

    private void HandleMemorialReached(string memorialID)
    {
        currentGuidanceSystem?.OnMemorialReached(memorialID);

        LogEvent("guidance_reached", memorialID, currentMode.ToString());
        TelemetryLogger.Instance?.OnStoneEntered(memorialID);

        bool isSystemicAudio = memorialID.Equals("WELCOME_EN") || memorialID.Equals("GOODBYE_EN") || memorialID.Equals("LOGISTICS_EN");
        if (!isSystemicAudio && uiManager != null)
        {
            uiManager.ShowMemorialDetail(memorialID);
        }

        // Preserve the reached memorial's popup while the next navigation target
        // is prepared in the background.
        TourManager tourManager = GetTourManager();
        if (currentMode == GuidanceMode.Impersonal && tourManager != null && tourManager.IsTourActiveAndRunning)
        {
            tourManager.AdvanceToNextStop();
        }

    }

    public void LogEvent(string eventType, string memorialID, string details = "")
    {
        if (currentSession == null)
            return;

        currentSession.events.Add(new UserInteractionEvent
        {
            timestamp = Time.time,
            eventType = eventType,
            memorialID = memorialID,
            details = details
        });
    }

    [Header("📊 Telemetry & Webhook Configuration")]
    [SerializeField] private string googleAppsScriptWebhookUrl = "https://script.google.com/macros/s/AKfycbw6oDJTvopDZ7kpuZdyLO8O88hTk_ZTswIp5jxFStSMeXYb9HvWNxGrUxr7cCjHh2dtFg/exec"; // Endpoint URL for Google Apps Script Webhook
    [SerializeField] private string surveyFormBaseUrl = "https://docs.google.com/forms/d/e/1FAIpQLScmedae9K0MAOLDULWO2kUa74cGDmm-DHshPgdJsZWauG_X4g/viewform";
    [SerializeField] private bool userConsentGDPR = false; // Default OFF for GDPR compliance
    private string anonymousUserID;

    public bool UserConsentGDPR
    {
        get => userConsentGDPR;
        set
        {
            userConsentGDPR = value;
        }
    }

    /// <summary>
    /// Generates a fresh anonymous user ID and starts a new session (essential when multiple professors/participants test on a shared device).
    /// </summary>
    public void RegenerateNewUserSession()
    {
        anonymousUserID = "USER_" + UnityEngine.Random.Range(100000, 999999).ToString();
        PlayerPrefs.SetString("Thesis_AnonymousUserID", anonymousUserID);
        PlayerPrefs.Save();
        BeginSession(currentMode);
        Debug.Log($"[ThesisManager] Regenerated new participant session ID: '{anonymousUserID}'");
    }

    private void BeginSession(GuidanceMode mode)
    {
        sessionStartTime = Time.time;
        currentSession = new TestSession
        {
            userID = AnonymousUserID,
            mode = mode,
            startTime = DateTime.Now.ToString("o"),
            totalSessionDuration = 0f,
            gdprConsent = userConsentGDPR,
            events = new List<UserInteractionEvent>()
        };
    }

    /// <summary>
    /// Synchronously saves session data to local disk. Always safe on Android pause.
    /// </summary>
    public void SaveSessionDataLocallyOnly(string fileName)
    {
        if (currentSession == null) return;

        currentSession.totalSessionDuration = Time.time - sessionStartTime;
        currentSession.gdprConsent = userConsentGDPR;
        string json = JsonUtility.ToJson(currentSession, true);
        string path = System.IO.Path.Combine(Application.persistentDataPath, fileName);
        System.IO.File.WriteAllText(path, json);
        Debug.Log($"[ThesisManager] Saved session JSON locally to: '{path}'");

        TelemetryLogger.Instance?.SaveTelemetryLogs();
    }

    /// <summary>
    /// Saves session data to local disk FIRST, and then uploads to Webhook if app is in foreground and GDPR consent is active.
    /// </summary>
    public void SaveAndUploadSessionData(string fileName)
    {
        if (currentSession == null) return;

        // 1. ALWAYS write to local disk first
        SaveSessionDataLocallyOnly(fileName);

        // 2. Upload via Webhook ONLY if user consented AND app is running in foreground
        string json = JsonUtility.ToJson(currentSession, true);
        if (userConsentGDPR && !string.IsNullOrEmpty(googleAppsScriptWebhookUrl))
        {
            StartCoroutine(PostTelemetryDataToWebhook(googleAppsScriptWebhookUrl, json));
        }
    }

    private System.Collections.IEnumerator PostTelemetryDataToWebhook(string url, string jsonPayload)
    {
        using (UnityEngine.Networking.UnityWebRequest request = new UnityEngine.Networking.UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.Log("[ThesisManager] Telemetry JSON uploaded successfully to Google Sheet Webhook!");
            }
            else
            {
                Debug.LogWarning($"[ThesisManager] Telemetry Webhook upload status: {request.error}");
            }
        }
    }

    public string AnonymousUserID
    {
        get
        {
            if (string.IsNullOrEmpty(anonymousUserID))
            {
                anonymousUserID = PlayerPrefs.GetString("Thesis_AnonymousUserID", "");
                if (string.IsNullOrEmpty(anonymousUserID))
                {
                    anonymousUserID = "USER_" + UnityEngine.Random.Range(100000, 999999).ToString();
                    PlayerPrefs.SetString("Thesis_AnonymousUserID", anonymousUserID);
                    PlayerPrefs.Save();
                }
            }
            return anonymousUserID;
        }
    }

    public void OpenPostVisitSurvey()
    {
        if (string.IsNullOrWhiteSpace(surveyFormBaseUrl) || surveyFormBaseUrl.Contains("..."))
        {
            Debug.LogWarning("[ThesisManager] Post-visit survey URL has not been configured.");
            uiManager?.ShowNotificationToast("Survey unavailable", "The post-visit survey is not configured yet.");
            return;
        }

        // Save and upload session data to webhook while app is active in foreground
        string modeStr = currentMode.ToString();
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        SaveAndUploadSessionData($"session_{modeStr}_{timestamp}.json");

        string surveyUrl = surveyFormBaseUrl;
        if (userConsentGDPR)
        {
            float totalMinutes = (Time.time - sessionStartTime) / 60f;
            surveyUrl = $"{surveyFormBaseUrl}?uid={AnonymousUserID}&mode={modeStr}&duration={totalMinutes:F1}";
        }
        Debug.Log($"[ThesisManager] Opening post-visit evaluation survey: {surveyUrl}");
        Application.OpenURL(surveyUrl);
    }

    private void SaveSessionOnExit()
    {
        if (currentSession != null)
        {
            string modeStr = currentMode.ToString();
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            SaveSessionDataLocallyOnly($"session_{modeStr}_{timestamp}.json");
        }
    }

    // On Android, OnApplicationQuit is unreliable; OnApplicationPause(true) fires when the OS sends the app to background
    void OnApplicationPause(bool paused)
    {
        if (paused) SaveSessionOnExit();
    }

    void OnDestroy()
    {
        if (wayfindingManager != null)
        {
            wayfindingManager.OnMemorialReached -= HandleMemorialReached;
        }

    }

    public void ResolveGuidanceInstances()
    {
        if (personalGuidanceInstance == null)
        {
            personalGuidanceInstance = PersonalGuidance.Instance ?? UnityEngine.Object.FindAnyObjectByType<PersonalGuidance>(FindObjectsInactive.Include);
            if (personalGuidanceInstance == null)
            {
                GameObject persGo = GameObject.Find("GuidanceSystem_Personal");
                if (persGo == null)
                {
                    persGo = new GameObject("GuidanceSystem_Personal");
                }
                personalGuidanceInstance = persGo.GetComponent<PersonalGuidance>() ?? persGo.AddComponent<PersonalGuidance>();
            }
        }
        if (intermediateGuidanceInstance == null)
        {
            intermediateGuidanceInstance = IntermediateGuidance.Instance ?? UnityEngine.Object.FindAnyObjectByType<IntermediateGuidance>(FindObjectsInactive.Include);
            if (intermediateGuidanceInstance == null)
            {
                GameObject interGo = GameObject.Find("AR_Core_Controller/GuidanceSystem_Intermediate") ?? GameObject.Find("GuidanceSystem_Intermediate");
                if (interGo == null)
                {
                    GameObject parentContainer = GameObject.Find("AR_Core_Controller") ?? GameObject.Find("_Managers");
                    interGo = new GameObject("GuidanceSystem_Intermediate");
                    if (parentContainer != null) interGo.transform.SetParent(parentContainer.transform, false);
                }
                intermediateGuidanceInstance = interGo.GetComponent<IntermediateGuidance>() ?? interGo.AddComponent<IntermediateGuidance>();
            }
        }
        if (impersonalGuidanceInstance == null)
        {
            impersonalGuidanceInstance = UnityEngine.Object.FindAnyObjectByType<ImpersonalGuidance>(FindObjectsInactive.Include);
            if (impersonalGuidanceInstance == null)
            {
                GameObject impGo = GameObject.Find("GuidanceSystem_Impersonal");
                if (impGo == null)
                {
                    impGo = new GameObject("GuidanceSystem_Impersonal");
                }
                impersonalGuidanceInstance = impGo.GetComponent<ImpersonalGuidance>() ?? impGo.AddComponent<ImpersonalGuidance>();
            }
        }

        if (guideAvatarInstance == null && guideAvatarPrefab != null)
        {
            var existing = GameObject.Find("SingleGuideAvatarInstance") ?? GameObject.Find("GuideCharacterInstance");
            if (existing != null)
            {
                guideAvatarInstance = existing;
            }
            else if (Application.isPlaying)
            {
                guideAvatarInstance = Instantiate(guideAvatarPrefab, Vector3.zero, Quaternion.identity, null);
                guideAvatarInstance.name = "SingleGuideAvatarInstance";
                guideAvatarInstance.SetActive(false);
                Debug.Log("[ThesisManager] Guaranteed Single Persistent Avatar instantiated at boot.");
            }
        }

        if (guideAvatarInstance != null)
        {
            ActiveGuideAvatarRegistry.RegisterSingleAvatarInstance(guideAvatarInstance);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveGuidanceInstances();
    }
#endif
}






