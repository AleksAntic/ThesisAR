using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Automatically scans the active hierarchy at startup to inject spawner registries,
/// implementing a centralized geographical culling loop to load/unload 3D models based on proximity.
/// Maintained strictly in English to preserve project constraints.
/// </summary>
public class RuntimeStonePopulator : MonoBehaviour
{
    [Header("⚙️ Target Tracking References")]
    [SerializeField] private Transform userCameraTransform;

    [Header("📊 Proximity Culling Parameters")]
    [Tooltip("Distance threshold (in meters) to dynamically load the high-fidelity 3D model into RAM.")]
    [SerializeField] private float activationRadiusMeters = 30f;
    [Tooltip("How often (in seconds) the proximity metrics are evaluated across all registry nodes.")]
    [SerializeField] private float checkIntervalSeconds = 0.5f;

    [Header("📦 Fallback Templates")]
    [SerializeField] private GameObject defaultStoneFallbackPrefab;
    [Tooltip("Only used if 'Default Stone Fallback Prefab' above is left empty: tries to load one from this Resources path exactly once at startup, then reuses that single reference for every spawner instead of loading it again per-stone.")]
    [SerializeField] private string fallbackPrefabResourcesPath = "Prefabs/DefaultStoneFallback";

    private readonly List<StoneModelSpawner> registeredSpawners = new List<StoneModelSpawner>();
    private bool isSystemInitialized = false;

    void Start()
    {
        if (userCameraTransform == null && Camera.main != null)
        {
            userCameraTransform = Camera.main.transform;
        }

        ResolveFallbackPrefabOnce();
        InitializeRegistryChannels();
        StartCoroutine(PeriodicRescanForLateSpawnedStones());
    }

    /// <summary>
    /// The initial scan at Start() only catches "point_" objects that already exist in the
    /// scene at that exact moment. If stones/graves stream in later (e.g. Cesium tiles/GLB
    /// content loading asynchronously after scene start, which is common), those would never
    /// get registered and would silently never load their 3D model no matter how close the
    /// user gets. This keeps re-scanning periodically (much less often than the distance
    /// culling check) to pick up anything that appeared since the last scan.
    /// </summary>
    private IEnumerator PeriodicRescanForLateSpawnedStones()
    {
        var rescanInterval = new WaitForSeconds(3f);
        while (true)
        {
            yield return rescanInterval;
            RegisterAnyNewlyFoundStones();
        }
    }

    private void RegisterAnyNewlyFoundStones()
    {
        // Search specifically for Transforms with "point_" prefix instead of allocating all GameObjects in scene
        Transform[] allTransforms = transform.root != null ? transform.root.GetComponentsInChildren<Transform>(true) : FindObjectsByType<Transform>();
        int newlyRegistered = 0;

        foreach (Transform tr in allTransforms)
        {
            if (tr == null || !tr.name.StartsWith("point_")) continue;
            if (tr.GetComponent<StoneModelSpawner>() != null) continue; // Already registered

            StoneModelSpawner spawner = tr.gameObject.AddComponent<StoneModelSpawner>();
            if (defaultStoneFallbackPrefab != null)
            {
                spawner.SetFallbackPrefabIfUnset(defaultStoneFallbackPrefab);
            }
            registeredSpawners.Add(spawner);
            newlyRegistered++;
        }

        if (newlyRegistered > 0)
        {
            Debug.Log($"[Stone Populator] Late-scan found {newlyRegistered} additional 'point_' objects that didn't exist at Start(). Total tracked: {registeredSpawners.Count}.");
        }
    }

    /// <summary>
    /// Resolves the shared fallback prefab exactly once, so it's never re-loaded per-stone.
    /// Prefers whatever is already assigned in the Inspector; only falls back to a single
    /// Resources.Load if that's empty. Every StoneModelSpawner created below receives this
    /// SAME resolved reference (or none, if both sources come up empty — in which case each
    /// spawner still has its own last-resort procedural cube, unchanged from before).
    /// </summary>
    private void ResolveFallbackPrefabOnce()
    {
        if (defaultStoneFallbackPrefab != null) return;

        if (string.IsNullOrEmpty(fallbackPrefabResourcesPath))
        {
            Debug.LogWarning("[Stone Populator] No 'Default Stone Fallback Prefab' assigned and no Resources path configured — spawners without a high-fidelity model will fall back to a plain procedural cube.");
            return;
        }

        defaultStoneFallbackPrefab = Resources.Load<GameObject>(fallbackPrefabResourcesPath);

        if (defaultStoneFallbackPrefab != null)
        {
            Debug.Log($"[Stone Populator] Resolved shared fallback prefab from Resources/{fallbackPrefabResourcesPath}.");
        }
        else
        {
            Debug.LogWarning($"[Stone Populator] Could not find a fallback prefab at Resources/{fallbackPrefabResourcesPath}. Spawners without a high-fidelity model will fall back to a plain procedural cube. Either assign 'Default Stone Fallback Prefab' directly in the Inspector, or place a prefab at that exact Resources path.");
        }
    }

    private void InitializeRegistryChannels()
    {
        // Find all GameObjects in the scene (including inactive placeholders)
        GameObject[] allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        int registryCount = 0;

        foreach (GameObject obj in allObjects)
        {
            if (obj != null && obj.name.StartsWith("point_"))
            {
                string cleanID = obj.name.Replace("point_", "").Trim();

                // Inject the Spawner abstraction layer cleanly without materializing assets yet
                StoneModelSpawner spawner = obj.GetComponent<StoneModelSpawner>() ?? obj.AddComponent<StoneModelSpawner>();

                // Propagate the single resolved fallback prefab — but only fills it in if this
                // spawner doesn't already have its own manually-assigned fallback in the Inspector.
                if (defaultStoneFallbackPrefab != null)
                {
                    spawner.SetFallbackPrefabIfUnset(defaultStoneFallbackPrefab);
                }

                registeredSpawners.Add(spawner);
                registryCount++;
            }
        }

        Debug.Log($"[Stone Populator] Registry complete. {registryCount} structural tracking channels armed and ready.");

        isSystemInitialized = true;
        StartCoroutine(GeographicalCullingLoop());
    }

    /// <summary>
    /// Throttled background execution thread processing real world coordinate tracking metrics.
    /// </summary>
    private IEnumerator GeographicalCullingLoop()
    {
        var waitInterval = new WaitForSeconds(checkIntervalSeconds);

        while (true)
        {
            if (!isSystemInitialized || userCameraTransform == null)
            {
                yield return waitInterval;
                continue;
            }

            Vector3 userPosition = userCameraTransform.position;

            // Step through all 202 nodes linearly without allocations
            for (int i = 0; i < registeredSpawners.Count; i++)
            {
                StoneModelSpawner spawner = registeredSpawners[i];
                if (spawner == null) continue;

                float currentDistance = Vector3.Distance(userPosition, spawner.transform.position);

                if (currentDistance <= activationRadiusMeters)
                {
                    // User is close: stream asset into RAM asynchronously
                    if (!spawner.IsModelLoaded)
                    {
                        spawner.LoadModelAsync(spawner.transform.name.Replace("point_", "").Trim());
                    }
                }
                else
                {
                    // User walked away: immediately purge asset from memory to reclaim RAM heap bounds
                    if (spawner.IsModelLoaded)
                    {
                        spawner.UnloadModel();
                    }
                }
            }

            yield return waitInterval;
        }
    }

    /// <summary>
    /// Evaluates proximity distance checks instantly (useful for manual teleportation and warp events).
    /// </summary>
    public void ForceInstantDistanceCheck()
    {
        if (userCameraTransform == null) return;
        Vector3 userPosition = userCameraTransform.position;

        for (int i = 0; i < registeredSpawners.Count; i++)
        {
            StoneModelSpawner spawner = registeredSpawners[i];
            if (spawner == null) continue;

            float currentDistance = Vector3.Distance(userPosition, spawner.transform.position);

            if (currentDistance <= activationRadiusMeters)
            {
                if (!spawner.IsModelLoaded)
                {
                    spawner.LoadModelAsync(spawner.transform.name.Replace("point_", "").Trim());
                }
            }
            else
            {
                if (spawner.IsModelLoaded)
                {
                    spawner.UnloadModel();
                }
            }
        }
    }

    /// <summary>
    /// Emergency utility method to force flush the entire loaded matrix structure instantly.
    /// </summary>
    public void FlushAllLoadedModels()
    {
        for (int i = 0; i < registeredSpawners.Count; i++)
        {
            if (registeredSpawners[i] != null && registeredSpawners[i].IsModelLoaded)
            {
                registeredSpawners[i].UnloadModel();
            }
        }
    }
}
