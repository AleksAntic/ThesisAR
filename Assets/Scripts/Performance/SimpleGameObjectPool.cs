using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight generic structural memory pool for GameObjects to mitigate memory allocation 
/// spikes and prevent runtime CPU garbage collection stutter during performance-heavy AR loops.
/// </summary>
public class SimpleGameObjectPool
{
    private readonly Queue<GameObject> pool = new Queue<GameObject>();
    private readonly HashSet<GameObject> currentlyPooled = new HashSet<GameObject>();
    private readonly GameObject prefab;
    private readonly Transform parent;

    public SimpleGameObjectPool(GameObject prefab, int prewarmCount, Transform parent = null)
    {
        this.prefab = prefab;
        this.parent = parent;

        for (int i = 0; i < prewarmCount; i++)
        {
            var instance = CreateInstance();
            instance.SetActive(false);
            pool.Enqueue(instance);
            currentlyPooled.Add(instance);
        }
    }

    /// <summary>
    /// Retrieves an inactive instance from the queue pool or dynamically creates one if empty.
    /// </summary>
    public GameObject Get(Vector3 position, Quaternion rotation)
    {
        GameObject instance = pool.Count > 0 ? pool.Dequeue() : CreateInstance();
        currentlyPooled.Remove(instance);
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);
        return instance;
    }

    /// <summary>
    /// Deactivates the instance and stores it back in the pool for future reuse.
    /// </summary>
    public void Release(GameObject instance)
    {
        if (instance == null)
            return;

        if (currentlyPooled.Contains(instance))
        {
            Debug.LogWarning($"[SimpleGameObjectPool] '{instance.name}' released twice without an intermediate Get(). Ignoring duplicate release.");
            return;
        }

        instance.SetActive(false);
        instance.transform.SetParent(parent, false);
        pool.Enqueue(instance);
        currentlyPooled.Add(instance);
    }

    private GameObject CreateInstance()
    {
        var instance = Object.Instantiate(prefab, parent);
        instance.name = prefab.name;
        return instance;
    }
}