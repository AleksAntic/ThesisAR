using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central State Machine running the dynamic AR narrative pipeline.
/// Implements the Time-Slicer logic for safe scheduling, handles semi-automated user input gates,
/// and executes smooth audio fade-out overrides during active tourist detour switches.
/// All variables, logs, and state routines are strictly maintained in English.
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("🔊 Hardware Audio Interfaces")]
    [SerializeField] private AudioSource companionAudioSource;
    [SerializeField] private Animator companionAnimator;

    [Header("⚙️ Narrative Behavior Constraints")]
    [SerializeField] private float safetySilenceBuffer = 2.0f; // Gap space between lines in seconds
    [SerializeField] private bool autoAdvanceNextLine = true;  // Toggle for Auto vs Semi-Auto preference

    [Header("🎭 Anchor Audio Fallbacks (Detour Connectors)")]
    [SerializeField] private List<AudioClip> detourAnchorClips = new List<AudioClip>();

    private Coroutine activePlaybackRoutine;
    private bool isWaitingForUserGateInput = false;
    private DialogueSequence currentActiveSequence;
    private int currentLinePlaybackIndex = 0;

    // Architectural global action callbacks for systems synchronization (e.g., RouteManager pausing)
    public static event Action OnDialoguePlaybackStarted;
    public static event Action<string> OnSubtitleUpdated;
    public static event Action OnDialoguePlaybackEnded;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (companionAudioSource == null) companionAudioSource = GetComponent<AudioSource>();
    }

    private UIManager uiManager;

    private AppLanguage CurrentLanguage
    {
        get
        {
            if (uiManager == null) uiManager = FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
            return uiManager != null ? uiManager.SelectedLanguage.ToAppLanguage() : AppLanguage.EN;
        }
    }

    /// <summary>
    /// Evaluates total route travel time using the Time-Slicer algorithm to select 
    /// a series of sequential clips that fit perfectly within the movement duration.
    /// </summary>
    public List<DialogueSequence.DialogueLine> CalculateOptimalTimeSliceQueue(List<DialogueSequence> availablePool, float totalTravelDuration)
    {
        List<DialogueSequence.DialogueLine> optimalSlicingQueue = new List<DialogueSequence.DialogueLine>();
        float allocatedTimeCounter = 0f;

        // Shuffle/Iterate through available conversational pools
        foreach (DialogueSequence seq in availablePool)
        {
            if (seq.narrativeCategory != DialogueSequence.DialogueCategory.Global_Walking) continue;

            foreach (var line in seq.dialogueLines)
            {
                var localized = line.GetLine(CurrentLanguage);
                float lineTotalWeight = localized.duration + safetySilenceBuffer;

                // Knapsack validation checklist constraint check
                if (allocatedTimeCounter + lineTotalWeight <= totalTravelDuration)
                {
                    optimalSlicingQueue.Add(line);
                    allocatedTimeCounter += lineTotalWeight;
                }
                else
                {
                    // Break early when available travel time slots fill up completely
                    break;
                }
            }
        }
        
        Debug.Log($"[Time-Slicer] Scheduled {optimalSlicingQueue.Count} narration lines. Occupying {allocatedTimeCounter:F1}s out of {totalTravelDuration:F1}s travel slot.");
        return optimalSlicingQueue;
    }

    public void PlayNarrativeSequence(DialogueSequence sequence)
    {
        if (sequence == null) return;

        // INTERRUPTION INTERCEPTOR: If the user triggers a high-priority detour, execute structural safety fade-out
        if (activePlaybackRoutine != null)
        {
            StopCoroutine(activePlaybackRoutine);
            activePlaybackRoutine = StartCoroutine(ExecuteDetourInterruptionTransition(sequence));
            return;
        }

        currentActiveSequence = sequence;
        activePlaybackRoutine = StartCoroutine(PlaybackExecutionLoop());
    }

    private IEnumerator PlaybackExecutionLoop()
    {
        OnDialoguePlaybackStarted?.Invoke();
        currentLinePlaybackIndex = 0;

        while (currentLinePlaybackIndex < currentActiveSequence.dialogueLines.Count)
        {
            var activeLineRaw = currentActiveSequence.dialogueLines[currentLinePlaybackIndex];
            var localized = activeLineRaw.GetLine(CurrentLanguage);

            // Send subtitle texts and animation triggers to UI and Companion models
            OnSubtitleUpdated?.Invoke(localized.subtitleText);
            
            if (companionAnimator != null && !string.IsNullOrEmpty(activeLineRaw.animationTriggerKey))
            {
                companionAnimator.SetTrigger(activeLineRaw.animationTriggerKey);
            }

            // Fire up native hardware audio outputs
            if (companionAudioSource != null && localized.voiceClip != null)
            {
                companionAudioSource.clip = localized.voiceClip;
                companionAudioSource.Play();
            }

            // Wait until the audio file finishes playing physically
            yield return new WaitForSeconds(localized.duration);

            // User configuration gate check: Auto vs Semi-Auto preference mode tracking
            if (!autoAdvanceNextLine && currentLinePlaybackIndex < currentActiveSequence.dialogueLines.Count - 1)
            {
                isWaitingForUserGateInput = true;
                Debug.Log("[Dialogue Engine] Line complete. Paused, waiting for user touch screen confirmation UI input...");
                while (isWaitingForUserGateInput)
                {
                    yield return null; // Freeze coroutine frame execution pending interface click callback
                }
            }

            // Inject the custom structural silence safety buffer spacer before opening the next line block
            yield return new WaitForSeconds(safetySilenceBuffer);
            currentLinePlaybackIndex++;
        }

        CleanPlaybackState();
    }

    /// <summary>
    /// Context-Switching Raccordo: Graces sudden detour cuts by fading audio, 
    /// injecting an organic transition vocalization phrase, then kicking off the focal monument description.
    /// </summary>
    private IEnumerator ExecuteDetourInterruptionTransition(DialogueSequence targetHighPrioritySequence)
    {
        Debug.Log("[Dialogue Engine] Interruption constraint caught! Executing active audio fade-out.");

        // 1. Smoothly fade out the current active background speech soundscape
        float startVolume = companionAudioSource.volume;
        while (companionAudioSource.volume > 0)
        {
            companionAudioSource.volume -= startVolume * Time.deltaTime * 2.5f; // Fast 0.4s drop
            yield return null;
        }

        companionAudioSource.Stop();
        companionAudioSource.volume = startVolume; // Reset original operational volume settings

        // 2. Play a random context connector anchor audio clip (e.g., "Oh, look at this section here...")
        if (detourAnchorClips.Count > 0)
        {
            AudioClip anchorPhrase = detourAnchorClips[UnityEngine.Random.Range(0, detourAnchorClips.Count)];
            companionAudioSource.clip = anchorPhrase;
            companionAudioSource.Play();
            
            if (companionAnimator != null) companionAnimator.SetTrigger("Look_Around");
            yield return new WaitForSeconds(anchorPhrase.length + 0.5f);
        }

        // 3. Clear memory matrices and safely branch into the high-priority destination data
        currentActiveSequence = targetHighPrioritySequence;
        activePlaybackRoutine = StartCoroutine(PlaybackExecutionLoop());
    }

    /// <summary>
    /// Public UI Interaction interface link. Clears User Preference Gate holds during Semi-Auto play loops.
    /// </summary>
    public void RequestUserGateAdvance()
    {
        if (isWaitingForUserGateInput)
        {
            isWaitingForUserGateInput = false;
            Debug.Log("[Dialogue Engine] User gate advance validated via UI click event.");
        }
    }

    private void CleanPlaybackState()
    {
        OnSubtitleUpdated?.Invoke(string.Empty);
        activePlaybackRoutine = null;
        currentActiveSequence = null;
        OnDialoguePlaybackEnded?.Invoke();
        Debug.Log("[Dialogue Engine] Sequence queue playback cycle reached end of stream successfully.");
    }

    public void ForceStopNarrationImmediate()
    {
        if (activePlaybackRoutine != null)
        {
            StopCoroutine(activePlaybackRoutine);
            activePlaybackRoutine = null;
        }
        if (companionAudioSource != null) companionAudioSource.Stop();
        CleanPlaybackState();
    }

    public void SetCompanionReferences(Animator animator, AudioSource audioSource)
    {
        companionAnimator = animator;
        companionAudioSource = audioSource;
        Debug.Log("[DialogueManager] Updated companion animator and audio source references.");
    }
}