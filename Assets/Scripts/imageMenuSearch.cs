using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI; 

public class ImageMenuSearch : MonoBehaviour
{
    public TMP_Dropdown dropdown; // TextMeshPro Dropdown for search
    public GameObject content; // The Content object containing all items
    public ScrollRect scrollRect; // Reference to the ScrollRect component (for scrolling)

    private List<GameObject> allCanvases = new List<GameObject>(); // List to hold all original canvas items (parents)
    private float originalContentHeight; // Store the original height of the content area

    void Start()
    {
        // Store all child canvas items of content in the list
        foreach (Transform child in content.transform)
        {
            allCanvases.Add(child.gameObject);
        }

        // Store the original content height
        originalContentHeight = content.GetComponent<RectTransform>().sizeDelta.y;

        // Add listener to the dropdown
        dropdown.onValueChanged.AddListener(FilterItemsByDropdown);
    }

    // Method to filter items based on the dropdown selection
    void FilterItemsByDropdown(int selectedIndex)
    {
        // Get the selected option's text
        string query = dropdown.options[selectedIndex].text.ToLower();

        // Check if the selected option is "All" (assuming "All" is the first option)
        if (query == "all")
        {
            // Show all canvases and reset scroll view size to the original height
            foreach (GameObject canvas in allCanvases)
            {
                canvas.SetActive(true);
            }
            ResetScrollViewSize();
            ResetScrollPosition(); // Scroll to top
            return;
        }

        // Filter each canvas based on the dropdown selection
        foreach (GameObject canvas in allCanvases)
        {
            // Check if any child or parent of the canvas matches the query
            bool isMatch = SearchInCanvasAndChildren(canvas.transform, query);
            canvas.SetActive(isMatch);
        }

        // Adjust scroll view size based on visible items
        AdjustScrollViewSize(query);
        ResetScrollPosition(); // Scroll to top
    }

    // Recursive function to search in parent and all children for a match
    bool SearchInCanvasAndChildren(Transform parent, string query)
    {
        // Check if the parent name contains the query
        if (parent.name.ToLower().Contains(query))
        {
            return true;
        }

        // Recursively check all children
        foreach (Transform child in parent)
        {
            if (SearchInCanvasAndChildren(child, query))
            {
                return true;
            }
        }
        return false;
    }

    // Resets the scroll view size to the original content height
    void ResetScrollViewSize()
    {
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, originalContentHeight);
    }

    // Adjusts the scroll view size based on visible items
    void AdjustScrollViewSize(string query)
    {
        RectTransform contentRect = content.GetComponent<RectTransform>();

        // Customize the size for specific queries
        if (query == "level 2")
        {
            contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, 2180);
        }
        else if (query == "level 4")
        {
            contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, 1380);
        }
        else
        {
            // Default adjustment (e.g., for other queries)
            contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, 1080);
        }
    }



    // Reset scroll position to the top
    void ResetScrollPosition()
    {
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1; // 1 is the top of the scroll view
        }
    }
}
