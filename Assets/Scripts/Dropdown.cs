using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DropdownMenu : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown dropDownMenu; // Reference to the Dropdown UI
    [SerializeField] private GameObject[] objSites; // Array of GameObjects for the different sites 

    public int levelNumber; // Manually set the level number (e.g., 2, 3, 4, 5, or 6)

    // Start is called before the first frame update
    void Start()
    {
        // Add listener to the dropdown menu to call HandleInputData when dropdown value changes
        dropDownMenu.onValueChanged.AddListener(delegate { HandleInputData(dropDownMenu.value); });
    }

    // Method to handle dropdown option changes
    void HandleInputData(int val)
    {
        if (val == 0)
        {
            Debug.Log("Placeholder selected. No site will be loaded.");
            return;
        }

        int siteIndex = val - 1;

        // Handle dropdown options based on the level number
        switch (levelNumber)
        {
            case 2:
                LoadSite(siteIndex); // Level 2 starts from index 0
                break;
            case 3:
                LoadSite(siteIndex + 42); // Level 3 starts from index 42
                break;
            case 4:
                LoadSite(siteIndex + 54); // Level 4 starts from index 54
                break;
            case 5:
                LoadSite(siteIndex + 74); // Level 5 starts from index 74
                break;
            case 6:
                LoadSite(siteIndex + 32); // Level 6 starts from index 32
                break;
            default:
                Debug.LogWarning("Invalid level number.");
                break;
        }
    }

    // Method to load a specific site
    public void LoadSite(int siteNumber)
    {
        // Hide all sites first
        for (int i = 0; i < objSites.Length; i++)
        {
            objSites[i].SetActive(false);
        }

        // Show the selected site
        if (siteNumber < objSites.Length)
        {
            objSites[siteNumber].SetActive(true);
        }

        // Reset dropdown menu to placeholder after selection
        dropDownMenu.value = 0;
    }
}
