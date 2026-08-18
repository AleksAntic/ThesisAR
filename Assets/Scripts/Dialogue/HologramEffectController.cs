using UnityEngine;

/// <summary>
/// Lightweight, pipeline-agnostic "hologram" visual effect for the Intermediate guidance avatar.
/// Works purely through script-driven material property changes (alpha flicker, emission pulse,
/// subtle vertical jitter) — no custom shader required, so it works unmodified under Built-in,
/// URP, or HDRP as long as the material supports transparency and (optionally) emission.
///
/// REQUIREMENTS ON THE MATERIAL (set these once in the Editor on the avatar's material/prefab):
///   - Rendering Mode / Surface Type = Transparent (Built-in: "Rendering Mode" = Fade or Transparent;
///     URP/HDRP Lit: "Surface Type" = Transparent). Without this, alpha changes have no visible effect.
///   - If you want the emission pulse too, enable "Emission" on the material and give it a base color;
///     if Emission isn't enabled, this script simply skips that part safely.
/// </summary>
public class HologramEffectController : MonoBehaviour
{
    [Header("🎨 Target Renderer")]
    [SerializeField] private Renderer targetRenderer;

    [Header("👁️ Flicker (opacity)")]
    [SerializeField] private float baseAlpha = 0.6f;
    [SerializeField] private float flickerAmplitude = 0.05f;
    [SerializeField] private float flickerSpeed = 1.0f;
    [Tooltip("Chance per second of a brief, more noticeable glitch dropout in opacity.")]
    [SerializeField] private float glitchChancePerSecond = 0.0f;
    [SerializeField] private float glitchDropAmount = 0.15f;
    [SerializeField] private float glitchDuration = 0.08f;

    [Header("💡 Emission Pulse (optional, skipped if material has no emission)")]
    [SerializeField] private bool pulseEmission = false;
    [SerializeField] private Color emissionColor = new Color(0f, 0.7f, 1f);
    [SerializeField] private float emissionMinIntensity = 0.8f;
    [SerializeField] private float emissionMaxIntensity = 1.0f;
    [SerializeField] private float emissionSpeed = 0.5f;

    [Header("↕️ Positional Jitter")]
    [SerializeField] private bool jitterPosition = true;
    [SerializeField] private float jitterAmplitude = 0.01f;
    [SerializeField] private float jitterSpeed = 6f;

    private Material runtimeMaterial;
    private bool hasEmissionProperty;
    private Vector3 localBasePosition;
    private float glitchTimer = 0f;
    private float currentGlitchOffset = 0f;
    private float noiseSeed;
    private float initialBaseAlpha;

    private void Awake()
    {
        initialBaseAlpha = baseAlpha;
        if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();
        noiseSeed = Random.Range(0f, 1000f);

        if (targetRenderer != null)
        {
            // .material (not .sharedMaterial) instantiates a per-object copy so we never
            // permanently alter the shared prefab/material asset.
            runtimeMaterial = targetRenderer.material;
            hasEmissionProperty = runtimeMaterial.HasProperty("_EmissionColor");

            if (hasEmissionProperty)
            {
                runtimeMaterial.EnableKeyword("_EMISSION");
            }
        }

        localBasePosition = transform.localPosition;
    }

    public void SetAlphaMultiplier(float multiplier)
    {
        baseAlpha = initialBaseAlpha * multiplier;
    }

    private void Update()
    {
        if (runtimeMaterial == null) return;

        UpdateAlphaFlicker();

        if (pulseEmission && hasEmissionProperty)
        {
            UpdateEmissionPulse();
        }

        if (jitterPosition)
        {
            UpdatePositionJitter();
        }
    }

    private void UpdateAlphaFlicker()
    {
        // Smooth base flicker via Perlin noise (less mechanical than a pure sine wave)
        float smoothNoise = Mathf.PerlinNoise(noiseSeed, Time.time * flickerSpeed);
        float alpha = baseAlpha + (smoothNoise - 0.5f) * 2f * flickerAmplitude;

        // Occasional short "signal drop" glitch
        if (glitchTimer <= 0f && Random.value < glitchChancePerSecond * Time.deltaTime)
        {
            glitchTimer = glitchDuration;
            currentGlitchOffset = glitchDropAmount;
        }

        if (glitchTimer > 0f)
        {
            glitchTimer -= Time.deltaTime;
            alpha -= currentGlitchOffset;
        }

        alpha = Mathf.Clamp01(alpha);

        Color c = runtimeMaterial.color;
        c.a = alpha;
        runtimeMaterial.color = c;
    }

    private void UpdateEmissionPulse()
    {
        float t = (Mathf.Sin(Time.time * emissionSpeed) + 1f) * 0.5f; // 0..1
        float intensity = Mathf.Lerp(emissionMinIntensity, emissionMaxIntensity, t);
        runtimeMaterial.SetColor("_EmissionColor", emissionColor * intensity);
    }

    private void UpdatePositionJitter()
    {
        float offsetY = (Mathf.PerlinNoise(noiseSeed + 5f, Time.time * jitterSpeed) - 0.5f) * 2f * jitterAmplitude;
        transform.localPosition = localBasePosition + new Vector3(0f, offsetY, 0f);
    }

    /// <summary>
    /// Call if the avatar's base position changes (e.g., teleported next to a new memorial)
    /// so jitter continues around the new resting position instead of the old one.
    /// </summary>
    public void RefreshBasePosition()
    {
        localBasePosition = transform.localPosition;
    }
}
