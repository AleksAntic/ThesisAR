using UnityEngine;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Handles lightweight persistent local storage using JSON serialization.
/// Stores custom user-created structural routes and accessibility configuration preferences.
/// Maintained strictly in English with safe directory fallback parameters.
/// </summary>
public class PersistenceManager : MonoBehaviour
{
    [System.Serializable]
    public class SavedRouteData
    {
        public string routeLabel;
        public List<string> orderedStoneIDs = new List<string>();
        public float aggregateDistance;
    }

    [System.Serializable]
    public class SavedRouteCollection
    {
        public List<SavedRouteData> customRoutes = new List<SavedRouteData>();
    }

    private string saveFilePath;

    void Awake()
    {
        // Platform independent permanent storage data file mapping directory
        saveFilePath = Path.Combine(Application.persistentDataPath, "custom_user_routes.json");
    }

    /// <summary>
    /// appends and serializes a custom path structure directly to the local system memory storage.
    /// </summary>
    public void SaveCustomRoute(string label, List<string> stoneIDs, float distance)
    {
        SavedRouteCollection collection = LoadAllRoutesContainer();

        // Overwrite if duplicate route name label is generated to preserve integrity
        collection.customRoutes.RemoveAll(r => r.routeLabel.Equals(label, System.StringComparison.OrdinalIgnoreCase));

        SavedRouteData newRoute = new SavedRouteData
        {
            routeLabel = label,
            orderedStoneIDs = new List<string>(stoneIDs),
            aggregateDistance = distance
        };

        collection.customRoutes.Add(newRoute);

        string jsonOutput = JsonUtility.ToJson(collection, true);
        File.WriteAllText(saveFilePath, jsonOutput);
        Debug.Log($"[Persistence] Custom route successfully synchronized to path: {saveFilePath}");
    }

    /// <summary>
    /// Retrieves all serialized route entries loaded straight from the physical disk cache files.
    /// </summary>
    public List<SavedRouteData> LoadSavedRoutes()
    {
        return LoadAllRoutesContainer().customRoutes;
    }

    private SavedRouteCollection LoadAllRoutesContainer()
    {
        if (!File.Exists(saveFilePath))
        {
            return new SavedRouteCollection();
        }

        try
        {
            string rawJson = File.ReadAllText(saveFilePath);
            SavedRouteCollection container = JsonUtility.FromJson<SavedRouteCollection>(rawJson);
            return container ?? new SavedRouteCollection();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Persistence] Critical failure reading local JSON save index: {ex.Message}");
            return new SavedRouteCollection();
        }
    }

    /// <summary>
    /// Accessibility Configuration Storage using lightweight native PlayerPrefs wrappers.
    /// </summary>
    public void SaveAccessibilitySettings(bool useWorldSpacePopups, float subtitleTextSize)
    {
        PlayerPrefs.SetInt("Settings_UI_WorldSpace", useWorldSpacePopups ? 1 : 0);
        PlayerPrefs.SetFloat("Settings_UI_SubtitleScale", subtitleTextSize);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Permanently deletes a saved route from the local JSON file matching its custom label name.
    /// Added to unblock user management actions.
    /// </summary>
    public void DeleteCustomRoute(string label)
    {
        SavedRouteCollection collection = LoadAllRoutesContainer();

        // Find and remove the route that matches the string identifier
        int removedCount = collection.customRoutes.RemoveAll(r => r.routeLabel.Equals(label, System.StringComparison.OrdinalIgnoreCase));

        if (removedCount > 0)
        {
            string jsonOutput = JsonUtility.ToJson(collection, true);
            File.WriteAllText(saveFilePath, jsonOutput);
            Debug.Log($"[Persistence] Route '{label}' was successfully purged from local storage.");
        }
        else
        {
            Debug.LogWarning($"[Persistence] Delete failed: No route named '{label}' discovered in JSON index.");
        }
    }
}

