using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Always-available "Site History" library: a dropdown list of general, non-location-specific
/// chapters (Global_Walking content), independent from the hologram/avatar systems entirely —
/// available in all three guidance conditions, since it isn't tied to proximity to anything.
///
/// Deliberately separate from AskMoreButtonController: mixing "about this specific stone" and
/// "general site history" in the same menu would confuse what a visitor is about to hear.
///
/// Chapter order in site_history_manifest.json is chronological (camp origins → liberation →
/// trials), read top to bottom like a table of contents.
/// </summary>
public class SiteHistoryDropdownController : MonoBehaviour
{
    [System.Serializable]
    private class ChapterEntry
    {
        public string id;
        public string titleEN;
        public string titleDE;
    }

    [System.Serializable]
    private class ChapterManifestWrapper
    {
        public ChapterEntry[] entries;
    }

    [Header("📖 Data")]
    [Tooltip("site_history_manifest.json placed under a Resources folder, assigned here as a TextAsset.")]
    [SerializeField] private TextAsset manifestJson;

    [Header("🖱️ Entry Point")]
    [SerializeField] private Button openButton;   // The persistent "📖 Site History" icon
    [SerializeField] private Button closeButton;

    [Header("📋 Dropdown Panel")]
    [SerializeField] private GameObject dropdownPanel; // Parent panel, inactive by default
    [SerializeField] private Transform chapterListContainer;
    [SerializeField] private GameObject chapterButtonPrefab; // Button + a child TextMeshProUGUI

    [Header("🔊 Playback")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private TextMeshProUGUI nowPlayingText;
    [SerializeField] private TextMeshProUGUI subtitleText;
    [SerializeField] private Slider progressBarSlider;

    [Header("🌐 Language")]
    [SerializeField] private AppLanguage currentLanguage = AppLanguage.EN;

    private struct ChapterButtonEntryUI
    {
        public GameObject root;
        public Button button;
        public TextMeshProUGUI titleText;
        public Image iconImage;
    }

    private List<ChapterEntry> chapters = new List<ChapterEntry>();
    private readonly List<GameObject> spawnedButtons = new List<GameObject>();
    private readonly List<ChapterButtonEntryUI> cachedChapterButtons = new List<ChapterButtonEntryUI>();
    private Sprite cachedPlaySprite;
    private Sprite cachedPauseSprite;
    private string currentlyLoadedChapterId = "";
    private Coroutine playbackRoutine = null;

    private void Awake()
    {
        cachedPlaySprite = LoadSpriteFromResources("UI/Icons/White Play");
        cachedPauseSprite = LoadSpriteFromResources("UI/Icons/White Pause");

        if (nowPlayingText != null) nowPlayingText.text = "";
        if (subtitleText != null) subtitleText.text = "";

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>() ?? Object.FindAnyObjectByType<AudioSource>(FindObjectsInactive.Include);
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        if (chapterButtonPrefab != null && chapterButtonPrefab.scene.IsValid())
        {
            chapterButtonPrefab.SetActive(false);
        }

        ParseManifest();
        BuildChapterButtons();

        if (openButton != null) openButton.onClick.AddListener(OpenDropdown);
        if (closeButton != null) closeButton.onClick.AddListener(CloseDropdown);

        if (dropdownPanel != null) dropdownPanel.SetActive(false);
    }

    private void ParseManifest()
    {
        chapters.Clear();
        if (manifestJson == null)
        {
            manifestJson = Resources.Load<TextAsset>("site_history_manifest") ?? Resources.Load<TextAsset>("DialogueAssets/site_history_manifest");
        }

        if (manifestJson == null)
        {
            Debug.LogWarning("[SiteHistoryDropdownController] site_history_manifest.json not found in Resources — the Site History list will be empty.");
            return;
        }

        try
        {
            ChapterManifestWrapper wrapper = JsonUtility.FromJson<ChapterManifestWrapper>(manifestJson.text);
            if (wrapper != null && wrapper.entries != null)
            {
                chapters.AddRange(wrapper.entries);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SiteHistoryDropdownController] Error parsing site_history_manifest: {ex.Message}");
        }
    }

    private void BuildChapterButtons()
    {
        // Clean up previously spawned buttons, being careful NEVER to destroy the template prefab
        foreach (var oldBtn in spawnedButtons)
        {
            if (oldBtn != null && oldBtn != chapterButtonPrefab)
            {
                Destroy(oldBtn);
            }
        }
        spawnedButtons.Clear();
        cachedChapterButtons.Clear();

        if (chapterButtonPrefab != null && chapterButtonPrefab.scene.IsValid())
        {
            chapterButtonPrefab.SetActive(false);
        }

        if (chapterListContainer == null)
        {
            var scrollRect = GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);
            chapterListContainer = scrollRect != null ? scrollRect.content : null;

            if (chapterListContainer == null)
            {
                chapterListContainer = transform.Find("Scroll View/Viewport/Content") ??
                                       transform.Find("Viewport/Content") ??
                                       transform.Find("Content");
            }
        }

        if (chapterListContainer == null) return;

        // Deactivate pre-existing template children inside chapterListContainer so they don't cover generated buttons
        foreach (Transform child in chapterListContainer)
        {
            if (child != null && (chapterButtonPrefab == null || child.gameObject != chapterButtonPrefab))
            {
                child.gameObject.SetActive(false);
            }
        }

        // Ensure vertical layout group on chapterListContainer
        var containerLayout = chapterListContainer.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        if (containerLayout == null)
        {
            containerLayout = chapterListContainer.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            containerLayout.padding = new RectOffset(10, 10, 10, 10);
            containerLayout.spacing = 8f;
            containerLayout.childControlWidth = true;
            containerLayout.childControlHeight = false;
            containerLayout.childForceExpandWidth = true;
            containerLayout.childForceExpandHeight = false;
        }

        var containerFitter = chapterListContainer.GetComponent<UnityEngine.UI.ContentSizeFitter>();
        if (containerFitter == null)
        {
            containerFitter = chapterListContainer.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            containerFitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        }

        foreach (ChapterEntry chapter in chapters)
        {
            GameObject buttonGO;
            if (chapterButtonPrefab != null)
            {
                buttonGO = Instantiate(chapterButtonPrefab, chapterListContainer);
            }
            else
            {
                buttonGO = new GameObject($"Btn_{chapter.id}", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button), typeof(UnityEngine.UI.LayoutElement));
                buttonGO.transform.SetParent(chapterListContainer, false);

                var btnImg = buttonGO.GetComponent<UnityEngine.UI.Image>();
                if (btnImg != null) btnImg.color = new Color(0.14f, 0.18f, 0.24f, 0.95f);

                var le = buttonGO.GetComponent<UnityEngine.UI.LayoutElement>();
                le.minHeight = 48f;
                le.preferredHeight = 54f;
                le.flexibleWidth = 1f;

                GameObject labelGo = new GameObject("Text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
                labelGo.transform.SetParent(buttonGO.transform, false);
                var labelTxt = labelGo.GetComponent<TMPro.TextMeshProUGUI>();
                labelTxt.alignment = TMPro.TextAlignmentOptions.Center;
                labelTxt.fontSize = 24f;
                labelTxt.color = Color.white;
            }

            // Ensure button and text objects are active
            buttonGO.SetActive(true);

            TextMeshProUGUI label = buttonGO.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.gameObject.SetActive(true);
                label.text = GetTitleFor(chapter);
            }

            // Only apply programmatic fallback styling if NO custom prefab was provided
            if (chapterButtonPrefab == null)
            {
                UnityEngine.UI.Image[] allImages = buttonGO.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                foreach (var img in allImages)
                {
                    if (img != null && (img.sprite == null || img.color == Color.white))
                    {
                        img.color = new Color(0.14f, 0.18f, 0.24f, 0.95f);
                    }
                }

                if (label != null)
                {
                    label.color = Color.white;
                    label.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
                    label.margin = new Vector4(12, 0, 12, 0);
                }
            }

            string capturedId = chapter.id;
            ChapterEntry capturedChapter = chapter;

            Button playBtn = null;
            Transform playTransform = buttonGO.transform.Find("PlayButton") ??
                                       buttonGO.transform.Find("Play") ??
                                       buttonGO.transform.Find("Btn_Play") ??
                                       buttonGO.transform.Find("Icon_Play");

            if (playTransform != null) playBtn = playTransform.GetComponent<Button>();
            if (playBtn == null)
            {
                Button[] childBtns = buttonGO.GetComponentsInChildren<Button>(true);
                foreach (var b in childBtns)
                {
                    if (b.gameObject != buttonGO)
                    {
                        playBtn = b;
                        break;
                    }
                }
            }

            if (playBtn != null)
            {
                playBtn.onClick.RemoveAllListeners();
                playBtn.onClick.AddListener(() => PlayChapter(capturedId));
            }

            Button rowBtn = buttonGO.GetComponent<Button>();
            if (rowBtn != null && rowBtn != playBtn)
            {
                rowBtn.onClick.RemoveAllListeners();
                rowBtn.onClick.AddListener(() => DisplayChapterTextOnly(capturedChapter));
            }
            else if (playBtn == null && rowBtn != null)
            {
                rowBtn.onClick.RemoveAllListeners();
                rowBtn.onClick.AddListener(() => PlayChapter(capturedId));
            }

            // Cache image and components at instantiation time
            Image iconImg = null;
            if (playTransform != null) iconImg = playTransform.GetComponent<Image>() ?? playTransform.GetComponentInChildren<Image>();
            if (iconImg == null)
            {
                Image[] childImgs = buttonGO.GetComponentsInChildren<Image>(true);
                foreach (var img in childImgs)
                {
                    if (img.gameObject != buttonGO) { iconImg = img; break; }
                }
                if (iconImg == null) iconImg = buttonGO.GetComponent<Image>();
            }

            spawnedButtons.Add(buttonGO);
            cachedChapterButtons.Add(new ChapterButtonEntryUI
            {
                root = buttonGO,
                button = rowBtn ?? playBtn ?? buttonGO.GetComponent<Button>(),
                titleText = label,
                iconImage = iconImg
            });
        }

        var rt = chapterListContainer as RectTransform;
        if (rt != null)
        {
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }
    }

    private string GetTitleFor(ChapterEntry chapter)
    {
        if (chapter == null) return string.Empty;
        return currentLanguage == AppLanguage.DE ? chapter.titleDE : chapter.titleEN;
    }

    /// <summary>Call when the app-wide language changes (EN/DE/HE toggle in Settings).</summary>
    public void SetLanguage(AppLanguage language)
    {
        currentLanguage = language;

        for (int i = 0; i < chapters.Count && i < cachedChapterButtons.Count; i++)
        {
            if (cachedChapterButtons[i].titleText != null)
                cachedChapterButtons[i].titleText.text = GetTitleFor(chapters[i]);
        }

        if (audioSource != null && audioSource.isPlaying)
        {
            StopPlayback();
        }
    }

    public bool IsDropdownOpen() => dropdownPanel != null && dropdownPanel.activeSelf;

    public void OpenDropdown()
    {
        var uiManager = Object.FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
        if (uiManager != null)
        {
            uiManager.CloseSidebar();
            if (dropdownPanel != null)
            {
                uiManager.OpenPanel(dropdownPanel);
            }
        }
        else if (dropdownPanel != null)
        {
            dropdownPanel.SetActive(true);
        }
        Debug.Log("[UI Event Trace] SiteHistoryPanel OPENED");
    }

    public void CloseDropdown()
    {
        var uiManager = Object.FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
        if (uiManager != null)
        {
            uiManager.ClosePanelAndReturn(dropdownPanel);
        }
        else if (dropdownPanel != null)
        {
            dropdownPanel.SetActive(false);
        }
        Debug.Log("[UI Event Trace] SiteHistoryPanel CLOSED");
        // Deliberately does NOT stop playback on close — like a podcast player, listening can
        // continue in the background while the visitor keeps exploring.
    }

    private void DisplayChapterTextOnly(ChapterEntry chapter)
    {
        if (chapter == null) return;
        ChapterEntry chapterMeta = chapters.Find(c => c.id == chapter.id);
        if (nowPlayingText != null && chapterMeta != null)
        if (nowPlayingText != null)
        {
            nowPlayingText.text = GetTitleFor(chapterMeta);
        }

        string langSuffix = currentLanguage.ToFileSuffix();
        string assetPath = $"DialogueAssets/Generated/{chapter.id}_{langSuffix}";
        DialogueSequence seq = Resources.Load<DialogueSequence>(assetPath) ?? Resources.Load<DialogueSequence>($"DialogueAssets/Generated/{chapter.id}");

        if (seq != null && seq.dialogueLines != null && seq.dialogueLines.Count > 0)
        {
            var localizedLine = seq.dialogueLines[0].GetLine(currentLanguage);
            if (subtitleText != null) subtitleText.text = localizedLine.subtitleText;
        }
    }

    /// <summary>
    /// Same toggle pattern as PopupAudioController: tapping the currently-playing chapter again
    /// pauses/resumes it; tapping a different chapter switches to it.
    /// </summary>
    private void PlayChapter(string chapterId)
    {
        if (chapterId == currentlyLoadedChapterId && audioSource != null && audioSource.clip != null)
        {
            if (audioSource.isPlaying)
            {
                audioSource.Pause();
            }
            else
            {
                audioSource.UnPause();
            }
            UpdateChapterButtonVisuals();
            return;
        }

        // Global Audio Guardian: Stop all active audio across the entire application before starting a NEW chapter
        NarrationManager.StopAllPlaybackGlobal();

        if (playbackRoutine != null)
        {
            StopCoroutine(playbackRoutine);
            playbackRoutine = null;
        }

        string langSuffix = currentLanguage.ToFileSuffix();
        string assetPath = $"DialogueAssets/Generated/{chapterId}_{langSuffix}";
        DialogueSequence seq = Resources.Load<DialogueSequence>(assetPath) ?? Resources.Load<DialogueSequence>($"DialogueAssets/Generated/{chapterId}");

        if (seq == null || seq.dialogueLines == null || seq.dialogueLines.Count == 0)
        {
            Debug.LogWarning($"[SiteHistoryDropdownController] Missing generated DialogueSequence asset at Resources/{assetPath} — checking fallback clip.");
            AudioClip directClip = Resources.Load<AudioClip>($"GuidanceAudio/{chapterId}_{langSuffix}") ??
                                   Resources.Load<AudioClip>($"GuidanceAudio/Walking_{chapterId}_{langSuffix}") ??
                                   Resources.Load<AudioClip>($"GuidanceAudio/Area_{chapterId}_{langSuffix}") ??
                                   Resources.Load<AudioClip>($"GuidanceAudio/{chapterId}");

            if (directClip != null && audioSource != null)
            {
                audioSource.Stop();
                audioSource.clip = directClip;
                audioSource.volume = 1f;
                audioSource.spatialBlend = 0f;
                audioSource.Play();
                currentlyLoadedChapterId = chapterId;
                UpdateChapterButtonVisuals();

                if (NarrationManager.Instance != null)
                {
                    NarrationManager.Instance.PlayNarration(chapterId, audioSource);
                }

                ChapterEntry directMeta = chapters.Find(c => c.id == chapterId);
                playbackRoutine = StartCoroutine(DirectClipProgressRoutine(directClip, directMeta));
                return;
            }
            return;
        }

        currentlyLoadedChapterId = chapterId;
        UpdateChapterButtonVisuals();

        ChapterEntry chapterMeta = chapters.Find(c => c.id == chapterId);
        if (nowPlayingText != null && chapterMeta != null)
        {
            nowPlayingText.text = GetTitleFor(chapterMeta);
        }

        playbackRoutine = StartCoroutine(PlaySequenceCoroutine(seq, chapterId, langSuffix));
    }

    private System.Collections.IEnumerator PlaySequenceCoroutine(DialogueSequence seq, string chapterId, string langSuffix)
    {
        ChapterEntry chapterMeta = chapters.Find(c => c.id == chapterId);

        if (audioSource != null)
        {
            audioSource.spatialBlend = 0f; // Force 2D stereo playback for UI chapters
            audioSource.volume = 1f;
        }

        for (int i = 0; i < seq.dialogueLines.Count; i++)
        {
            var line = seq.dialogueLines[i];
            AudioClip clipToPlay = line.voiceClip;

            if (clipToPlay == null)
            {
                clipToPlay = Resources.Load<AudioClip>($"GuidanceAudio/{chapterId}_{langSuffix}") ??
                             Resources.Load<AudioClip>($"GuidanceAudio/Walking_{chapterId}_{langSuffix}") ??
                             Resources.Load<AudioClip>($"GuidanceAudio/Area_{chapterId}_{langSuffix}") ??
                             Resources.Load<AudioClip>($"GuidanceAudio/{chapterId}");
            }

            if (clipToPlay != null && audioSource != null)
            {
                audioSource.Stop();
                audioSource.clip = clipToPlay;
                audioSource.volume = 1f;
                audioSource.spatialBlend = 0f;
                audioSource.Play();
            }

            if (subtitleText != null && !string.IsNullOrEmpty(line.subtitleText))
            {
                subtitleText.text = line.subtitleText;
            }

            float duration = (clipToPlay != null && clipToPlay.length > 0f) ? clipToPlay.length : (line.duration > 0f ? line.duration : 10f);

            while (audioSource != null && (audioSource.isPlaying || audioSource.time > 0))
            {
                float timePos = audioSource.time;
                if (nowPlayingText != null && chapterMeta != null)
                {
                    nowPlayingText.text = $"{GetTitleFor(chapterMeta)}  ({FormatTime(timePos)} / {FormatTime(duration)})";
                }
                if (progressBarSlider != null && duration > 0f)
                {
                    progressBarSlider.value = Mathf.Clamp01(timePos / duration);
                }

                if (timePos >= duration - 0.05f) break;
                yield return null;
            }

            yield return new WaitForSeconds(0.3f);
        }

        StopPlayback();
    }

    private System.Collections.IEnumerator DirectClipProgressRoutine(AudioClip clip, ChapterEntry meta)
    {
        float duration = (clip != null && clip.length > 0f) ? clip.length : 10f;

        while (audioSource != null && (audioSource.isPlaying || audioSource.time > 0))
        {
            float timePos = audioSource.time;
            if (nowPlayingText != null && meta != null)
            {
                nowPlayingText.text = $"{GetTitleFor(meta)}  ({FormatTime(timePos)} / {FormatTime(duration)})";
            }
            if (progressBarSlider != null && duration > 0f)
            {
                progressBarSlider.value = Mathf.Clamp01(timePos / duration);
            }

            if (timePos >= duration - 0.05f) break;
            yield return null;
        }

        StopPlayback();
    }

    private string FormatTime(float seconds)
    {
        int mins = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        return $"{mins:D2}:{secs:D2}";
    }

    public void StopPlayback()
    {
        if (playbackRoutine != null)
        {
            StopCoroutine(playbackRoutine);
            playbackRoutine = null;
        }

        if (audioSource != null) audioSource.Stop();
        currentlyLoadedChapterId = "";
        UpdateChapterButtonVisuals();

        if (nowPlayingText != null) nowPlayingText.text = "";
        if (subtitleText != null) subtitleText.text = "";
    }

    private void Update()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            UpdateChapterButtonVisuals();
        }
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

    private void UpdateChapterButtonVisuals()
    {
        if (cachedPlaySprite == null) cachedPlaySprite = LoadSpriteFromResources("UI/Icons/White Play");
        if (cachedPauseSprite == null) cachedPauseSprite = LoadSpriteFromResources("UI/Icons/White Pause");

        for (int i = 0; i < chapters.Count && i < cachedChapterButtons.Count; i++)
        {
            var entry = cachedChapterButtons[i];
            bool isCurrent = chapters[i].id == currentlyLoadedChapterId;
            bool isPlayingCurrent = isCurrent && audioSource != null && audioSource.isPlaying;

            if (entry.titleText != null)
            {
                entry.titleText.fontStyle = isCurrent ? FontStyles.Bold : FontStyles.Normal;
            }

            if (entry.iconImage != null)
            {
                if (isPlayingCurrent && cachedPauseSprite != null)
                {
                    entry.iconImage.sprite = cachedPauseSprite;
                }
                else if (cachedPlaySprite != null)
                {
                    entry.iconImage.sprite = cachedPlaySprite;
                }
            }

            if (entry.button != null && chapterButtonPrefab == null)
            {
                var colors = entry.button.colors;
                if (isCurrent)
                {
                    colors.normalColor = new Color(0.61f, 0.34f, 0.82f, 0.9f);
                    colors.selectedColor = new Color(0.61f, 0.34f, 0.82f, 1f);
                }
                else
                {
                    colors.normalColor = Color.white;
                    colors.selectedColor = Color.white;
                }
                entry.button.colors = colors;
            }
        }
    }
}
