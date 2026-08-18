using UnityEngine;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Shifts the entire world geometry container (GeoRoot) dynamically to warp remote 
/// historical environments directly onto the local soccer field testing origin.
/// Fully synchronized with the asynchronous proximity culling layers to safeguard mobile RAM.
/// </summary>
public class FieldTestManager : MonoBehaviour
{
    [Header("🌍 Target World Root Transformation")]
    [Tooltip("Trascina qui l'oggetto 'GeoRoot' (il padre del GLB e dei punti) così sposteremo il mondo sotto i piedi del telefono.")]
    [SerializeField] private Transform worldRoot;

    [Tooltip("The calibration anchor placed exactly at the real SDU Soccer Field center coordinate.")]
    [SerializeField] private SoccerFieldZone sduFieldCalibrator;

    [Header("📋 Zone Repository")]
    [SerializeField] private List<SoccerFieldZone> availableZones = new List<SoccerFieldZone>();

    [Header("⚙️ UI Injection")]
    [SerializeField] private TMP_Dropdown zoneDropdown;

    private int currentlyActiveIndex = -1;
    private RuntimeStonePopulator stonePopulatorCache;

    void Start()
    {
        if (worldRoot == null)
        {
            GameObject rootObj = GameObject.Find("GeoRoot");
            if (rootObj != null) worldRoot = rootObj.transform;
        }

        if (availableZones.Count == 0)
        {
            availableZones.AddRange(UnityEngine.Object.FindObjectsByType<SoccerFieldZone>(FindObjectsInactive.Include));
            if (sduFieldCalibrator != null) availableZones.Remove(sduFieldCalibrator);
        }

        // Cache the centralized population engine to allow immediate geographical refesh loops
        stonePopulatorCache = UnityEngine.Object.FindAnyObjectByType<RuntimeStonePopulator>(FindObjectsInactive.Include);

        PopulateZoneDropdown();
    }

    private void PopulateZoneDropdown()
    {
        if (zoneDropdown == null) return;

        zoneDropdown.ClearOptions();
        List<string> options = new List<string> { "Select Testing Zone..." };

        foreach (SoccerFieldZone zone in availableZones)
            options.Add(zone.zoneName);

        zoneDropdown.AddOptions(options);
        zoneDropdown.onValueChanged.RemoveAllListeners();
        zoneDropdown.onValueChanged.AddListener(OnDropdownIndexChanged);
    }

    private void OnDropdownIndexChanged(int index)
    {
        if (index == 0) ResetAllZoneColors();
        else ActivateZoneAndShiftWorld(index - 1);
    }

    /// <summary>
    /// Shifts and rotates the entire world hierarchy to align the selected Bergen-Belsen zone 
    /// perfectly on top of the real physical SDU Soccer field coordinates.
    /// Synchronizes changes instantly with spatial streaming sub-systems.
    /// </summary>
    public void ActivateZoneAndShiftWorld(int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= availableZones.Count || sduFieldCalibrator == null || worldRoot == null) return;

        // ⚡ MEMORY LIFECYCLE PARITY: Flush all loaded 3D models before mutating coordinate anchors 
        // to prevent asset drifting ghost instances from leaking inside the old location bounds.
        if (stonePopulatorCache != null)
        {
            stonePopulatorCache.FlushAllLoadedModels();
        }

        currentlyActiveIndex = targetIndex;
        SoccerFieldZone selectedZone = availableZones[currentlyActiveIndex];

        // 1. Update line rendering feedback states
        for (int i = 0; i < availableZones.Count; i++)
        {
            availableZones[i].UpdateBoundsVisualization(i == currentlyActiveIndex);
        }

        // 2. WORLD MATHEMATICS: Compute precise delta rotation to lock coordinates onto the physical field orientation
        Quaternion deltaRot = sduFieldCalibrator.transform.rotation * Quaternion.Inverse(selectedZone.transform.rotation);
        worldRoot.rotation = deltaRot * worldRoot.rotation;

        // 3. Translate the root anchor under the origin world zero tracking frame
        Vector3 deltaPos = sduFieldCalibrator.transform.position - selectedZone.transform.position;
        worldRoot.position += deltaPos;

        Debug.Log($"[Warp Engine] World 'GeoRoot' warped successfully. Zone '{selectedZone.zoneName}' is now locked over SDU field coordinates.");

        // Force garbage collector sweep right after coordinate transformation is finalized
        System.GC.Collect();
    }

    private void ResetAllZoneColors()
    {
        if (stonePopulatorCache != null)
        {
            stonePopulatorCache.FlushAllLoadedModels();
        }

        currentlyActiveIndex = -1;
        foreach (SoccerFieldZone zone in availableZones)
        {
            if (zone != null) zone.UpdateBoundsVisualization(false);
        }
    }
}
