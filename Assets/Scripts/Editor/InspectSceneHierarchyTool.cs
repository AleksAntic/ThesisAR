#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;
using System.IO;

public static class InspectSceneHierarchyTool
{
    [MenuItem("ThesisAR/Inspect UI Hierarchy & Dump Diagnostics")]
    public static void InspectAndDumpUI()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== THESISAR UI DIAGNOSTICS DUMP ===");
        sb.AppendLine($"Time: {System.DateTime.Now}");

        UIManager uiManager = Object.FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
        sb.AppendLine($"UIManager instance found? {uiManager != null}");
        if (uiManager != null)
        {
            sb.AppendLine($"  selectedLanguage: '{uiManager.SelectedLanguage}'");
        }

        GameObject searchPanel = GameObject.Find("Database_Search_Panel") ?? GameObject.Find("Canvas/Database_Search_Panel");
        if (searchPanel == null)
        {
            foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (g.name == "Database_Search_Panel" && g.scene.name != null)
                {
                    searchPanel = g;
                    break;
                }
            }
        }

        sb.AppendLine($"\n--- DATABASE_SEARCH_PANEL ---");
        if (searchPanel != null)
        {
            DumpTransformTree(searchPanel.transform, sb, 0);
        }
        else
        {
            sb.AppendLine("Database_Search_Panel NOT FOUND in scene!");
        }

        GameObject onboarding = GameObject.Find("Onboarding_Panel") ?? GameObject.Find("Canvas/Onboarding_Panel");
        if (onboarding == null)
        {
            foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (g.name == "Onboarding_Panel" && g.scene.name != null)
                {
                    onboarding = g;
                    break;
                }
            }
        }

        sb.AppendLine($"\n--- ONBOARDING_PANEL ---");
        if (onboarding != null)
        {
            DumpTransformTree(onboarding.transform, sb, 0);
        }
        else
        {
            sb.AppendLine("Onboarding_Panel NOT FOUND in scene!");
        }

        string outputPath = Path.Combine(Application.dataPath, "../UI_Hierarchy_Diagnostics.txt");
        File.WriteAllText(outputPath, sb.ToString());
        Debug.Log($"[Hierarchy Diagnostic] Saved full UI dump to: {outputPath}");
    }

    private static void DumpTransformTree(Transform t, StringBuilder sb, int indent)
    {
        string prefix = new string(' ', indent * 2);
        string activeStr = t.gameObject.activeSelf ? "ACTIVE" : "INACTIVE";
        RectTransform rt = t.GetComponent<RectTransform>();
        string rectStr = rt != null ? $"anchorMin={rt.anchorMin}, anchorMax={rt.anchorMax}, sizeDelta={rt.sizeDelta}, rect={rt.rect}" : "No RectTransform";

        sb.AppendLine($"{prefix}• {t.name} [{activeStr}] ({rectStr})");

        var btn = t.GetComponent<Button>();
        if (btn != null)
        {
            sb.AppendLine($"{prefix}   [Button] interactable={btn.interactable}, targetGraphic={(btn.targetGraphic != null ? btn.targetGraphic.name : "NULL")}, onClickCount={btn.onClick.GetPersistentEventCount()}");
        }

        var sr = t.GetComponent<ScrollRect>();
        if (sr != null)
        {
            sb.AppendLine($"{prefix}   [ScrollRect] content={(sr.content != null ? sr.content.name : "NULL")}, viewport={(sr.viewport != null ? sr.viewport.name : "NULL")}");
        }

        var txt = t.GetComponent<TextMeshProUGUI>();
        if (txt != null)
        {
            sb.AppendLine($"{prefix}   [TMP] text='{txt.text}', color={txt.color}, fontSize={txt.fontSize}, raycastTarget={txt.raycastTarget}");
        }

        var img = t.GetComponent<Image>();
        if (img != null)
        {
            sb.AppendLine($"{prefix}   [Image] color={img.color}, raycastTarget={img.raycastTarget}");
        }

        for (int i = 0; i < t.childCount; i++)
        {
            DumpTransformTree(t.GetChild(i), sb, indent + 1);
        }
    }
}
#endif
