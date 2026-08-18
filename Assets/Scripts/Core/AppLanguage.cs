using System;

/// <summary>
/// Enum di lingua unico e condiviso in tutto il progetto ThesisAR.
/// Sostituisce le rappresentazioni parallele precedenti:
/// - UIManager.selectedLanguage (string libera)
/// - CoachmarkTutorialController.Language (enum locale, mancava HE)
/// Ogni controller che gestisce testo/audio multilingua deve referenziare QUESTO enum.
/// </summary>
public enum AppLanguage
{
    EN,
    DE,
    HE
}

public static class AppLanguageExtensions
{
    /// <summary>
    /// Converte la stringa libera storicamente usata in PlayerPrefs / UIManager.selectedLanguage
    /// nel nuovo enum. Mantiene la retrocompatibilità con i salvataggi PlayerPrefs esistenti.
    /// </summary>
    public static AppLanguage ToAppLanguage(this string raw)
    {
        if (string.IsNullOrEmpty(raw)) return AppLanguage.EN;

        switch (raw.Trim().ToLowerInvariant())
        {
            case "german":
            case "de":
            case "deutsch":
                return AppLanguage.DE;
            case "hebrew":
            case "he":
                return AppLanguage.HE;
            default:
                return AppLanguage.EN;
        }
    }

    /// <summary>Converte l'enum nella stringa libera usata storicamente (PlayerPrefs, log, ecc.)</summary>
    public static string ToLegacyString(this AppLanguage lang)
    {
        switch (lang)
        {
            case AppLanguage.DE: return "german";
            case AppLanguage.HE: return "hebrew";
            default: return "english";
        }
    }

    /// <summary>Suffisso a 2 lettere usato per i nomi dei file Resources (es. "STONE_A3_DE")</summary>
    public static string ToFileSuffix(this AppLanguage lang)
    {
        switch (lang)
        {
            case AppLanguage.DE: return "DE";
            case AppLanguage.HE: return "HE";
            default: return "EN";
        }
    }
}
