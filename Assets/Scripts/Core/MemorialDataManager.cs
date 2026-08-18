using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using System.IO;
using System.Globalization;

/// <summary>
/// Central read-only information database. Parses the flat Excel-like JSON structure 
/// into optimized, nested memory models once at startup to ensure high performance on mobile devices.
/// Includes data-sanitization fallbacks to tolerate syntax errors in the raw source file.
/// All variables, comments, and internal logs are strictly maintained in English.
/// </summary>
public class MemorialDataManager : MonoBehaviour
{
    [Serializable]
    public class Person
    {
        public string surname;
        public string forename;
        public string religion;
        public string gender;
        public string date_of_birth;
        public string place_of_birth;
        public string inmate_number;
        public string date_of_liberation;
        public string place_of_liberation;
        public string date_of_death;
        public int? age_at_death;
        public string place_of_death;
        public string other_links;
        public string english_inscription;
        public string german_inscription;
        public string hebrew_inscription;
    }

    [Serializable]
    public class MemorialStone
    {
        public string id;
        public string type = "memorial_stone";
        public bool has_coordinates;
        public float latitude;
        public float longitude;
        public bool scan;
        public bool photo;
        public bool geo_ref;
        public List<Person> persons = new List<Person>();
        public string book_text;
        public string notes;
        public string symbols;
    }

    [Serializable]
    public class MassGrave
    {
        public string id;
        public string type = "mass_grave";
        public bool has_coordinates;
        public float latitude;
        public float longitude;
        public int death_count;
        public string description;
        public string description_de;
        public string description_he;
        public string notes;
        public string notes_de;
        public string notes_he;
    }

    [Serializable]
    public class OtherMemorial
    {
        public string id;
        public string type = "other_memorial";
        public bool has_coordinates;
        public float latitude;
        public float longitude;
        public string description;
        public string description_de;
        public string description_he;
        public string notes;
        public string notes_de;
        public string notes_he;
    }

    private class InscriptionsDTO
    {
        [JsonProperty("text_english")] public string TextEnglish { get; set; }
        [JsonProperty("text_german")] public string TextGerman { get; set; }
        [JsonProperty("text_hebrew")] public string TextHebrew { get; set; }
    }

    private class RawStoneRowDTO
    {
        [JsonProperty("Memorial Stone")] public string MemorialStone { get; set; }
        [JsonProperty("Latitude")] public object Latitude { get; set; }
        [JsonProperty("Longitude")] public object Longitude { get; set; }
        [JsonProperty("Scan Y/N")] public string ScanYN { get; set; }
        [JsonProperty("Photo Y/N")] public string PhotoYN { get; set; }
        [JsonProperty("Geo ref Y/N")] public string GeoRefYN { get; set; }
        [JsonProperty("Surname")] public string Surname { get; set; }
        [JsonProperty("Forename")] public string Forename { get; set; }
        [JsonProperty("Religion")] public string Religion { get; set; }
        [JsonProperty("Gender")] public string Gender { get; set; }
        [JsonProperty("Date of Birth")] public string DateOfBirth { get; set; }
        [JsonProperty("Place of Birth")] public string PlaceOfBirth { get; set; }
        [JsonProperty("Inmate number")] public string InmateNumber { get; set; }
        [JsonProperty("Date of liberation")] public string DateOfLiberation { get; set; }
        [JsonProperty("place of liberation")] public string PlaceOfLiberation { get; set; }
        [JsonProperty("Date of Death")] public string DateOfDeath { get; set; }
        [JsonProperty("age at death")] public object AgeAtDeath { get; set; }
        [JsonProperty("Place of death")] public string PlaceOfDeath { get; set; }
        [JsonProperty("Notes")] public string Notes { get; set; }
        [JsonProperty("Symbols")] public string Symbols { get; set; }
        [JsonProperty("Text_English")] public string TextEnglish { get; set; }
        [JsonProperty("Text_German")] public string TextGerman { get; set; }
        [JsonProperty("Text_Hebrew")] public string TextHebrew { get; set; }
        [JsonProperty("inscriptions_by_language")] public InscriptionsDTO Inscriptions { get; set; }
    }

    private class RawGraveRowDTO
    {
        [JsonProperty("Mass Graves")] public string MassGraveId { get; set; }

        [JsonProperty("Latitude")] public object StandardLatitude { get; set; }
        [JsonProperty("Unnamed: 6")] public object FallbackLatitude { get; set; }
        [JsonIgnore] public object Latitude => StandardLatitude ?? FallbackLatitude;

        [JsonProperty("Longitude")] public object StandardLongitude { get; set; }
        [JsonProperty("Unnamed: 7")] public object FallbackLongitude { get; set; }
        [JsonIgnore] public object Longitude => StandardLongitude ?? FallbackLongitude;

        [JsonProperty("Estimated Deaths")] public string StandardEstimatedDeaths { get; set; }
        [JsonProperty("Unnamed: 4")] public string FallbackEstimatedDeaths { get; set; }
        [JsonIgnore] public string EstimatedDeaths => StandardEstimatedDeaths ?? FallbackEstimatedDeaths;

        [JsonProperty("Description")] public string Description { get; set; }
        [JsonProperty("Description_DE")] public string DescriptionDE { get; set; }
        [JsonProperty("Description_HE")] public string DescriptionHE { get; set; }
        [JsonProperty("Notes")] public string Notes { get; set; }
        [JsonProperty("Notes_DE")] public string NotesDE { get; set; }
        [JsonProperty("Notes_HE")] public string NotesHE { get; set; }
    }

    private class RawOtherRowDTO
    {
        [JsonProperty("Other Mem Number")] public string OtherMemNumber { get; set; }
        [JsonProperty("Latitude")] public object Latitude { get; set; }
        [JsonProperty("Longitude")] public object Longitude { get; set; }
        [JsonProperty("Description")] public string Description { get; set; }
        [JsonProperty("Description_DE")] public string DescriptionDE { get; set; }
        [JsonProperty("Description_HE")] public string DescriptionHE { get; set; }
        [JsonProperty("Notes")] public string Notes { get; set; }
        [JsonProperty("Notes_DE")] public string NotesDE { get; set; }
        [JsonProperty("Notes_HE")] public string NotesHE { get; set; }
    }

    private class RawDatabaseRootDTO
    {
        [JsonProperty("Sheet1")] public List<RawStoneRowDTO> Sheet1 { get; set; }
        [JsonProperty("Mass Graves")] public List<RawGraveRowDTO> MassGraves { get; set; }
        [JsonProperty("Other Memorials")] public List<RawOtherRowDTO> OtherMemorials { get; set; }
    }

    private List<MemorialStone> stoneList = new List<MemorialStone>();
    private List<MassGrave> graveList = new List<MassGrave>();
    private List<OtherMemorial> otherList = new List<OtherMemorial>();

    private Dictionary<string, MemorialStone> stonesByID = new Dictionary<string, MemorialStone>(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, MassGrave> gravesByID = new Dictionary<string, MassGrave>(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, OtherMemorial> memorialsByID = new Dictionary<string, OtherMemorial>(StringComparer.OrdinalIgnoreCase);

    private bool isLoaded = false;
    public bool IsLoaded => isLoaded;

    public void LoadData()
    {
        if (isLoaded) return;

        string jsonPath = Path.Combine(Application.streamingAssetsPath, "Bergen_Belsen_Database.json");

        if (!File.Exists(jsonPath))
        {
            Debug.LogError($"[DataManager] Critical Error: Database file not found at {jsonPath}");
            return;
        }

        try
        {
            string jsonContent = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
            RawDatabaseRootDTO rawData = JsonConvert.DeserializeObject<RawDatabaseRootDTO>(jsonContent);

            if (rawData == null)
            {
                Debug.LogError("[DataManager] Failed to parse internal flat database structure.");
                return;
            }

            // 1. Process Memorial Stones
            if (rawData.Sheet1 != null)
            {
                foreach (var row in rawData.Sheet1)
                {
                    if (string.IsNullOrEmpty(row.MemorialStone)) continue;

                    if (!stonesByID.TryGetValue(row.MemorialStone, out MemorialStone stone))
                    {
                        var (cleanedLat, cleanedLon) = SanitizeCoordinates(row.Latitude, row.Longitude);

                        stone = new MemorialStone
                        {
                            id = row.MemorialStone,
                            latitude = cleanedLat,
                            longitude = cleanedLon,
                            scan = row.ScanYN == "Y",
                            photo = row.PhotoYN == "Y",
                            geo_ref = row.GeoRefYN == "Y",
                            notes = row.Notes,
                            book_text = row.TextEnglish,
                            symbols = row.Symbols
                        };
                        stone.has_coordinates = (stone.latitude != 0f || stone.longitude != 0f);
                        stonesByID[row.MemorialStone] = stone;
                        stoneList.Add(stone);
                    }

                    int? parsedAge = null;
                    if (row.AgeAtDeath != null && !string.IsNullOrWhiteSpace(row.AgeAtDeath.ToString()))
                    {
                        if (float.TryParse(row.AgeAtDeath.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out float ageVal))
                        {
                            parsedAge = Mathf.RoundToInt(ageVal);
                        }
                    }

                    Person person = new Person
                    {
                        surname = row.Surname,
                        forename = row.Forename,
                        religion = row.Religion,
                        gender = row.Gender,
                        date_of_birth = row.DateOfBirth,
                        place_of_birth = row.PlaceOfBirth,
                        inmate_number = row.InmateNumber,
                        date_of_liberation = row.DateOfLiberation,
                        place_of_liberation = row.PlaceOfLiberation,
                        date_of_death = row.DateOfDeath,
                        age_at_death = parsedAge,
                        place_of_death = row.PlaceOfDeath,
                        other_links = row.Notes,
                        english_inscription = row.Inscriptions?.TextEnglish ?? row.TextEnglish,
                        german_inscription = row.Inscriptions?.TextGerman ?? row.TextGerman,
                        hebrew_inscription = row.Inscriptions?.TextHebrew ?? row.TextHebrew
                    };

                    stone.persons.Add(person);
                }
            }

            // 2. Process Mass Graves
            if (rawData.MassGraves != null)
            {
                foreach (var row in rawData.MassGraves)
                {
                    if (string.IsNullOrEmpty(row.MassGraveId)) continue;

                    string cleanID = row.MassGraveId.Trim();
                    if (!cleanID.StartsWith("MG", StringComparison.OrdinalIgnoreCase)) continue;

                    int parsedDeaths = 0;
                    if (!string.IsNullOrEmpty(row.EstimatedDeaths))
                    {
                        string cleanRow = row.EstimatedDeaths.Replace(",", "").Replace(".", "").Trim();
                        int.TryParse(cleanRow, out parsedDeaths);
                    }

                    var (cleanedLat, cleanedLon) = SanitizeCoordinates(row.Latitude, row.Longitude);

                    MassGrave grave = new MassGrave
                    {
                        id = cleanID,
                        latitude = cleanedLat,
                        longitude = cleanedLon,
                        death_count = parsedDeaths,
                        description = string.IsNullOrEmpty(row.Description) ? $"Mass Grave {cleanID}" : row.Description,
                        notes = row.Notes,
                        has_coordinates = (cleanedLat != 0f || cleanedLon != 0f)
                    };

                    gravesByID[grave.id] = grave;
                    if (!string.IsNullOrEmpty(grave.description))
                    {
                        gravesByID[grave.description.Trim()] = grave;
                    }
                    graveList.Add(grave);
                }
            }

            // 3. Process Other Memorials
            if (rawData.OtherMemorials != null)
            {
                foreach (var row in rawData.OtherMemorials)
                {
                    if (string.IsNullOrEmpty(row.OtherMemNumber)) continue;

                    var (cleanedLat, cleanedLon) = SanitizeCoordinates(row.Latitude, row.Longitude);

                    OtherMemorial memorial = new OtherMemorial
                    {
                        id = row.OtherMemNumber.Trim(),
                        latitude = cleanedLat,
                        longitude = cleanedLon,
                        description = row.Description,
                        notes = row.Notes,
                        has_coordinates = (cleanedLat != 0f || cleanedLon != 0f)
                    };
                    memorialsByID[memorial.id] = memorial;
                    if (!string.IsNullOrEmpty(memorial.description))
                    {
                        memorialsByID[memorial.description.Trim()] = memorial;
                    }
                    otherList.Add(memorial);
                }
            }

            isLoaded = true;
            Debug.Log($"[DataManager] Mobile indices synchronized successfully. Loaded {stonesByID.Count} stones, {gravesByID.Count} graves, {memorialsByID.Count} memorials.");

            rawData = null;
            GC.Collect();
        }
        catch (Exception e)
        {
            Debug.LogError($"[DataManager] Critical database parsing failure: {e.Message}\n{e.StackTrace}");
        }
    }

    private (float lat, float lon) SanitizeCoordinates(object rawLat, object rawLon)
    {
        string latStr = rawLat?.ToString().Trim() ?? "";
        string lonStr = rawLon?.ToString().Trim() ?? "";

        if (latStr.Contains(",") && (latStr.Contains("   ") || string.IsNullOrEmpty(lonStr)))
        {
            string[] blocks = latStr.Split(new string[] { "   ", "  ", " , " }, StringSplitOptions.RemoveEmptyEntries);
            if (blocks.Length >= 2)
            {
                latStr = blocks[0].Trim().TrimEnd(',');
                lonStr = blocks[1].Trim();
            }
            else
            {
                string[] parts = latStr.Split(',');
                if (parts.Length == 4)
                {
                    latStr = parts[0] + "." + parts[1];
                    lonStr = parts[2] + "." + parts[3];
                }
            }
        }

        float lat = ParseSingleCoordinate(latStr);
        float lon = ParseSingleCoordinate(lonStr);
        return (lat, lon);
    }

    private float ParseSingleCoordinate(string input)
    {
        if (string.IsNullOrEmpty(input)) return 0f;
        if (input.Contains(",") && !input.Contains(".")) input = input.Replace(',', '.');
        input = input.Replace(" ", "");

        if (float.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out float result))
        {
            return result;
        }
        return 0f;
    }

    public MemorialStone GetMemorialStoneByID(string id) => stonesByID.TryGetValue(id, out var s) ? s : null;
    public MassGrave GetMassGraveByID(string id) => gravesByID.TryGetValue(id, out var g) ? g : null;
    public OtherMemorial GetOtherMemorialByID(string id) => memorialsByID.TryGetValue(id, out var m) ? m : null;

    public object GetDataByID(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        string cleanId = id.Trim();

        if (cleanId.Contains("0") || cleanId.ToLower().Contains("info stone"))
        {
            if (memorialsByID.TryGetValue("OM0", out var infoMem)) return infoMem;
            if (memorialsByID.TryGetValue("0", out infoMem)) return infoMem;
        }

        if (stonesByID.TryGetValue(cleanId, out var stone)) return stone;
        if (gravesByID.TryGetValue(cleanId, out var grave)) return grave;
        if (memorialsByID.TryGetValue(cleanId, out var memorial)) return memorial;

        // Fallback 1: Strip common prefixes (e.g. "Stone I12" -> "I12", "Memorial AnneFrank" -> "AnneFrank")
        string unprefixedId = cleanId.Replace("Stone ", "").Replace("stone_", "").Replace("Memorial ", "").Trim();
        if (!string.IsNullOrEmpty(unprefixedId) && !string.Equals(unprefixedId, cleanId, StringComparison.OrdinalIgnoreCase))
        {
            if (stonesByID.TryGetValue(unprefixedId, out stone)) return stone;
            if (gravesByID.TryGetValue(unprefixedId, out grave)) return grave;
            if (memorialsByID.TryGetValue(unprefixedId, out memorial)) return memorial;
        }

        // Fallback 2: Strip common suffixes (e.g. "I65 TBC" -> "I65", "C5_2" -> "C5", "C12-1" -> "C12")
        string strippedId = unprefixedId.Split(' ')[0].Split('_')[0].Split('-')[0].Trim();
        if (!string.IsNullOrEmpty(strippedId) && !string.Equals(strippedId, cleanId, StringComparison.OrdinalIgnoreCase))
        {
            if (stonesByID.TryGetValue(strippedId, out stone)) return stone;
            if (gravesByID.TryGetValue(strippedId, out grave)) return grave;
            if (memorialsByID.TryGetValue(strippedId, out memorial)) return memorial;
        }

        // Fallback 2: Check for prefix / case-insensitive matching across all dictionaries
        foreach (var kvp in stonesByID)
        {
            if (kvp.Key.StartsWith(cleanId, StringComparison.OrdinalIgnoreCase) || cleanId.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }
        foreach (var kvp in gravesByID)
        {
            if (kvp.Key.StartsWith(cleanId, StringComparison.OrdinalIgnoreCase) || cleanId.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }
        foreach (var kvp in memorialsByID)
        {
            if (kvp.Key.StartsWith(cleanId, StringComparison.OrdinalIgnoreCase) || cleanId.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }

        return null;
    }

    public List<MemorialStone> GetAllMemorialStones() => stoneList;
    public List<MassGrave> GetAllMassGraves() => graveList;
    public List<OtherMemorial> GetAllOtherMemorials() => otherList;
}