using UnityEngine;

/// <summary>
/// Runtime controller attached to the UI Panel. Handles dynamic loading,
/// playback orchestration, and Play/Pause toggle logic for the audio files.
/// </summary>
public class PopupAudioController : MonoBehaviour
{
    [Header("🔊 Audio Output Component")]
    [SerializeField] private AudioSource interfaceAudioSource;

    private string currentlyLoadedStoneID = "";
    private AppLanguage currentlyLoadedLanguage = AppLanguage.EN;
    private AudioClip cachedClip;
    private bool buttonComponentsResolved = false;
    private bool playbackPaused;

    public bool IsPlaying => interfaceAudioSource != null && interfaceAudioSource.isPlaying;
    public bool IsPlaybackActive => IsPlaying || playbackPaused;

    [Header("🎨 UI Play/Pause Visual Components")]
    [SerializeField] private UnityEngine.UI.Image playAudioButtonImage;
    [SerializeField] private TMPro.TextMeshProUGUI playAudioButtonText;
    [SerializeField] private Sprite whitePlaySprite;
    [SerializeField] private Sprite whitePauseSprite;

    void Awake()
    {
        // Se non viene assegnato manualmente, ne iniettiamo uno di sicurezza
        EnsurePersistentAudioSource();

        if (whitePlaySprite == null) whitePlaySprite = LoadSpriteFromResources("UI/Icons/White Play");
        if (whitePauseSprite == null) whitePauseSprite = LoadSpriteFromResources("UI/Icons/White Pause");
    }

    private void EnsurePersistentAudioSource()
    {
        GameObject host = GameObject.Find("ThesisAR_PopupAudioHost");
        if (host == null)
        {
            host = new GameObject("ThesisAR_PopupAudioHost");
            DontDestroyOnLoad(host);
        }

        AudioSource hostSource = host.GetComponent<AudioSource>();
        if (hostSource == null)
            hostSource = host.AddComponent<AudioSource>();

        interfaceAudioSource = hostSource;
        interfaceAudioSource.playOnAwake = false;
        interfaceAudioSource.spatialBlend = 0f;
    }

    void Start()
    {
        ResolveButtonComponents();
    }

    private static Sprite LoadSpriteFromResources(string path)
    {
        Sprite s = Resources.Load<Sprite>(path);
        if (s != null) return s;

        Texture2D tex = Resources.Load<Texture2D>(path);
        if (tex != null)
        {
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        return null;
    }

    void Update()
    {
        if (!buttonComponentsResolved)
        {
            ResolveButtonComponents();
        }
        UpdateUIVisualState();
    }

    private void ResolveButtonComponents()
    {
        if (playAudioButtonImage != null)
        {
            buttonComponentsResolved = true;
            return;
        }

        GameObject btnGo = null;
        Transform target = transform;
        while (target != null && btnGo == null)
        {
            Transform bar = target.Find("Bottom_Action_Bar") ?? target.Find("Person_Info_Area/Bottom_Action_Bar");
            if (bar == null && target.parent != null)
            {
                bar = target.parent.Find("Bottom_Action_Bar");
            }

            if (bar != null)
            {
                foreach (Transform child in bar)
                {
                    string n = child.name.ToLower();
                    if (n.Contains("play") || n.Contains("audio") || n.Contains("playback"))
                    {
                        btnGo = child.gameObject;
                        break;
                    }
                }
                if (btnGo == null && bar.childCount >= 3)
                {
                    btnGo = bar.GetChild(2).gameObject;
                }
            }

            if (btnGo == null)
            {
                foreach (var b in target.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                {
                    string n = b.name.ToLower();
                    if (n.Contains("playaudio") || n.Contains("playback") || n.Contains("play_audio") || n.Contains("audioplay"))
                    {
                        btnGo = b.gameObject;
                        break;
                    }
                }
            }
            target = target.parent;
        }

        if (btnGo != null)
        {
            playAudioButtonImage = btnGo.GetComponent<UnityEngine.UI.Image>() ?? btnGo.GetComponentInChildren<UnityEngine.UI.Image>();
            playAudioButtonText = btnGo.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            buttonComponentsResolved = (playAudioButtonImage != null);
        }
    }

    private void UpdateUIVisualState()
    {
        if (interfaceAudioSource == null) return;

        bool isPlaying = interfaceAudioSource.isPlaying;

        if (playAudioButtonImage != null)
        {
            if (isPlaying && whitePauseSprite != null)
            {
                playAudioButtonImage.sprite = whitePauseSprite;
            }
            else if (whitePlaySprite != null)
            {
                playAudioButtonImage.sprite = whitePlaySprite;
            }
        }
    }

    /// <summary>
    /// Core toggle executor function triggered directly by the UI Play Button component.
    /// Handles execution loops: Play, Pause, Unpause, or Switch to new asset.
    /// </summary>
    public void ToggleAudioPlayback(string stoneID)
    {
        if (string.IsNullOrEmpty(stoneID)) return;

        AppLanguage requestedLanguage = PlayerPrefs.GetString("Thesis_Language", "english").ToAppLanguage();

        bool sameStoneAndLanguage = currentlyLoadedStoneID == stoneID
                                     && currentlyLoadedLanguage == requestedLanguage
                                     && interfaceAudioSource.clip != null;

        if (sameStoneAndLanguage)
        {
            if (interfaceAudioSource.isPlaying)
            {
                interfaceAudioSource.Pause();
                playbackPaused = true;
                Debug.Log($"[Audio Runtime] Audio clip for stone {stoneID} paused.");
            }
            else if (playbackPaused)
            {
                interfaceAudioSource.UnPause();
                playbackPaused = false;
                Debug.Log($"[Audio Runtime] Audio clip for stone {stoneID} unpaused.");
            }
            else if (NarrationManager.Instance != null)
            {
                NarrationManager.Instance.PlayNarration(stoneID, interfaceAudioSource, force2DAudio: true);
                Debug.Log($"[Audio Runtime] Restarted stone narration for '{stoneID}'.");
            }
            return;
        }

        currentlyLoadedStoneID = stoneID;
        currentlyLoadedLanguage = requestedLanguage;
        playbackPaused = false;

        if (NarrationManager.Instance != null)
        {
            NarrationManager.Instance.PlayNarration(stoneID, interfaceAudioSource, force2DAudio: true);
            Debug.Log($"[Audio Runtime] Delegates stone narration for '{stoneID}' ({requestedLanguage}) to NarrationManager with synchronized subtitles.");
        }
        else
        {
            NarrationManager.StopAllPlaybackGlobal();
            interfaceAudioSource.Stop();

            string langSuffix = requestedLanguage.ToFileSuffix();

            AudioClip clip = Resources.Load<AudioClip>($"GuidanceAudio/{stoneID}_{langSuffix}")
                          ?? Resources.Load<AudioClip>($"GuidanceAudio/{stoneID}_EN")
                          ?? Resources.Load<AudioClip>($"GuidanceAudio/{stoneID}");

            if (clip != null)
            {
                interfaceAudioSource.clip = clip;
                interfaceAudioSource.Play();
                Debug.Log($"[Audio Runtime] Fallback audio playback started for stone: {stoneID}");
            }
            else
            {
                Debug.LogWarning($"[Audio Runtime] No audio record file named '{stoneID}' found.");
                if (playAudioButtonText != null) playAudioButtonText.text = "Audio not available";
                UIManager uiMgr = UnityEngine.Object.FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
                if (uiMgr != null) uiMgr.ShowNotificationToast("Audio Unavailable", $"No recorded narration clip found for '{stoneID}'.");
            }
        }
    }

    /// <summary>
    /// Call this when the user closes the popup window panel to kill the audio stream instantly.
    /// </summary>
    public void ForceStopPlayback()
    {
        if (interfaceAudioSource != null) interfaceAudioSource.Stop();
        currentlyLoadedStoneID = "";
        currentlyLoadedLanguage = AppLanguage.EN;
        playbackPaused = false;
    }

    public void PlayNarration(string narrationID)
    {
        if (string.IsNullOrEmpty(narrationID) || NarrationManager.Instance == null)
            return;

        playbackPaused = false;
        NarrationManager.Instance.PlayNarration(narrationID, interfaceAudioSource, force2DAudio: true);
    }

    public void PlayClipDirectly(AudioClip clip)
    {
        if (interfaceAudioSource != null && clip != null)
        {
            interfaceAudioSource.Stop();
            interfaceAudioSource.clip = clip;
            interfaceAudioSource.Play();
            currentlyLoadedStoneID = "direct_playback";
        }
    }
}
