using UnityEngine;

/// <summary>
/// Condition A (Impersonal): Autonomous exploration profile. 
/// Bypasses 3D avatar instantiation entirely, leaving data streams strictly to 2D UI panels.
/// </summary>
public class ImpersonalGuidance : GuidanceSystemBase
{
    private Coroutine welcomeSubtitleRoutine;
    private AskMoreButtonController askMoreButton;
    private string activeMemorialID;
    private int nextFactIndex;

    protected override void OnInitialize()
    {
        askMoreButton = Object.FindAnyObjectByType<AskMoreButtonController>(FindObjectsInactive.Include);
        if (askMoreButton == null) return;
        askMoreButton.OnTopicSelected -= HandleAskMoreTopic;
        askMoreButton.OnTopicSelected += HandleAskMoreTopic;
        askMoreButton.Configure(false);
    }
    public override void OnMemorialSelected(string memorialID)
    {
        if (!string.IsNullOrEmpty(memorialID) && !memorialID.StartsWith("WELCOME")) activeMemorialID = memorialID;
        if (thesisManager != null)
            thesisManager.LogEvent("memorial_selected", memorialID, "impersonal");

        if (memorialID != null && memorialID.StartsWith("WELCOME") && uiManager != null)
        {
            bool isGerman = string.Equals(uiManager.SelectedLanguage, "german", System.StringComparison.OrdinalIgnoreCase);
            string text = isGerman
                ? "Autonomer Erkundungsmodus aktiv.\n• Erkunden Sie leise das Gelände.\n• Tippen Sie auf eine rote Stecknadel auf der 2D-Karte.\n• Nutzen Sie das Navigationsmenü für Routen."
                : "Manual Guidance Mode Active.\n• Explore the site at your own pace.\n• Tap on any red pin on the 2D map to view historical profiles.\n• Plan custom routes by selecting multiple memorials.";
            text = text.Replace("red pin", "pin");
            uiManager.DisplayGuideSubtitle(text);
            if (welcomeSubtitleRoutine != null) StopCoroutine(welcomeSubtitleRoutine);
            welcomeSubtitleRoutine = StartCoroutine(ClearWelcomeSubtitleAfterDelay());
        }
    }


    public override void OnMemorialDeselected()
    {
        if (thesisManager != null)
            thesisManager.LogEvent("memorial_deselected", string.Empty, "impersonal");
    }

    public override void OnMemorialReached(string memorialID)
    {
        activeMemorialID = memorialID;
        askMoreButton?.Configure(false);
        if (thesisManager != null)
            thesisManager.LogEvent("memorial_reached", memorialID, "impersonal");

    }

    private System.Collections.IEnumerator ClearWelcomeSubtitleAfterDelay()
    {
        yield return new WaitForSeconds(9f);
        if (uiManager != null) uiManager.DisplayGuideSubtitle(string.Empty);
        welcomeSubtitleRoutine = null;
    }

    private void HandleAskMoreTopic(AskMoreButtonController.Topic topic)
    {
        if (NarrationManager.Instance == null) return;
        if (topic == AskMoreButtonController.Topic.Repeat && !string.IsNullOrEmpty(activeMemorialID))
        {
            NarrationManager.Instance.PlayNarration(activeMemorialID);
            return;
        }

        string[] facts = {
            "CampOrigins", "ExchangeCampAnomaly", "JosefKramer", "CampPyres",
            "DeathMarches", "RegistryDestruction", "PowCemetery", "CrematoriumDiaries",
            "LiberationDrama", "BarracksBurning", "DisplacedPersons", "BelsenTrials"
        };
        string suffix = uiManager != null && uiManager.SelectedLanguage == "german" ? "DE" : "EN";
        NarrationManager.Instance.PlayNarration(facts[nextFactIndex++ % facts.Length] + "_" + suffix);
    }
}
