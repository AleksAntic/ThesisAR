using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Centralized, decoupled localization manager for all static and dynamic UI text elements across ThesisAR.
/// Reads from Resources/ui_localization.json and broadcasts language change events.
/// </summary>
public class UILocalizationManager : MonoBehaviour
{
    public static UILocalizationManager Instance { get; private set; }

    [SerializeField] private string currentLanguage = "english";

    public string CurrentLanguage
    {
        get => currentLanguage;
        set
        {
            if (!string.Equals(currentLanguage, value, StringComparison.OrdinalIgnoreCase))
            {
                currentLanguage = value.ToLower();
                PlayerPrefs.SetString("Thesis_Language", currentLanguage);
                PlayerPrefs.Save();
                OnLanguageChanged?.Invoke(currentLanguage);
            }
        }
    }

    public bool IsGerman => string.Equals(currentLanguage, "german", StringComparison.OrdinalIgnoreCase);

    public event Action<string> OnLanguageChanged;

    private Dictionary<string, LocalizationEntry> localizationCache = new Dictionary<string, LocalizationEntry>(StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        currentLanguage = PlayerPrefs.GetString("Thesis_Language", "english").ToLower();
        LoadLocalizationCatalog();
    }

    public void LoadLocalizationCatalog()
    {
        localizationCache.Clear();
        TextAsset asset = Resources.Load<TextAsset>("ui_localization");
        if (asset != null)
        {
            try
            {
                CatalogWrapper catalog = JsonUtility.FromJson<CatalogWrapper>(asset.text);
                if (catalog != null && catalog.entries != null)
                {
                    foreach (var entry in catalog.entries)
                    {
                        if (!string.IsNullOrEmpty(entry.key))
                        {
                            localizationCache[entry.key] = entry;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UILocalizationManager] Failed to parse ui_localization.json: {ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning("[UILocalizationManager] Resources/ui_localization.json not found. Using runtime fallbacks.");
        }
    }

    public string GetText(string key, string fallbackEn = "", string fallbackDe = "")
    {
        if (localizationCache.TryGetValue(key, out LocalizationEntry entry))
        {
            string val = IsGerman ? entry.de : entry.en;
            if (!string.IsNullOrEmpty(val)) return val;
        }

        if (IsGerman && !string.IsNullOrEmpty(fallbackDe)) return fallbackDe;
        return fallbackEn;
    }

    [Serializable]
    private class CatalogWrapper
    {
        public List<LocalizationEntry> entries;
    }

    [Serializable]
    private class LocalizationEntry
    {
        public string key;
        public string en;
        public string de;
    }
}
