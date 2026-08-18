using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages search and filtering workflows for memorial points of interest.
/// Supports text queries, religious tracking classifications, and gender background parameters.
/// </summary>
public class MemorialFilterManager : MonoBehaviour
{
    [Serializable]
    public class FilterCriteria
    {
        public string textQuery = string.Empty;
        public string religion = string.Empty;
        public string gender = string.Empty;
    }

    [SerializeField] private MemorialDataManager dataManager;
    [SerializeField] private MemorialSpawner memorialSpawner;

    public event Action<List<string>> OnFilterChanged;

    private readonly List<string> currentFilteredIDs = new List<string>();
    private FilterCriteria currentCriteria = new FilterCriteria();

    void Awake()
    {
        if (dataManager == null) dataManager = UnityEngine.Object.FindAnyObjectByType<MemorialDataManager>(FindObjectsInactive.Include);
        
        if (memorialSpawner == null)
            memorialSpawner = UnityEngine.Object.FindAnyObjectByType<MemorialSpawner>(FindObjectsInactive.Include);
    }

    public List<string> FilterPoints(string query, string religion, string gender)
    {
        currentCriteria.textQuery = query ?? string.Empty;
        currentCriteria.religion = religion ?? string.Empty;
        currentCriteria.gender = gender ?? string.Empty;

        currentFilteredIDs.Clear();

        if (dataManager == null)
        {
            Debug.LogWarning("[FilterManager] Operations bypassed: MemorialDataManager instance reference is missing.");
            return currentFilteredIDs;
        }

        var stones = dataManager.GetAllMemorialStones();
        foreach (var stone in stones)
        {
            if (MatchesStone(stone, currentCriteria))
                currentFilteredIDs.Add(stone.id);
        }

        var graves = dataManager.GetAllMassGraves();
        foreach (var grave in graves)
        {
            if (MatchesGrave(grave, currentCriteria))
                currentFilteredIDs.Add(grave.id);
        }

        var memorials = dataManager.GetAllOtherMemorials();
        foreach (var memorial in memorials)
        {
            if (MatchesOtherMemorial(memorial, currentCriteria))
                currentFilteredIDs.Add(memorial.id);
        }

        OnFilterChanged?.Invoke(new List<string>(currentFilteredIDs));
        UpdateSpawnerVisibility();
        return new List<string>(currentFilteredIDs);
    }

    public List<string> ClearFilter()
    {
        return FilterPoints(string.Empty, string.Empty, string.Empty);
    }

    public List<string> SearchText(string query)
    {
        return FilterPoints(query, currentCriteria.religion, currentCriteria.gender);
    }

    public List<string> FilterByReligion(string religion)
    {
        return FilterPoints(currentCriteria.textQuery, religion, currentCriteria.gender);
    }

    public List<string> FilterByGender(string gender)
    {
        return FilterPoints(currentCriteria.textQuery, currentCriteria.religion, gender);
    }

    private void UpdateSpawnerVisibility()
    {
        if (memorialSpawner == null)
            return;

        var allSpawned = memorialSpawner.GetAllSpawnedMemorials();
        foreach (var kvp in allSpawned)
        {
            bool visible = currentFilteredIDs.Count == 0 || currentFilteredIDs.Contains(kvp.Key);
            var obj = kvp.Value;

            if (obj == null) continue;

            // Instead of deactivating the whole GameObject (which may disable AR anchors/tracking),
            // only disable visual and physics components while leaving structural scripts and anchors active.
            var renderers = obj.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
                r.enabled = visible;

            var canvasGroups = obj.GetComponentsInChildren<CanvasGroup>(true);
            foreach (var cg in canvasGroups)
                cg.alpha = visible ? 1f : 0f;

            var colliders = obj.GetComponentsInChildren<Collider>(true);
            foreach (var c in colliders)
                c.enabled = visible;

            // Keep the GameObject active so AR anchor components continue running smoothly
        }
    }

    private bool MatchesStone(MemorialDataManager.MemorialStone stone, FilterCriteria criteria)
    {
        if (!MatchesText(stone, criteria.textQuery))
            return false;

        if (!MatchesPersonFilters(stone.persons, criteria.religion, criteria.gender))
            return false;

        return true;
    }

    private bool MatchesGrave(MemorialDataManager.MassGrave grave, FilterCriteria criteria)
    {
        return MatchesText(grave.description, grave.notes, criteria.textQuery);
    }

    private bool MatchesOtherMemorial(MemorialDataManager.OtherMemorial memorial, FilterCriteria criteria)
    {
        return MatchesText(memorial.description, memorial.notes, criteria.textQuery);
    }

    private bool MatchesText(MemorialDataManager.MemorialStone stone, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        string q = query.Trim().ToLowerInvariant();

        if (stone.persons != null)
        {
            foreach (var person in stone.persons)
            {
                if (!string.IsNullOrEmpty(person.surname) && person.surname.ToLowerInvariant().Contains(q))
                    return true;
                if (!string.IsNullOrEmpty(person.forename) && person.forename.ToLowerInvariant().Contains(q))
                    return true;
                if (!string.IsNullOrEmpty(person.other_links) && person.other_links.ToLowerInvariant().Contains(q))
                    return true;
            }
        }

        if (!string.IsNullOrEmpty(stone.book_text) && stone.book_text.ToLowerInvariant().Contains(q))
            return true;

        if (!string.IsNullOrEmpty(stone.notes) && stone.notes.ToLowerInvariant().Contains(q))
            return true;

        return false;
    }

    private bool MatchesText(string primary, string secondary, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        string q = query.Trim().ToLowerInvariant();
        return (!string.IsNullOrEmpty(primary) && primary.ToLowerInvariant().Contains(q)) ||
               (!string.IsNullOrEmpty(secondary) && secondary.ToLowerInvariant().Contains(q));
    }

    private bool MatchesPersonFilters(List<MemorialDataManager.Person> persons, string religion, string gender)
    {
        bool religionFilter = !string.IsNullOrWhiteSpace(religion);
        bool genderFilter = !string.IsNullOrWhiteSpace(gender);

        if (!religionFilter && !genderFilter)
            return true;

        if (persons == null || persons.Count == 0)
            return false;

        foreach (var person in persons)
        {
            bool religionOk = !religionFilter || (!string.IsNullOrEmpty(person.religion) && string.Equals(person.religion, religion, StringComparison.OrdinalIgnoreCase));
            bool genderOk = !genderFilter || (!string.IsNullOrEmpty(person.gender) && string.Equals(person.gender, gender, StringComparison.OrdinalIgnoreCase));

            if (religionOk && genderOk)
                return true;
        }

        return false;
    }
}