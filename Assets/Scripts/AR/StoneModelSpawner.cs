using UnityEngine;
using System.Collections;

/// <summary>
/// Dynamically manages 3D model lifecycle for individual stones.
/// Supports asynchronous local instantiation and on-demand unloading 
/// to maximize mobile RAM availability during heavy AR geospatial tracking.
/// </summary>
public class StoneModelSpawner : MonoBehaviour
{
    [Tooltip("If true (Field Test Simulation), 3D stone models are spawned on the ground. If false (Real Site Deployment), 3D ground models are culled since physical stones exist on-site.")]
    public static bool SimulateStonesInAR = true;

    [SerializeField] private string stoneId;
    [SerializeField] private GameObject defaultFallbackPrefab;

    private GameObject spawnedInstance;
    private bool isModelLoaded = false;
    private bool hasHighFidelityModel = false;

    public string StoneId => stoneId;
    public bool IsModelLoaded => isModelLoaded;
    public bool HasHighFidelityModel => hasHighFidelityModel;

    /// <summary>
    /// Called by RuntimeStonePopulator to supply a shared fallback prefab, but ONLY if this
    /// particular spawner doesn't already have one configured (e.g. hand-placed in the scene
    /// with its own Inspector-assigned fallback, which must never be silently overwritten).
    /// </summary>
    public void SetFallbackPrefabIfUnset(GameObject prefab)
    {
        if (defaultFallbackPrefab == null)
        {
            defaultFallbackPrefab = prefab;
        }
    }

    void Awake()
    {
        EnforceCorrectVisualTransforms();
    }

    void Start()
    {
        EnforceCorrectVisualTransforms();
    }

    public void EnforceCorrectVisualTransforms()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.name.StartsWith("[BakedVisual]_Fallback_"))
            {
                Destroy(child.gameObject);
                continue;
            }
            if (child.name.StartsWith("[BakedVisual]_"))
            {
                child.localScale = new Vector3(20f, 20f, 20f);
                SnapStoneToGround(child.gameObject);
            }
        }
    }

    public static void SnapStoneToGround(GameObject stoneVisual)
    {
        if (stoneVisual == null) return;

        Renderer[] renderers = stoneVisual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        Bounds totalBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            totalBounds.Encapsulate(renderers[i].bounds);
        }

        Transform anchor = stoneVisual.transform.parent;
        Vector3 anchorPos = anchor != null ? anchor.position : stoneVisual.transform.position;
        float targetGroundY = anchorPos.y;

        if (Physics.Raycast(anchorPos + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 30f))
        {
            targetGroundY = hit.point.y;
        }
        else if (UnityEngine.AI.NavMesh.SamplePosition(anchorPos, out UnityEngine.AI.NavMeshHit navHit, 10f, UnityEngine.AI.NavMesh.AllAreas))
        {
            targetGroundY = navHit.position.y;
        }

        float bottomY = totalBounds.min.y;
        float heightCorrection = targetGroundY - bottomY;

        stoneVisual.transform.position += new Vector3(0f, heightCorrection, 0f);
    }

    /// <summary>
    /// Materializes the 3D asset geometry only when the user enters proximity boundaries.
    /// </summary>
    public void LoadModelAsync(string id)
    {
        if (!SimulateStonesInAR) return; // Do not spawn 3D ground stone prefabs on real site
        if (isModelLoaded) return;
        if (!isActiveAndEnabled) return;

        // Check if a baked visual already exists under this anchor AND has a valid mesh
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name.StartsWith("[BakedVisual]_"))
            {
                MeshFilter mf = child.GetComponentInChildren<MeshFilter>(true);
                if (mf != null && mf.sharedMesh != null)
                {
                    Debug.Log($"[StoneModelSpawner] Valid visual already baked under '{name}'. Skipping runtime duplicate spawn.");
                    isModelLoaded = true;
                    hasHighFidelityModel = true;
                    return;
                }
                else
                {
                    Debug.LogWarning($"[StoneModelSpawner] Baked visual under '{name}' has missing/broken mesh. Destroying broken child to load fresh model.");
                    Destroy(child.gameObject);
                }
            }
        }

        this.stoneId = id;
        StartCoroutine(LoadAssetRoutine());
    }

    [Header("🌐 Remote Model Fallback")]
    [Tooltip("Se true, quando il modello non è trovato in Resources (locale), tenta di scaricarlo da GitHub Release prima di ricadere sul fallback procedurale.")]
    [SerializeField] private bool allowRemoteDownloadFallback = true;

    private IEnumerator LoadAssetRoutine()
    {
        Debug.Log($"[StoneModelSpawner] Loading asset asynchronously: Stones/{stoneId} for object: {name}");
        ResourceRequest request = Resources.LoadAsync<GameObject>($"Stones/{stoneId}");
        yield return request;

        GameObject stoneModel = request.asset as GameObject;
        if (stoneModel == null)
        {
            stoneModel = Resources.Load<GameObject>($"Stones/{stoneId}-1") ??
                         Resources.Load<GameObject>($"Stones/{stoneId}_1") ??
                         Resources.Load<GameObject>($"Stones/{stoneId}-2") ??
                         Resources.Load<GameObject>($"Stones/{stoneId}_2");
        }
        Debug.Log($"[StoneModelSpawner] Resources.LoadAsync Result for 'Stones/{stoneId}' is null? {stoneModel == null}");

        bool isHighFidelityValid = false;

        if (stoneModel != null)
        {
            // --- Percorso 1: modello incluso localmente nell'APK ---
            spawnedInstance = Instantiate(stoneModel, transform);
            spawnedInstance.name = $"[BakedVisual]_{stoneId}";
            spawnedInstance.transform.localScale = new Vector3(20f, 20f, 20f);
            SnapStoneToGround(spawnedInstance);

            ApplyCompressedTextureOverrideIfPresent(spawnedInstance);

            MeshFilter mfLocal = spawnedInstance.GetComponentInChildren<MeshFilter>(true);
            if (mfLocal != null && mfLocal.sharedMesh == null)
            {
                Debug.LogWarning($"[StoneModelSpawner] High-fidelity LOCAL model '{stoneId}' has a MISSING mesh reference. Falling back...");
                Destroy(spawnedInstance);
                spawnedInstance = null;
            }
            else
            {
                Debug.Log($"[StoneModelSpawner] Instantiated high-fidelity LOCAL 3D stone model: '{stoneModel.name}' under parent: '{name}'");
                isHighFidelityValid = true;
                hasHighFidelityModel = true;
            }
        }
        else if (allowRemoteDownloadFallback && GitHubAssetDownloader.Instance != null)
        {
            // --- Percorso 2: modello non incluso nell'APK, tentiamo il download da GitHub Release ---
            Debug.Log($"[StoneModelSpawner] '{stoneId}' not found locally. Attempting remote download via GitHubAssetDownloader...");

            bool downloadFinished = false;
            GameObject downloadedRoot = null;

            GitHubAssetDownloader.Instance.DownloadOrCacheModelAsync(stoneId, result =>
            {
                downloadedRoot = result;
                downloadFinished = true;
            });

            while (!downloadFinished) yield return null;

            if (downloadedRoot != null)
            {
                downloadedRoot.transform.SetParent(transform, false);
                downloadedRoot.name = $"[BakedVisual]_{stoneId}";
                downloadedRoot.transform.localScale = new Vector3(20f, 20f, 20f);
                SnapStoneToGround(downloadedRoot);

                spawnedInstance = downloadedRoot;
                ApplyCompressedTextureOverrideIfPresent(spawnedInstance);

                MeshFilter mfRemote = spawnedInstance.GetComponentInChildren<MeshFilter>(true);
                if (mfRemote != null && mfRemote.sharedMesh == null)
                {
                    Debug.LogWarning($"[StoneModelSpawner] Downloaded model '{stoneId}' has a MISSING mesh reference. Falling back...");
                    Destroy(spawnedInstance);
                    spawnedInstance = null;
                }
                else
                {
                    Debug.Log($"[StoneModelSpawner] Instantiated high-fidelity REMOTE (GitHub Release) 3D stone model for: '{name}'");
                    isHighFidelityValid = true;
                    hasHighFidelityModel = true;
                }
            }
            else
            {
                Debug.LogWarning($"[StoneModelSpawner] Remote download for '{stoneId}' failed or unavailable. Using procedural fallback.");
            }
        }

        if (!isHighFidelityValid)
        {
            Debug.LogWarning($"[StoneModelSpawner] No high-fidelity model is available for '{stoneId}'. No placeholder is shown.");
        }

        isModelLoaded = true;
        Debug.Log($"[StoneModelSpawner] Model loading lifecycle successfully complete for: '{stoneId}'");
    }

    private void ApplyCompressedTextureOverrideIfPresent(GameObject targetInstance)
    {
        if (targetInstance == null) return;
        Texture2D compressedTex = Resources.Load<Texture2D>($"Stones_Textures/{stoneId}_Tex");
        if (compressedTex == null) return;

        var renderers = targetInstance.GetComponentsInChildren<MeshRenderer>(true);
        Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
        foreach (var r in renderers)
        {
            if (r.sharedMaterial != null)
            {
                Material mat = new Material(r.sharedMaterial);
                mat.enableInstancing = true;
                if (urpShader != null) mat.shader = urpShader;
                mat.mainTexture = compressedTex;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", compressedTex);
                r.sharedMaterial = mat;
            }
        }
    }

    /// <summary>
    /// Flushes the 3D geometry instance from memory instantly when the user walks away.
    /// </summary>
    public void UnloadModel()
    {
        if (!isModelLoaded) return;

        if (spawnedInstance != null)
        {
            Destroy(spawnedInstance);
            spawnedInstance = null;
        }

        isModelLoaded = false;
    }

    void OnDestroy()
    {
        UnloadModel();
    }
}
