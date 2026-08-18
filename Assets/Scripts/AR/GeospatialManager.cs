using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Google.XR.ARCoreExtensions;
using System;
using CesiumForUnity;
using Unity.Mathematics;

/// <summary>
/// Manages geospatial positioning using ARCore Geospatial API and Terrain Anchors.
/// Includes localized geofencing calculations to determine user boundary compliance.
/// </summary>
public class GeospatialManager : MonoBehaviour
{
    [SerializeField] private ARSession arSession;
    [SerializeField] private AREarthManager earthManager;
    [SerializeField] private ARAnchorManager anchorManager;

    [SerializeField] private bool useTerrainAnchors = true;
    [SerializeField] private float horizontalAccuracyThreshold = 5.0f;
    [SerializeField] private float headingAccuracyThreshold = 15.0f;

    [Header("🖥️ PC Simulation Setup")]
    [SerializeField] private bool simulateOnPC = true;
    [SerializeField] private CesiumGeoreference cesiumGeoreference;
    [SerializeField] private Transform userCameraTransform;

    [Header("Geofencing Memorial Settings")]
    [SerializeField] private double memorialCenterLatitude = 52.7571; // Bergen-Belsen baseline coordinates placeholder
    [SerializeField] private double memorialCenterLongitude = 9.9175;
    [SerializeField] private double allowedBoundaryRadiusMeters = 500.0; // Perimeter radius threshold

    public bool IsGeospatialReady { get; private set; }
    public double? CurrentLatitude { get; private set; }
    public double? CurrentLongitude { get; private set; }
    public double? CurrentAltitude { get; private set; }
    public float CurrentHorizontalAccuracy { get; private set; }
    public float CurrentVerticalAccuracy { get; private set; }
    public float CurrentHeading { get; private set; }
    public float CurrentHeadingAccuracy { get; private set; }
    public TrackingState EarthTrackingState { get; private set; }
    public VpsAvailability CurrentVpsAvailability { get; private set; } = VpsAvailability.Unknown;

#if UNITY_ANDROID && !UNITY_EDITOR
    private Coroutine vpsAvailabilityRoutine;
    private float nextVpsAvailabilityCheckTime;
#endif

    void Awake()
    {
        if (arSession == null) arSession = UnityEngine.Object.FindAnyObjectByType<ARSession>(FindObjectsInactive.Include);
        if (earthManager == null) earthManager = UnityEngine.Object.FindAnyObjectByType<AREarthManager>(FindObjectsInactive.Include);
        if (anchorManager == null) anchorManager = UnityEngine.Object.FindAnyObjectByType<ARAnchorManager>(FindObjectsInactive.Include);

#if UNITY_EDITOR || UNITY_STANDALONE
        if (cesiumGeoreference == null) cesiumGeoreference = UnityEngine.Object.FindAnyObjectByType<CesiumGeoreference>(FindObjectsInactive.Include);
        if (userCameraTransform == null) userCameraTransform = Camera.main != null ? Camera.main.transform : null;
#endif
    }

    void Update()
    {
        UpdateGeospatialState();
    }

    private void UpdateGeospatialState()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (simulateOnPC)
        {
            if (cesiumGeoreference == null) cesiumGeoreference = UnityEngine.Object.FindAnyObjectByType<CesiumGeoreference>(FindObjectsInactive.Include);
            
            if (userCameraTransform == null && Camera.main != null)
                userCameraTransform = Camera.main.transform;

            if (cesiumGeoreference != null && userCameraTransform != null)
            {
                Vector3 pos = userCameraTransform.position;
                double3 cameraPosDouble = new double3((double)pos.x, (double)pos.y, (double)pos.z);
                double3 ecef = cesiumGeoreference.TransformUnityPositionToEarthCenteredEarthFixed(cameraPosDouble);
                double3 lonLatHeight = CesiumWgs84Ellipsoid.EarthCenteredEarthFixedToLongitudeLatitudeHeight(ecef);

                CurrentLatitude = lonLatHeight.y; // Latitude is Y
                CurrentLongitude = lonLatHeight.x; // Longitude is X
                CurrentAltitude = lonLatHeight.z; // Altitude is Z
                CurrentHorizontalAccuracy = 0.5f;
                CurrentVerticalAccuracy = 0.5f;
                CurrentHeading = userCameraTransform.eulerAngles.y;
                CurrentHeadingAccuracy = 1.0f;
                IsGeospatialReady = true;
                EarthTrackingState = TrackingState.Tracking;
                return;
            }
        }
#endif

        if (earthManager == null || earthManager.EarthTrackingState != TrackingState.Tracking)
        {
            IsGeospatialReady = false;
            EarthTrackingState = earthManager != null ? earthManager.EarthTrackingState : TrackingState.None;
            return;
        }

        EarthTrackingState = earthManager.EarthTrackingState;
        GeospatialPose pose = earthManager.CameraGeospatialPose;

        CurrentLatitude = pose.Latitude;
        CurrentLongitude = pose.Longitude;
        CurrentAltitude = pose.Altitude;
        CurrentHorizontalAccuracy = (float)pose.HorizontalAccuracy;
        CurrentVerticalAccuracy = (float)pose.VerticalAccuracy;
        CurrentHeading = pose.EunRotation.eulerAngles.y;
        CurrentHeadingAccuracy = (float)pose.OrientationYawAccuracy;

        IsGeospatialReady = true;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (vpsAvailabilityRoutine == null && Time.unscaledTime >= nextVpsAvailabilityCheckTime)
        {
            vpsAvailabilityRoutine = StartCoroutine(CheckVpsAvailability(CurrentLatitude.Value, CurrentLongitude.Value));
        }
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private System.Collections.IEnumerator CheckVpsAvailability(double latitude, double longitude)
    {
        nextVpsAvailabilityCheckTime = Time.unscaledTime + 60f;

        if (!Input.location.isEnabledByUser)
        {
            CurrentVpsAvailability = VpsAvailability.Unknown;
            vpsAvailabilityRoutine = null;
            yield break;
        }

        if (Input.location.status == LocationServiceStatus.Stopped) Input.location.Start();

        float timeout = Time.realtimeSinceStartup + 10f;
        while (Input.location.status == LocationServiceStatus.Initializing && Time.realtimeSinceStartup < timeout)
            yield return null;

        if (Input.location.status == LocationServiceStatus.Running)
        {
            VpsAvailabilityPromise request = AREarthManager.CheckVpsAvailabilityAsync(latitude, longitude);
            yield return request;
            CurrentVpsAvailability = request.Result;
        }
        else
        {
            CurrentVpsAvailability = VpsAvailability.Unknown;
        }

        vpsAvailabilityRoutine = null;
    }
#endif

    /// <summary>
    /// Evaluates if the user's current geospatial coordinate matrix falls within the designated memorial boundary layout.
    /// </summary>
    public bool IsUserInsideMemorialBoundaries()
    {
        if (!IsGeospatialReady || !CurrentLatitude.HasValue || !CurrentLongitude.HasValue)
        {
            return false;
        }

        double distanceToCenter = CalculateHaversineDistance(
            CurrentLatitude.Value,
            CurrentLongitude.Value,
            memorialCenterLatitude,
            memorialCenterLongitude
        );

        return distanceToCenter <= allowedBoundaryRadiusMeters;
    }

    /// <summary>
    /// Employs high-precision Haversine mathematical formulas to compute surface distances in meters between two GPS coordinates.
    /// </summary>
    private double CalculateHaversineDistance(double lat1, double lon1, double lat2, double lon2)
    {
        double EarthRadiusMeters = 6371000.0; // Mean radius of the earth

        double dLat = (lat2 - lat1) * Mathf.Deg2Rad;
        double dLon = (lon2 - lon1) * Mathf.Deg2Rad;

        double a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                   Math.Cos(lat1 * Mathf.Deg2Rad) * Math.Cos(lat2 * Mathf.Deg2Rad) *
                   Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);

        double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return EarthRadiusMeters * c;
    }

    public void CreateAnchorAsync(double latitude, double longitude, double altitudeAboveTerrain, Quaternion rotation, System.Action<ARGeospatialAnchor> onComplete)
    {
        if (earthManager == null || anchorManager == null || !IsGeospatialReady)
        {
            Debug.LogWarning("[GeospatialManager] Geospatial not ready, cannot create anchor.");
            onComplete?.Invoke(null);
            return;
        }

        if (useTerrainAnchors && HasGoodAccuracy())
        {
            try
            {
                var promise = anchorManager.ResolveAnchorOnTerrainAsync(latitude, longitude, altitudeAboveTerrain, rotation);
                StartCoroutine(WaitForTerrainAnchorPromise(promise, onComplete));
                return;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[GeospatialManager] Terrain anchor resolution failed: {ex.Message}");
            }
        }

        try
        {
            ARGeospatialAnchor fallbackAnchor = anchorManager.AddAnchor(latitude, longitude, altitudeAboveTerrain, rotation);
            onComplete?.Invoke(fallbackAnchor);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[GeospatialManager] Failed to create fallback anchor: {ex.Message}");
            onComplete?.Invoke(null);
        }
    }

    private System.Collections.IEnumerator WaitForTerrainAnchorPromise(ResolveAnchorOnTerrainPromise promise, System.Action<ARGeospatialAnchor> onComplete)
    {
        if (promise == null) { onComplete?.Invoke(null); yield break; }
        while (promise.State == PromiseState.Pending) yield return null;

        if (promise.State == PromiseState.Done && promise.Result != null && promise.Result.Anchor != null)
        {
            onComplete?.Invoke(promise.Result.Anchor);
        }
        else
        {
            onComplete?.Invoke(null);
        }
    }

    public bool HasGoodAccuracy()
    {
        return IsGeospatialReady &&
               CurrentHorizontalAccuracy <= horizontalAccuracyThreshold &&
               CurrentHeadingAccuracy <= headingAccuracyThreshold;
    }

    public bool IsPositioningReliable() => HasGoodAccuracy();
}
