using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Intercetta il rendering della Map_Camera a runtime per spegnere la luce direzionale
/// e forzare un'illuminazione ambientale piatta, accendendo i colori senza creare riflessi.
/// </summary>
public class MapCameraLightingController : MonoBehaviour
{
    [Header("🎥 Camera Links")]
    [SerializeField] private Camera mapCamera;

    [Header("💡 Scene Light Links")]
    [SerializeField] private Light arDirectionalLight;

    [Header("🎛️ Brightness Settings")]
    [Tooltip("Luminosità della mappa. 1 = Bianco pieno, 0.5 = Più cupo. Regolalo se la mappa è troppo illuminata.")]
    [SerializeField] private float mapBrightness = 1.0f;

    private bool originalLightState;
    private Color originalAmbientColor;
    private AmbientMode originalAmbientMode;

    void OnEnable()
    {
        if (mapCamera == null) mapCamera = GetComponent<Camera>();

        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
    }

    // Eseguito un istante PRIMA che la Map_Camera renderizzi la mappa 2D
    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera == mapCamera)
        {
            // 1. Spegne la luce direzionale per eliminare la patina lucida
            if (arDirectionalLight != null)
            {
                originalLightState = arDirectionalLight.enabled;
                arDirectionalLight.enabled = false;
            }

            // 2. Salva l'ambiente originale e ne forza uno piatto e luminoso per accendere i colori
            originalAmbientMode = RenderSettings.ambientMode;
            originalAmbientColor = RenderSettings.ambientLight;

            RenderSettings.ambientMode = AmbientMode.Flat;
            // Crea un grigio chiaro/bianco in base al valore di mapBrightness per illuminare i materiali
            RenderSettings.ambientLight = new Color(mapBrightness, mapBrightness, mapBrightness, 1f);
        }
    }

    // Eseguito un istante DOPO che la Map_Camera ha finito il rendering
    private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera == mapCamera)
        {
            // 1. Ripristina la luce direzionale per la telecamera AR principale
            if (arDirectionalLight != null)
            {
                arDirectionalLight.enabled = originalLightState;
            }

            // 2. Ripristina l'illuminazione ambientale originale del mondo di gioco
            RenderSettings.ambientMode = originalAmbientMode;
            RenderSettings.ambientLight = originalAmbientColor;
        }
    }
}