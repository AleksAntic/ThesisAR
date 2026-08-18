using UnityEngine;
using System.Collections;
using TMPro;

/// <summary>
/// Manages world-space TextMeshPro visibility dynamically based on camera proximity.
/// Incorporates structural CPU throttling to bypass LookAt calculations when objects are culled.
/// </summary>
public class DistanceCullingBillboard : MonoBehaviour
{
    [Header("🎯 Target Observer")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private TextMeshPro textMeshPro;

    [Header("📊 Distance Threshold Metrics")]
    [SerializeField] private float hideDistance = 50f;
    [SerializeField] private float reduceDetailDistance = 20f;

    [Header("🎨 Font Size LOD Scales")]
    [SerializeField] private float nearFontSize = 4f;
    [SerializeField] private float midFontSize = 2f;

    private bool originalEnabled;
    private float originalFontSize;
    private bool isSystemFilterHidden = false;
    private Renderer parentRenderer;
    private bool isWithinCullingRange = true;
    private Coroutine cullingLoopHandle;

    void Awake()
    {
        if (textMeshPro == null) textMeshPro = GetComponent<TextMeshPro>();

        if (textMeshPro != null)
        {
            originalEnabled = textMeshPro.enabled;
            originalFontSize = textMeshPro.fontSize;
        }

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        parentRenderer = GetComponentInParent<Renderer>();
    }

    void OnEnable()
    {
        if (cullingLoopHandle != null) StopCoroutine(cullingLoopHandle);
        cullingLoopHandle = StartCoroutine(CullingLoop());
    }

    void OnDisable()
    {
        if (cullingLoopHandle != null)
        {
            StopCoroutine(cullingLoopHandle);
            cullingLoopHandle = null;
        }
    }

    void LateUpdate()
    {
        // ⚡ CRITICAL CPU OPTIMIZATION: Completely bypass mathematical LookAt transformations 
        // if the text component is disabled, hidden by distance LOD, or masked by search filters.
        if (textMeshPro == null || !textMeshPro.enabled || !isWithinCullingRange || isSystemFilterHidden) return;
        if (cameraTransform == null) return;

        // Execute precise billboard alignment matching camera perspective vectors smoothly
        transform.LookAt(transform.position + cameraTransform.rotation * Vector3.forward, cameraTransform.rotation * Vector3.up);
    }

    /// <summary>
    /// Throttled background loop evaluating metric distances 5 times per second instead of every frame.
    /// </summary>
    private IEnumerator CullingLoop()
    {
        var wait = new WaitForSeconds(0.2f);

        while (true)
        {
            if (cameraTransform == null || textMeshPro == null)
            {
                yield return wait;
                continue;
            }

            // Verify if the parent anchor has been explicitly turned off by search criteria queries
            if (parentRenderer != null && !parentRenderer.enabled)
            {
                isSystemFilterHidden = true;
                if (textMeshPro.enabled) textMeshPro.enabled = false;
                yield return wait;
                continue;
            }

            isSystemFilterHidden = false;
            float distance = Vector3.Distance(transform.position, cameraTransform.position);

            if (distance > hideDistance)
            {
                isWithinCullingRange = false;
                if (textMeshPro.enabled) textMeshPro.enabled = false;
            }
            else
            {
                isWithinCullingRange = true;
                if (!textMeshPro.enabled) textMeshPro.enabled = true;

                // Adjust text scale Level of Detail based on physical proximity steps
                if (distance > reduceDetailDistance)
                    textMeshPro.fontSize = midFontSize;
                else
                    textMeshPro.fontSize = nearFontSize;
            }

            yield return wait;
        }
    }

    /// <summary>
    /// Returns the components back to their default initialization properties.
    /// </summary>
    public void ResetState()
    {
        if (textMeshPro == null) return;

        textMeshPro.enabled = originalEnabled;
        textMeshPro.fontSize = originalFontSize;
        isSystemFilterHidden = false;
        isWithinCullingRange = true;
    }
}