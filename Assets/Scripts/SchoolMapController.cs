using UnityEngine;
using UnityEngine.UI;

public class SchoolMapController : MonoBehaviour
{
    [Header("Tabs")]
    public Button[] tabButtons;        // Assign your tab buttons here
    public GameObject[] levelMaps;     // Assign each map panel here (same order as tabs)

    [Header("Close Button")]
    public Button closeButton;

    private int currentActiveIndex = 0;  // Track which map is active

    private void Awake()
    {
        // Hook up tab button clicks
        for (int i = 0; i < tabButtons.Length; i++)
        {
            int index = i; // capture loop variable
            tabButtons[i].onClick.AddListener(() => ShowMap(index));
        }

        // Hook up close button
        if (closeButton != null)
            closeButton.onClick.AddListener(CloseSchoolLayout);
    }

    private void Start()
    {
        // Default: show first map
        if (levelMaps != null && levelMaps.Length > 0)
        {
            ShowMap(0);
        }
    }

    public void ShowMap(int index)
    {
        if (levelMaps == null || levelMaps.Length == 0) return;

        // Hide all maps
        for (int i = 0; i < levelMaps.Length; i++)
        {
            if (levelMaps[i] != null)
                levelMaps[i].SetActive(i == index);
        }

        currentActiveIndex = index;
    }

    public void CloseSchoolLayout()
    {
        // Example: just hide the whole controller
        gameObject.SetActive(false);
    }
}
