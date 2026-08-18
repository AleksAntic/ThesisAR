using UnityEngine;
using CesiumForUnity;

/// <summary>
/// Utility to quickly switch CesiumGeoreference origin between known evaluation locations.
/// </summary>
public class CesiumOriginSwitcher : MonoBehaviour
{
    [SerializeField] private CesiumGeoreference georeference;

    [Header("🌍 GLB Origin (Bergen-Belsen)")]
    [SerializeField] private double glbLatitude = 52.757620;
    [SerializeField] private double glbLongitude = 9.912300;
    [SerializeField] private double glbHeight = 60.0;

    [Header("🏢 SDU Office Origin")]
    [SerializeField] private double officeLatitude = 55.367643;
    [SerializeField] private double officeLongitude = 10.429753;
    [SerializeField] private double officeHeight = 60.0;

    [Header("⚽ SDU Actual Soccer Field Origin")]
    [SerializeField] private double soccerFieldLatitude = 55.369903;
    [SerializeField] private double soccerFieldLongitude = 10.436850;
    [SerializeField] private double soccerFieldHeight = 60.0;

    void Awake()
    {
        if (georeference == null)
            georeference = Object.FindAnyObjectByType<CesiumGeoreference>(FindObjectsInactive.Include);
    }

    [ContextMenu("Set Origin: GLB (Bergen-Belsen)")]
    public void SetOriginToGlb()
    {
        SetOrigin(glbLongitude, glbLatitude, glbHeight, "GLB (Bergen-Belsen)");
    }

    [ContextMenu("Set Origin: SDU Office")]
    public void SetOriginToOffice()
    {
        SetOrigin(officeLongitude, officeLatitude, officeHeight, "SDU Office");
    }

    [ContextMenu("Set Origin: SDU Soccer Field")]
    public void SetOriginToSoccerField()
    {
        SetOrigin(soccerFieldLongitude, soccerFieldLatitude, soccerFieldHeight, "SDU Soccer Field");
    }

    public void SetOrigin(double latitude, double longitude, double height)
    {
        SetOrigin(longitude, latitude, height, "Custom Coordinates");
    }

    private void SetOrigin(double longitude, double latitude, double height, string label)
    {
        if (georeference == null)
        {
            Debug.LogError("[OriginSwitcher] CesiumGeoreference component not found or unassigned.", this);
            return;
        }

        georeference.SetOriginLongitudeLatitudeHeight(longitude, latitude, height);
        Debug.Log($"[OriginSwitcher] Georeference origin shifted to {label} -> Lat: {latitude}, Lon: {longitude}, Height: {height}", this);
    }
}