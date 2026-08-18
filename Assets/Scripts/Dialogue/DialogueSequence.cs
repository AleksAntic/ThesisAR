using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data architecture component using ScriptableObjects to define standalone speech lines.
/// Allows narrative designers to arrange audio data, subtitles, and animation triggers without modifying code.
/// All variables, comments, and structure are strictly maintained in English.
/// </summary>
[CreateAssetMenu(fileName = "NewDialogueSequence", menuName = "Narrative/Dialogue Sequence", order = 1)]
public class DialogueSequence : ScriptableObject
{
    public enum DialogueCategory { Global_Walking, Zone_SocioCultural, Micro_StoneFocus, Detour_Transition, Systemic_UI, Symbol_Info }

    [Serializable]
    public class LocalizedVoiceLine
    {
        [TextArea(2, 5)]
        public string subtitleText;
        public AudioClip voiceClip;
        [Tooltip("Explicit length in seconds of THIS language's audio clip. Different languages record at different paces.")]
        public float duration;
    }

    [Serializable]
    public class DialogueLine
    {
        public string animationTriggerKey; // Animator trigger matching companion pose (e.g., "Talk", "Point")

        [Header("🌐 Per-Language Content")]
        public LocalizedVoiceLine english;
        public LocalizedVoiceLine german;
        public LocalizedVoiceLine hebrew;

        // Legacy fields preserved for backwards compatibility before data migration
        [HideInInspector] public string subtitleText;
        [HideInInspector] public AudioClip voiceClip;
        [HideInInspector] public float duration;

        /// <summary>
        /// Restituisce la LocalizedVoiceLine per la lingua richiesta, con fallback automatico a
        /// inglese o ai campi legacy se la lingua non è popolata.
        /// </summary>
        public LocalizedVoiceLine GetLine(AppLanguage lang)
        {
            LocalizedVoiceLine candidate = lang switch
            {
                AppLanguage.DE => german,
                AppLanguage.HE => hebrew,
                _ => english
            };

            bool candidateIsEmpty = candidate == null || (candidate.voiceClip == null && string.IsNullOrEmpty(candidate.subtitleText));
            if (!candidateIsEmpty) return candidate;

            if (english != null && (english.voiceClip != null || !string.IsNullOrEmpty(english.subtitleText)))
            {
                return english;
            }

            // Fallback to legacy fields if migration hasn't run yet
            return new LocalizedVoiceLine
            {
                subtitleText = this.subtitleText,
                voiceClip = this.voiceClip,
                duration = this.duration
            };
        }
    }

    [Header("📋 Narrative Metadata Classification")]
    public string sequenceLabelId;
    public DialogueCategory narrativeCategory;
    
    [Header("💬 Sequential Speech Queue")]
    public List<DialogueLine> dialogueLines = new List<DialogueLine>();
}