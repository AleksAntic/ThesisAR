using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Handles the UI interactions, input fields, and search categorizations for the Database Search Panel.
/// </summary>
public class SearchPanelController : PanelController
{
    [SerializeField] private TMP_InputField searchInputField;
    [SerializeField] private TMP_Dropdown searchCategoryDropdown;
    [SerializeField] private TMP_Dropdown searchSymbolsDropdown;
    [SerializeField] private Button clearAllFiltersButton;
    [SerializeField] private TextMeshProUGUI searchCounterText;

    private UIManager uiManager;

    protected override void Awake()
    {
        base.Awake();
        uiManager = GetComponentInParent<UIManager>() ?? FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);

        if (searchInputField == null)
        {
            searchInputField = transform.Find("Search_Input_Field")?.GetComponent<TMP_InputField>()
                               ?? transform.Find("SearchInputField")?.GetComponent<TMP_InputField>();
        }
    }

    void Start()
    {
        if (searchInputField != null)
        {
            searchInputField.onValueChanged.AddListener(OnSearchInputValueChanged);
        }
    }

    private void OnDestroy()
    {
        if (searchInputField != null)
        {
            searchInputField.onValueChanged.RemoveListener(OnSearchInputValueChanged);
        }
    }

    private void OnSearchInputValueChanged(string value)
    {
        if (uiManager != null)
        {
            uiManager.ExecuteDynamicFacetedSearch();
        }
    }
}
