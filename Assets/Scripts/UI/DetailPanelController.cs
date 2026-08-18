using UnityEngine;
using TMPro;

/// <summary>
/// Handles the display, pagination, and translation of memorial stone details.
/// </summary>
public class DetailPanelController : PanelController
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI personsListText;

    private UIManager uiManager;

    protected override void Awake()
    {
        base.Awake();
        uiManager = GetComponentInParent<UIManager>();
    }
}
