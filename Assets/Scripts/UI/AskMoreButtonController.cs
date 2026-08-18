using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen-fixed "ask for more" button that appears next to the subtitle whenever the
/// Intermediate hologram is active. If the currently selected stone has no symbol data,
/// tapping it directly replays the bio audio (no fan-out — nothing else to offer). If the
/// stone DOES have one or more symbols (per stone_symbols_map.json), tapping it fans out into
/// two chips: "Repeat" and "Symbol". This component only handles the button/chip UI and raises
/// OnTopicSelected; IntermediateGuidance.cs owns actually fading/playing audio.
/// </summary>
public class AskMoreButtonController : MonoBehaviour
{
    [Serializable]
    public enum Topic { Repeat, Symbol, CampInfo, RandomFacts }

    [Header("🔘 Main Button")]
    [SerializeField] private RectTransform mainButtonRoot;
    [SerializeField] private Button mainButton;

    [Header("🍡 Chip Fan-Out (used when extra topics are available)")]
    [SerializeField] private RectTransform chipContainer;
    [SerializeField] private Button repeatChipButton;
    [SerializeField] private Button symbolChipButton;
    [SerializeField] private Button campInfoChipButton;
    [SerializeField] private Button randomFactsChipButton;
    [SerializeField] private TextMeshProUGUI repeatChipLabel;
    [SerializeField] private TextMeshProUGUI symbolChipLabel;
    [SerializeField] private TextMeshProUGUI campInfoChipLabel;
    [SerializeField] private TextMeshProUGUI randomFactsChipLabel;

    [Header("✨ First-appearance nudge")]
    [SerializeField] private AudioSource nudgeAudioSource;
    [SerializeField] private AudioClip nudgeSound;
    [SerializeField] private int nudgePulseCount = 3;
    [SerializeField] private float nudgePulseScale = 1.08f;
    [SerializeField] private float nudgePulseDuration = 0.25f;

    [Header("🌐 Language")]
    [SerializeField] private AppLanguage currentLanguage = AppLanguage.EN;

    public event Action<Topic> OnTopicSelected;

    private bool hasSymbolTopic = false;
    private bool chipsExpanded = false;
    private Vector3 mainButtonBaseScale;

    private void Awake()
    {
        if (mainButtonRoot != null) mainButtonBaseScale = mainButtonRoot.localScale;

        if (mainButton != null) mainButton.onClick.AddListener(HandleMainButtonPressed);
        if (repeatChipButton != null) repeatChipButton.onClick.AddListener(() => SelectTopic(Topic.Repeat));
        if (symbolChipButton != null) symbolChipButton.onClick.AddListener(() => SelectTopic(Topic.Symbol));
        if (campInfoChipButton != null) campInfoChipButton.onClick.AddListener(() => SelectTopic(Topic.CampInfo));
        if (randomFactsChipButton != null) randomFactsChipButton.onClick.AddListener(() => SelectTopic(Topic.RandomFacts));

        Hide();
    }

    /// <summary>Call whenever the hologram is (re)activated for a given memorial.</summary>
    public void Configure(bool symbolTopicAvailable)
    {
        hasSymbolTopic = symbolTopicAvailable;
        chipsExpanded = false;
        
        if (chipContainer != null) chipContainer.gameObject.SetActive(false);
        if (symbolChipButton != null) symbolChipButton.gameObject.SetActive(symbolTopicAvailable);
        
        if (randomFactsChipButton != null) randomFactsChipButton.gameObject.SetActive(true);

        RefreshLabels();
        Show();

        if (gameObject.activeInHierarchy)
        {
            StopAllCoroutines();
            StartCoroutine(NudgeRoutine());
        }
    }

    public void SetLanguage(AppLanguage language)
    {
        currentLanguage = language;
        RefreshLabels();
    }

    public void Hide()
    {
        if (mainButtonRoot != null) mainButtonRoot.gameObject.SetActive(false);
        if (chipContainer != null) chipContainer.gameObject.SetActive(false);
        else if (symbolChipButton != null) symbolChipButton.gameObject.SetActive(false);
        chipsExpanded = false;
        gameObject.SetActive(false);
    }

    private void Show()
    {
        gameObject.SetActive(true);
        if (mainButtonRoot != null) mainButtonRoot.gameObject.SetActive(true);
        if (chipContainer == null && symbolChipButton != null) symbolChipButton.gameObject.SetActive(hasSymbolTopic);
    }

    private void RefreshLabels()
    {
        string repeatText, symbolText, campInfoText, randomFactsText;

        switch (currentLanguage)
        {
            case AppLanguage.DE:
                repeatText = "Wiederholen";
                symbolText = "Symbol";
                campInfoText = "Lager Info";
                randomFactsText = "Fakt";
                break;
            case AppLanguage.HE:
                repeatText = "חזור";
                symbolText = "סמל";
                campInfoText = "מידע על המחנה";
                randomFactsText = "עובדה";
                break;
            default:
                repeatText = "Repeat";
                symbolText = "Symbol";
                campInfoText = "Camp Info";
                randomFactsText = "Fact";
                break;
        }

        if (repeatChipLabel != null) repeatChipLabel.text = repeatText;
        if (symbolChipLabel != null) symbolChipLabel.text = symbolText;
        if (campInfoChipLabel != null) campInfoChipLabel.text = campInfoText;
        if (randomFactsChipLabel != null) randomFactsChipLabel.text = randomFactsText;
    }

    private void HandleMainButtonPressed()
    {
        // If there is no chip container, the main button is a direct Repeat button
        if (chipContainer == null)
        {
            SelectTopic(Topic.Repeat);
            return;
        }

        chipsExpanded = !chipsExpanded;
        if (chipContainer != null) chipContainer.gameObject.SetActive(chipsExpanded);
    }

    private void SelectTopic(Topic topic)
    {
        NarrationManager.StopAllPlaybackGlobal();
        chipsExpanded = false;
        if (chipContainer != null) chipContainer.gameObject.SetActive(false);
        OnTopicSelected?.Invoke(topic);
    }

    /// <summary>
    /// A few gentle scale pulses plus a soft one-shot sound, played once when the button first
    /// appears with something extra to offer. Settles back to a static icon afterward — this is
    /// NOT a looping/permanent animation, just an attention cue at the moment of appearance.
    /// </summary>
    private IEnumerator NudgeRoutine()
    {
        if (nudgeAudioSource != null && nudgeSound != null)
        {
            nudgeAudioSource.PlayOneShot(nudgeSound);
        }

        for (int i = 0; i < nudgePulseCount; i++)
        {
            yield return ScaleTo(mainButtonBaseScale * nudgePulseScale, nudgePulseDuration * 0.5f);
            yield return ScaleTo(mainButtonBaseScale, nudgePulseDuration * 0.5f);
        }
    }

    private IEnumerator ScaleTo(Vector3 targetScale, float duration)
    {
        if (mainButtonRoot == null) yield break;

        Vector3 startScale = mainButtonRoot.localScale;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            mainButtonRoot.localScale = Vector3.Lerp(startScale, targetScale, t / duration);
            yield return null;
        }
        mainButtonRoot.localScale = targetScale;
    }
}
