using System.Collections;
using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Centralized manager for handling spoken guidance audio and subtitle rendering.
/// Decouples dialogue loading, localization, spatial audio 3D configuration, 
/// and automatic subtitle clearing from the physical guidance avatars.
/// </summary>
public class NarrationManager : MonoBehaviour
{
    public static NarrationManager Instance { get; private set; }

    private UIManager uiManager;
    private MemorialDataManager dataManager;
    private AudioSource activeAudioSource;
    private Coroutine activeSubtitleTimer;
    private float currentPlaybackSpeed = 1.0f;

    private Dictionary<string, string> subtitleCache;

    public System.Action OnNarrationFinished;

    public bool IsSpeaking
    {
        get
        {
            return activeAudioSource != null && activeAudioSource.isPlaying;
        }
    }

    public AudioSource CurrentAudioSource => activeAudioSource;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        ResolveReferences();
        BuildSubtitleCache();
    }

    private void ResolveReferences()
    {
        if (uiManager == null)
            uiManager = FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
        if (dataManager == null)
            dataManager = FindAnyObjectByType<MemorialDataManager>(FindObjectsInactive.Include);
    }

    private void BuildSubtitleCache()
    {
        subtitleCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // The runtime catalog is the authoritative transcript for regenerated
        // clips. Load it first and keep legacy files only as a fallback.
        LoadSubtitleCatalog(Resources.Load<TextAsset>("audio_runtime_catalog"), false, "audio_runtime_catalog");

        TextAsset configAsset = Resources.Load<TextAsset>("dialogues_config") ??
                               Resources.Load<TextAsset>("DialogueAssets/dialogues_config") ??
                               Resources.Load<TextAsset>("GuidanceAudio/dialogues_config");

        if (configAsset != null)
        {
            LoadSubtitleCatalog(configAsset, true, "dialogues_config");
        }
        else
        {
            Debug.LogWarning("[NarrationManager] No dialogues_config.json found in any Resources folder.");
        }

        Debug.Log($"[NarrationManager] Subtitle cache built with {subtitleCache.Count} normalized entries.");
    }

    private void LoadSubtitleCatalog(TextAsset catalogAsset, bool addNormalizedVariants, string label)
    {
        if (catalogAsset == null) return;

        try
        {
            var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<DialogueConfigData>(catalogAsset.text);
            if (parsed?.dialogues == null) return;

            foreach (var entry in parsed.dialogues)
            {
                if (string.IsNullOrEmpty(entry.id) || string.IsNullOrEmpty(entry.text)) continue;
                AddSubtitleEntry(entry.id, entry.text);
                if (addNormalizedVariants)
                    AddNormalizedKeyVariants(entry.id, entry.text);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[NarrationManager] Failed to parse {label}: {ex.Message}");
        }
    }

    // Kept as a method so the legacy block remains removable without changing
    // active playback behavior. Raw inscriptions are display-only content.
    private static bool ShouldUseLegacyInscriptionFallback() => false;

    private void AddSubtitleEntry(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value)) return;
        if (!subtitleCache.ContainsKey(key))
        {
            subtitleCache[key] = value;
        }
    }

    private void AddNormalizedKeyVariants(string rawKey, string value)
    {
        string trimmed = rawKey.Trim();
        string withoutLanguage = StripLanguageSuffix(trimmed);
        string clean = withoutLanguage.Replace("stone_", string.Empty).Replace("Stone ", string.Empty).Trim();

        AddSubtitleEntry(withoutLanguage, value);   // WELCOME_INTERMEDIATE
        AddSubtitleEntry(clean, value);             // A3 / WELCOME_INTERMEDIATE

        int firstUnderscore = clean.IndexOf('_');
        if (firstUnderscore > 0)
        {
            AddSubtitleEntry(clean.Substring(0, firstUnderscore), value); // WELCOME
        }
    }

    private static string StripLanguageSuffix(string key)
    {
        if (key.EndsWith("_EN", StringComparison.OrdinalIgnoreCase) ||
            key.EndsWith("_DE", StringComparison.OrdinalIgnoreCase) ||
            key.EndsWith("_HE", StringComparison.OrdinalIgnoreCase))
        {
            return key.Substring(0, key.Length - 3);
        }
        return key;
    }

    public void SetPlaybackSpeed(float speed)
    {
        currentPlaybackSpeed = Mathf.Clamp(speed, 0.8f, 1.2f);
        if (activeAudioSource != null && activeAudioSource.isPlaying)
        {
            activeAudioSource.pitch = currentPlaybackSpeed;
        }
    }

    public void PlayNarration(string id, AudioSource source = null, bool force2DAudio = false)
    {
        StopCurrentNarration();

        // Lazy-resolve every call — ensures we find UIManager even if it was inactive during Start()
        ResolveReferences();
        if (subtitleCache == null || subtitleCache.Count == 0)
            BuildSubtitleCache();

        this.activeAudioSource = source;
        if (this.activeAudioSource == null)
        {
            this.activeAudioSource = GetComponent<AudioSource>();
            if (this.activeAudioSource == null)
                this.activeAudioSource = gameObject.AddComponent<AudioSource>();
            this.activeAudioSource.spatialBlend = 0f;
        }

        string lang = (uiManager != null) ? uiManager.SelectedLanguage : "english";
        AppLanguage appLang = lang.ToAppLanguage();
        string langSuffix = appLang.ToFileSuffix();

        string cleanID = id.Replace("Stone ", "").Replace("stone_", "").Trim();
        bool hasLangSuffix = id.EndsWith("_EN") || id.EndsWith("_DE") || id.EndsWith("_HE");
        string fullID = hasLangSuffix ? id : $"{id}_{langSuffix}";
        bool cleanHasLangSuffix = cleanID.EndsWith("_EN") || cleanID.EndsWith("_DE") || cleanID.EndsWith("_HE");
        string cleanFullID = cleanHasLangSuffix ? cleanID : $"{cleanID}_{langSuffix}";

        Debug.Log($"[NarrationManager] PlayNarration: id='{id}', fullID='{fullID}', lang='{lang}', uiManager={(uiManager != null ? uiManager.gameObject.name : "NULL")}");

        // --- 1. DialogueSequence ScriptableObject ---
        DialogueSequence seq = Resources.Load<DialogueSequence>($"DialogueAssets/Generated/{fullID}") ??
                               Resources.Load<DialogueSequence>($"DialogueAssets/Generated/{id}") ??
                               Resources.Load<DialogueSequence>($"DialogueAssets/Generated/{cleanFullID}") ??
                               Resources.Load<DialogueSequence>($"DialogueAssets/Generated/{cleanID}");

        AudioClip voiceClip = null;
        string subtitleText = "";
        string catalogSubtitle = FindCatalogSubtitle(fullID, cleanFullID);

        if (seq != null && seq.dialogueLines != null && seq.dialogueLines.Count > 0)
        {
            var line = seq.dialogueLines[0];
            // A direct clip on a legacy sequence must not bypass the reviewed
            // catalog MP3 for this exact runtime ID.
            if (string.IsNullOrEmpty(catalogSubtitle))
                voiceClip = line.voiceClip;
            subtitleText = line.subtitleText;
        }

        // A catalog transcript is generated from the same locked script as the
        // clip, so it intentionally supersedes stale ScriptableObject copy.
        if (!string.IsNullOrEmpty(catalogSubtitle))
            subtitleText = catalogSubtitle;

        // --- 2. Audio clip from GuidanceAudio ---
        if (voiceClip == null)
        {
            voiceClip = Resources.Load<AudioClip>($"GuidanceAudio/{fullID}") ??
                        Resources.Load<AudioClip>($"GuidanceAudio/{id}") ??
                        Resources.Load<AudioClip>($"GuidanceAudio/{cleanFullID}") ??
                        Resources.Load<AudioClip>($"GuidanceAudio/{cleanID}");
        }

        // --- 3. Subtitle from cache ---
        if (string.IsNullOrEmpty(subtitleText))
        {
            string[] candidates = { fullID, id, cleanFullID, cleanID };
            foreach (string candidate in candidates)
            {
                if (subtitleCache != null && subtitleCache.TryGetValue(candidate, out string cachedText))
                {
                    subtitleText = cachedText;
                    break;
                }
            }

            // Inscriptions remain visible in the memorial panel; narration subtitles
            // must come from the selected catalog entry, never from data fallback.
            if (ShouldUseLegacyInscriptionFallback())
            {
                object dataContext = dataManager.GetDataByID(id) ??
                                     dataManager.GetDataByID(cleanID) ??
                                     dataManager.GetDataByID(StripLanguageSuffix(fullID)) ??
                                     dataManager.GetDataByID(StripLanguageSuffix(cleanFullID));
                if (dataContext is MemorialDataManager.MemorialStone stone && stone.persons.Count > 0)
                {
                    var p = stone.persons[0];
                    subtitleText = appLang switch
                    {
                        AppLanguage.DE => p.german_inscription,
                        AppLanguage.HE => p.hebrew_inscription,
                        _ => p.english_inscription
                    };
                }
                else if (dataContext is MemorialDataManager.MassGrave grave)
                {
                    subtitleText = grave.description;
                }
                else if (dataContext is MemorialDataManager.OtherMemorial other)
                {
                    subtitleText = other.description;
                }
            }

            if (string.IsNullOrEmpty(subtitleText))
            {
                if (id.StartsWith("WELCOME_PERSONAL"))
                {
                    subtitleText = appLang switch
                    {
                        AppLanguage.DE => "Willkommen in Bergen-Belsen. Ich bin dein AR-Begleiter.",
                        AppLanguage.HE => "ברוכים הבאים לברגן-בלזן. אני מלווה המציאות המדומה שלך.",
                        _ => "Welcome to Bergen-Belsen. I am your AR companion guide. I will walk ahead of you to lead you to historical memorials."
                    };
                }
                else if (id.StartsWith("WELCOME_INTERMEDIATE"))
                {
                    subtitleText = appLang switch
                    {
                        AppLanguage.DE => "Willkommen in Bergen-Belsen. Tippe auf einen Pin, um ein Hologramm zu starten.",
                        AppLanguage.HE => "ברוכים הבאים לברגן-בלזן. הקישו על סיכה כדי להפעיל הולוגרמה.",
                        _ => "Welcome to Bergen-Belsen. I am your AR guide. Tap any memorial pin to spawn my hologram and hear detailed historical insights."
                    };
                }
                else if (id.StartsWith("WELCOME"))
                {
                    subtitleText = appLang switch
                    {
                        AppLanguage.DE => "Willkommen in der Gedenkstätte Bergen-Belsen.",
                        AppLanguage.HE => "ברוכים הבאים לאתר ההנצחה ברגן-בלזן.",
                        _ => "Welcome to Bergen-Belsen Memorial site. Explore historical memorial stones and mass graves using the map or database."
                    };
                }
            }
        }

        Debug.Log($"[NarrationManager] Resolved: voiceClip={(voiceClip != null ? voiceClip.name : "NULL")}, subtitle={(!string.IsNullOrEmpty(subtitleText) ? subtitleText.Substring(0, Mathf.Min(subtitleText.Length, 50)) + "..." : "EMPTY")}");

        // --- 4. Play audio ---
        if (activeAudioSource != null && voiceClip != null)
        {
            activeAudioSource.Stop();
            activeAudioSource.clip = voiceClip;

            bool isSystemic = force2DAudio || id.StartsWith("WELCOME") || id.StartsWith("GOODBYE") || id.StartsWith("LOGISTICS");
            activeAudioSource.volume = 1f;
            activeAudioSource.spatialBlend = isSystemic ? 0f : 1f;
            activeAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            activeAudioSource.minDistance = 1.5f;
            activeAudioSource.maxDistance = 25f;
            activeAudioSource.spatialize = !isSystemic;
            activeAudioSource.playOnAwake = false;

            activeAudioSource.pitch = currentPlaybackSpeed;
            activeAudioSource.Play();
        }

        // --- 5. Display subtitle ---
        float displayDuration = 4.0f;
        if (voiceClip != null)
        {
            displayDuration = voiceClip.length / currentPlaybackSpeed;
        }
        else if (!string.IsNullOrEmpty(subtitleText))
        {
            displayDuration = Mathf.Max(3.0f, (subtitleText.Length * 0.06f) + 1.5f);
        }

        if (uiManager != null)
        {
            uiManager.DisplayGuideSubtitle(subtitleText);
            activeSubtitleTimer = StartCoroutine(SubtitleTimeoutRoutine(displayDuration));
        }
        else
        {
            Debug.LogError("[NarrationManager] uiManager is NULL! Cannot display subtitles.");
            // Emergency fallback
            uiManager = FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
            if (uiManager != null)
            {
                uiManager.DisplayGuideSubtitle(subtitleText);
                activeSubtitleTimer = StartCoroutine(SubtitleTimeoutRoutine(displayDuration));
            }
        }

        TelemetryLogger.Instance?.OnNarrationStarted(fullID, displayDuration);
    }

    private string FindCatalogSubtitle(string fullID, string cleanFullID)
    {
        if (subtitleCache == null) return string.Empty;
        if (subtitleCache.TryGetValue(fullID, out string subtitle)) return subtitle;
        if (subtitleCache.TryGetValue(cleanFullID, out subtitle)) return subtitle;
        return string.Empty;
    }

    public static void StopAllPlaybackGlobal()
    {
        if (Instance != null)
        {
            Instance.StopCurrentNarration();
        }

        var siteHistory = UnityEngine.Object.FindAnyObjectByType<SiteHistoryDropdownController>(FindObjectsInactive.Include);
        if (siteHistory != null)
        {
            siteHistory.StopPlayback();
        }

        var popupAudio = UnityEngine.Object.FindAnyObjectByType<PopupAudioController>(FindObjectsInactive.Include);
        if (popupAudio != null)
        {
            popupAudio.ForceStopPlayback();
        }

        AudioSource[] allSources = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include);
        foreach (AudioSource src in allSources)
        {
            if (src != null && src.isPlaying)
            {
                src.Stop();
            }
        }
    }

    public void StopCurrentNarration()
    {
        if (activeSubtitleTimer != null)
        {
            StopCoroutine(activeSubtitleTimer);
            activeSubtitleTimer = null;
        }

        if (activeAudioSource != null)
        {
            activeAudioSource.Stop();
            activeAudioSource = null;
        }

        if (uiManager != null)
        {
            uiManager.DisplayGuideSubtitle("");
        }

        TelemetryLogger.Instance?.OnNarrationStopped(null);
    }

    private IEnumerator SubtitleTimeoutRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (uiManager != null)
        {
            uiManager.DisplayGuideSubtitle("");
        }

        OnNarrationFinished?.Invoke();
        activeSubtitleTimer = null;
    }

    [System.Serializable]
    private class DialogueConfigData { public System.Collections.Generic.List<DialogueConfigEntry> dialogues; }
    [System.Serializable]
    private class DialogueConfigEntry { public string id; public string text; }
}
