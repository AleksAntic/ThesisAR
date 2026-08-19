using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Provides elegant, standardized hover feedback transitions for UI buttons.
/// Swaps between a dark charcoal normal state and a slightly lighter warm grey hover state.
/// </summary>
public class UIHoverFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image targetImage;
    [SerializeField] private Color normalColor = new Color(0.114f, 0.094f, 0.094f, 0.71f); // #1D1818
    [SerializeField] private Color hoverColor = new Color(0.235f, 0.208f, 0.208f, 0.90f);  // #3C3535
    [SerializeField] private float transitionSpeed = 10f;

    private Color currentColor;
    private bool isHovered = false;

    void Awake()
    {
        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }

        if (targetImage != null)
        {
            currentColor = targetImage.color;
            normalColor = currentColor; // Preserve the original color style if already configured
            // Calculate a matching hover color that is slightly brighter/more opaque
            hoverColor = new Color(
                Mathf.Min(normalColor.r + 0.12f, 1f),
                Mathf.Min(normalColor.g + 0.12f, 1f),
                Mathf.Min(normalColor.b + 0.12f, 1f),
                Mathf.Min(normalColor.a + 0.19f, 1f)
            );
        }
    }

    void OnEnable()
    {
        if (targetImage != null)
        {
            targetImage.color = normalColor;
            currentColor = normalColor;
        }
        isHovered = false;
    }

    void Update()
    {
        if (targetImage == null) return;

        Color targetColor = isHovered ? hoverColor : normalColor;
        currentColor = Color.Lerp(currentColor, targetColor, Time.deltaTime * transitionSpeed);
        if (targetImage.color != currentColor)
        {
            targetImage.color = currentColor;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
    }
}
