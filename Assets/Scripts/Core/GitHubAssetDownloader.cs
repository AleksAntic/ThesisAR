using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using GLTFast;

/// <summary>
/// Gestisce il download, la cache su disco, e l'importazione runtime dei modelli 3D delle lapidi
/// pubblicati come asset di una GitHub Release. Sostituisce la dipendenza rigida da Resources.LoadAsync
/// per i modelli ad alta fedeltà, permettendo di NON includerli tutti dentro l'APK.
/// </summary>
public class GitHubAssetDownloader : MonoBehaviour
{
    [Header("📦 GitHub Release Source")]
    [Tooltip("Nome utente o organizzazione GitHub proprietaria del repository con i modelli.")]
    [SerializeField] private string githubRepoOwner = "AleksAntic";
    [Tooltip("Nome del repository GitHub che ospita la Release con i file .glb.")]
    [SerializeField] private string githubRepoName = "thesisar-stone-models";
    [Tooltip("Tag della release da cui scaricare (es. 'v1.0-models').")]
    [SerializeField] private string releaseTag = "v1.0-models";

    [Header("💾 Local Disk Cache")]
    [SerializeField] private string localCacheFolderName = "StoneModelsCache";

    [Header("⚙️ Settings")]
    [Tooltip("Timeout in secondi per la richiesta di download.")]
    [SerializeField] private int downloadTimeoutSeconds = 30;

    private string CacheDirectory => Path.Combine(Application.persistentDataPath, localCacheFolderName);

    public static GitHubAssetDownloader Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<GitHubAssetDownloader>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("GitHubAssetDownloader");
                    _instance = go.AddComponent<GitHubAssetDownloader>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }
    private static GitHubAssetDownloader _instance;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        if (!Directory.Exists(CacheDirectory))
        {
            Directory.CreateDirectory(CacheDirectory);
        }
    }

    /// <summary>
    /// Punto di ingresso pubblico. Restituisce, tramite callback, un GameObject pronto per essere
    /// parentato/posizionato nella scena, oppure null se il download o l'import falliscono per
    /// qualunque motivo.
    /// </summary>
    public void DownloadOrCacheModelAsync(string stoneId, Action<GameObject> onComplete)
    {
        StartCoroutine(DownloadOrCacheModelRoutine(stoneId, onComplete));
    }

    private IEnumerator DownloadOrCacheModelRoutine(string stoneId, Action<GameObject> onComplete)
    {
        string localPath = Path.Combine(CacheDirectory, $"{stoneId}.glb");

        if (!File.Exists(localPath))
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                Debug.LogWarning($"[GitHubAssetDownloader] No network connection. Cannot fetch '{stoneId}.glb' and no cached copy exists.");
                onComplete?.Invoke(null);
                yield break;
            }

            string downloadUrl = $"https://github.com/{githubRepoOwner}/{githubRepoName}/releases/download/{releaseTag}/{stoneId}.glb";
            string tempPath = localPath + ".part";

            using (UnityWebRequest request = UnityWebRequest.Get(downloadUrl))
            {
                request.downloadHandler = new DownloadHandlerFile(tempPath);
                request.timeout = downloadTimeoutSeconds;

                Debug.Log($"[GitHubAssetDownloader] Downloading '{stoneId}.glb' from {downloadUrl}");
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[GitHubAssetDownloader] Download failed for '{stoneId}.glb': {request.error} (HTTP {request.responseCode})");
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                    onComplete?.Invoke(null);
                    yield break;
                }
            }

            if (File.Exists(localPath)) File.Delete(localPath);
            File.Move(tempPath, localPath);
            Debug.Log($"[GitHubAssetDownloader] Cached '{stoneId}.glb' locally at: {localPath}");
        }
        else
        {
            Debug.Log($"[GitHubAssetDownloader] Using cached model for '{stoneId}' (no network request needed).");
        }

        Task<GameObject> importTask = ImportGlbAsync(localPath, stoneId);
        while (!importTask.IsCompleted) yield return null;

        if (importTask.IsFaulted || importTask.Result == null)
        {
            Debug.LogError($"[GitHubAssetDownloader] glTF import failed for '{stoneId}': {importTask.Exception?.InnerException?.Message ?? "unknown error"}");

            if (File.Exists(localPath)) File.Delete(localPath);

            onComplete?.Invoke(null);
            yield break;
        }

        onComplete?.Invoke(importTask.Result);
    }

    private async Task<GameObject> ImportGlbAsync(string localFilePath, string stoneId)
    {
        var gltf = new GltfImport();
        bool loaded = await gltf.Load(new Uri(localFilePath).AbsoluteUri);
        if (!loaded) return null;

        GameObject root = new GameObject($"[GLB_Downloaded]_{stoneId}");
        bool instantiated = await gltf.InstantiateMainSceneAsync(root.transform);

        if (!instantiated)
        {
            UnityEngine.Object.Destroy(root);
            return null;
        }

        return root;
    }

    /// <summary>Svuota completamente la cache locale dei modelli.</summary>
    public void ClearCache()
    {
        if (Directory.Exists(CacheDirectory))
        {
            Directory.Delete(CacheDirectory, true);
            Directory.CreateDirectory(CacheDirectory);
            Debug.Log("[GitHubAssetDownloader] Local model cache cleared.");
        }
    }

    /// <summary>Dimensione totale corrente della cache su disco, in MB.</summary>
    public float GetCacheSizeMB()
    {
        if (!Directory.Exists(CacheDirectory)) return 0f;

        long totalBytes = 0;
        foreach (string file in Directory.GetFiles(CacheDirectory))
        {
            totalBytes += new FileInfo(file).Length;
        }
        return totalBytes / (1024f * 1024f);
    }
}
