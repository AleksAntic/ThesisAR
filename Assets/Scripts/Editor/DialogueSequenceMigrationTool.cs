#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class DialogueSequenceMigrationTool
{
    [MenuItem("ThesisAR/Migrate DialogueSequences to Multi-Language Schema")]
    public static void MigrateAllDialogueSequences()
    {
        string[] guids = AssetDatabase.FindAssets("t:DialogueSequence");
        int migratedCount = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            DialogueSequence seq = AssetDatabase.LoadAssetAtPath<DialogueSequence>(path);
            if (seq == null || seq.dialogueLines == null) continue;

            bool modified = false;
            foreach (var line in seq.dialogueLines)
            {
                if (line.english == null) line.english = new DialogueSequence.LocalizedVoiceLine();

                // If legacy fields are set but english line is empty, migrate legacy values to english
                if (line.english.voiceClip == null && string.IsNullOrEmpty(line.english.subtitleText))
                {
                    if (line.voiceClip != null || !string.IsNullOrEmpty(line.subtitleText))
                    {
                        line.english.subtitleText = line.subtitleText;
                        line.english.voiceClip = line.voiceClip;
                        line.english.duration = line.duration;
                        modified = true;
                    }
                }
            }

            if (modified)
            {
                EditorUtility.SetDirty(seq);
                migratedCount++;
            }
        }

        if (migratedCount > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"[Migration Tool] Successfully migrated {migratedCount} DialogueSequence asset(s) to the new multi-language schema.");
        }
        else
        {
            Debug.Log($"[Migration Tool] All {guids.Length} DialogueSequence asset(s) are already up to date.");
        }
    }
}
#endif
