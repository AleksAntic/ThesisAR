using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// SINGLE SOURCE OF TRUTH per l'istanza avatar guida.
/// Mantiene SEMPRE un unico GameObject 3D in scena (mai distrutto),
/// e gestisce in unico punto: creazione/riuso, shader/materiali visuali,
/// Animator + AudioSource + NavMeshAgent, visibilità e animazione Talking/Idle.
/// </summary>
public static class ActiveGuideAvatarRegistry
{
    public interface IAvatarOwner
    {
        void ForceDespawnImmediate();
    }

    private static IAvatarOwner currentOwner;
    private static GameObject singleGuideAvatarInstance;
    private static Animator cachedAnimator;
    private static AudioSource cachedAudioSource;
    private static NavMeshAgent cachedNavMeshAgent;
    private static Dictionary<Renderer, Material[]> originalPBRMaterialsMap = new Dictionary<Renderer, Material[]>();
    private static Dictionary<Renderer, Material[]> hologramMaterialsCache = new Dictionary<Renderer, Material[]>();
    private static Shader hologramShader;
    private static bool materialsCached = false;
    private static bool hologramMaterialsBuilt = false;

    public static GameObject SingleAvatarInstance => singleGuideAvatarInstance;
    public static Animator AvatarAnimator => cachedAnimator;
    public static AudioSource AvatarAudioSource => cachedAudioSource;
    private static void CacheOriginalPBRMaterials()
    {
        if (singleGuideAvatarInstance == null) return;
        materialsCached = true;
        originalPBRMaterialsMap.Clear();
        foreach (var rend in singleGuideAvatarInstance.GetComponentsInChildren<Renderer>(true))
        {
            if (rend != null && rend.sharedMaterials != null)
            {
                originalPBRMaterialsMap[rend] = rend.sharedMaterials;
            }
        }
        Debug.Log($"[ActiveGuideAvatarRegistry] Cached ORIGINAL PBR materials for {originalPBRMaterialsMap.Count} renderers (head, hair, body, clothing).");
    }

    private static void CacheHologramMaterials()
    {
        if (singleGuideAvatarInstance == null) return;
        hologramShader = Shader.Find("ThesisAR/HologramURP");
        if (hologramShader == null)
        {
            hologramShader = Shader.Find("Universal Render Pipeline/Lit");
            Debug.LogWarning("[ActiveGuideAvatarRegistry] Shader 'ThesisAR/HologramURP' NOT FOUND by Shader.Find. Falling back to 'Universal Render Pipeline/Lit'.");
        }

        hologramMaterialsCache.Clear();
        foreach (var rend in singleGuideAvatarInstance.GetComponentsInChildren<Renderer>(true))
        {
            if (rend == null) continue;
            Material[] holoMats = new Material[rend.sharedMaterials.Length];
            for (int i = 0; i < rend.sharedMaterials.Length; i++)
            {
                Material m = new Material(hologramShader);
                
                // CRITICAL: Configure material for Transparency so alpha fading works in URP
                m.SetFloat("_Surface", 1f); // Transparent
                m.SetFloat("_Blend", 0f);   // Alpha
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                if (m.HasProperty("_Color")) m.SetColor("_Color", new Color(0.2f, 0.8f, 1.0f, 0.65f));
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.2f, 0.8f, 1.0f, 0.65f));
                holoMats[i] = m;
            }
            hologramMaterialsCache[rend] = holoMats;
        }
        hologramMaterialsBuilt = true;
        Debug.Log($"[ActiveGuideAvatarRegistry] Built hologram material cache for {hologramMaterialsCache.Count} renderers.");
    }

    public static GameObject GetOrCreateAvatar(GameObject prefab)
    {
        if (singleGuideAvatarInstance == null)
        {
            if (prefab == null)
            {
                Debug.LogError("[ActiveGuideAvatarRegistry] GetOrCreateAvatar called with NULL prefab.");
                return null;
            }

            singleGuideAvatarInstance = (GameObject)Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, null);
            singleGuideAvatarInstance.name = "SingleGuideAvatarInstance";
            singleGuideAvatarInstance.SetActive(false);

            cachedAnimator = singleGuideAvatarInstance.GetComponentInChildren<Animator>();
            cachedAudioSource = singleGuideAvatarInstance.GetComponentInChildren<AudioSource>();
            cachedNavMeshAgent = singleGuideAvatarInstance.GetComponentInChildren<NavMeshAgent>();
            if (cachedAnimator != null) cachedAnimator.applyRootMotion = false;
            if (cachedAudioSource != null)
            {
                cachedAudioSource.spatialBlend = 0f;
                cachedAudioSource.playOnAwake = false;
            }

            CacheOriginalPBRMaterials();
            CacheHologramMaterials();
            Debug.Log("[ActiveGuideAvatarRegistry] Created NEW SingleGuideAvatarInstance (persistent).");
        }

        if (cachedAnimator == null) cachedAnimator = singleGuideAvatarInstance.GetComponentInChildren<Animator>();
        if (cachedAudioSource == null) cachedAudioSource = singleGuideAvatarInstance.GetComponentInChildren<AudioSource>();
        if (cachedNavMeshAgent == null) cachedNavMeshAgent = singleGuideAvatarInstance.GetComponentInChildren<NavMeshAgent>();

        return singleGuideAvatarInstance;
    }

    public static void AssignComponents(ref Animator animator, ref AudioSource audio, ref NavMeshAgent agent)
    {
        if (animator == null) animator = cachedAnimator;
        if (audio == null) audio = cachedAudioSource;
        if (agent == null) agent = cachedNavMeshAgent;
    }

    // Backwards-compat alias used by PersonalGuidance/IntermediateGuidance.
    public static void RegisterSingleAvatarInstance(GameObject avatarGO)
    {
        RegisterExistingAvatar(avatarGO);
    }

    public static void RegisterExistingAvatar(GameObject avatarGO)
    {
        if (avatarGO == null) return;
        if (singleGuideAvatarInstance != avatarGO)
        {
            singleGuideAvatarInstance = avatarGO;
            singleGuideAvatarInstance.name = "SingleGuideAvatarInstance";
            cachedAnimator = singleGuideAvatarInstance.GetComponentInChildren<Animator>();
            cachedAudioSource = singleGuideAvatarInstance.GetComponentInChildren<AudioSource>();
            cachedNavMeshAgent = singleGuideAvatarInstance.GetComponentInChildren<NavMeshAgent>();
            if (cachedAnimator != null) cachedAnimator.applyRootMotion = false;
            if (cachedAudioSource != null) { cachedAudioSource.spatialBlend = 0f; cachedAudioSource.playOnAwake = false; }
            CacheOriginalPBRMaterials();
            CacheHologramMaterials();
        }
        else
        {
            Debug.Log($"[ActiveGuideAvatarRegistry] Avatar instance already registered ('{singleGuideAvatarInstance.name}'); reusing persistent instance.");
        }
    }

    private static bool HasStaleKeys<T>(Dictionary<Renderer, T> map)
    {
        if (map == null || map.Count == 0) return true;
        foreach (var key in map.Keys)
        {
            if (key == null) return true;
        }
        return false;
    }

    public static void ApplyPersonalPBRVisuals()
    {
        Debug.Log($"[REGISTRY TRACE] ApplyPersonalPBRVisuals() - instanceNull={singleGuideAvatarInstance == null}");
        if (singleGuideAvatarInstance == null) return;

        if (!materialsCached || HasStaleKeys(originalPBRMaterialsMap))
        {
            CacheOriginalPBRMaterials();
        }

        foreach (var kvp in originalPBRMaterialsMap)
        {
            if (kvp.Key != null && kvp.Value != null)
            {
                kvp.Key.materials = kvp.Value;
            }
        }

        // Enable NavMeshAgent for Personal Mode companion movement
        var agent = cachedNavMeshAgent ?? singleGuideAvatarInstance.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = true;
        }

        // Enable CompanionIKController head-gaze tracking for Personal Mode
        var ik = singleGuideAvatarInstance.GetComponent<CompanionIKController>();
        if (ik != null)
        {
            ik.enabled = true;
            Debug.Log("[REGISTRY TRACE] CompanionIKController set to enabled=true on singleGuideAvatarInstance.");
        }
        else
        {
            Debug.Log("[REGISTRY TRACE] CompanionIKController component not present on singleGuideAvatarInstance yet.");
        }

        Debug.Log("[ActiveGuideAvatarRegistry] Applied Personal PBR textured visuals, enabled NavMeshAgent & CompanionIKController.");
    }

    public static void ApplyIntermediateHologramVisuals()
    {
        Debug.Log($"[REGISTRY TRACE] ApplyIntermediateHologramVisuals() - instanceNull={singleGuideAvatarInstance == null}");
        if (singleGuideAvatarInstance == null) return;

        if (!hologramMaterialsBuilt || HasStaleKeys(hologramMaterialsCache))
        {
            CacheHologramMaterials();
        }

        foreach (var kvp in hologramMaterialsCache)
        {
            if (kvp.Key != null && kvp.Value != null)
            {
                kvp.Key.materials = kvp.Value;
            }
        }

        // Disable NavMeshAgent for Intermediate Mode static hologram placement
        var agent = cachedNavMeshAgent ?? singleGuideAvatarInstance.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = false;
        }

        // Enable CompanionIKController so Intermediate Hologram looks the user in the eye just like Personal Mode
        var ik = singleGuideAvatarInstance.GetComponent<CompanionIKController>();
        if (ik != null)
        {
            ik.enabled = true;
            Debug.Log("[ActiveGuideAvatarRegistry] CompanionIKController enabled for Intermediate Hologram head-gaze tracking.");
        }

        Debug.Log("[ActiveGuideAvatarRegistry] Applied Intermediate URP Hologram cached visuals, disabled NavMeshAgent & enabled CompanionIKController.");
    }

    public static void ClaimOwnership(IAvatarOwner newOwner)
    {
        if (currentOwner != null && !ReferenceEquals(currentOwner, newOwner))
        {
            Debug.Log($"[ActiveGuideAvatarRegistry] Ownership transfer: {currentOwner.GetType().Name} -> {newOwner.GetType().Name}. Forcing despawn of previous owner's avatar.");
            currentOwner.ForceDespawnImmediate();
        }

        currentOwner = newOwner;
    }

    public static void ReleaseOwnership(IAvatarOwner owner)
    {
        if (ReferenceEquals(currentOwner, owner))
        {
            currentOwner = null;
        }
    }

    public static bool HasActiveOwner => currentOwner != null;
}
