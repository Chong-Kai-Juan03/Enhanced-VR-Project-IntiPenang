using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class HoverController : MonoBehaviour
{
    [Header("Hover UI Elements")]
    public GameObject HoverCaption; // Hover text container (usually TMP)
    public TextMeshProUGUI hoverText;  // The text to show

    private Vector3 originalSize; // Original scale
    private GameObject currentHoveredUIElement; // Track hovered element

    [Header("Raycasting References")]
    public EventSystem eventSystem;
    public List<GraphicRaycaster> graphicRaycasters = new List<GraphicRaycaster>();

    void Start()
    {
        // Ensure the Event System is assigned
        if (eventSystem == null)
        {
            eventSystem = FindObjectOfType<EventSystem>();
        }

        // Find all GraphicRaycasters in child canvases if not assigned in Inspector
        if (graphicRaycasters.Count == 0)
        {
            graphicRaycasters.AddRange(GetComponentsInChildren<GraphicRaycaster>());
        }
    }

    public bool IsPointerOverUIElement(out GameObject hoveredUIElement)
    {
        PointerEventData pointerData = new PointerEventData(eventSystem)
        {
            position = Input.mousePosition
        };

        foreach (GraphicRaycaster raycaster in graphicRaycasters)
        {
            List<RaycastResult> results = new List<RaycastResult>();
            raycaster.Raycast(pointerData, results);

            if (results.Count > 0)
            {
                hoveredUIElement = results[0].gameObject;
                return true;
            }
        }

        hoveredUIElement = null;
        return false;
    }

    // --- Hover Text ---
    public void DisplayHoverText(string objectName, bool isUI = false)
    {
        HoverCaption.SetActive(true);
        if (hoverText != null)
        {
            hoverText.text = "Go to " + objectName;
        }
    }

    // Clear hover text
    public void ClearHoverText()
    {
        HoverCaption.SetActive(false);
        if (hoverText != null)
        {
            hoverText.text = "";
        }
    }

    // Adjust the size of the UI image when hovering
    public void AdjustImageSize(GameObject uiElement, bool increaseSize)
    {
        RectTransform rectTransform = uiElement.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            if (increaseSize)
            {
                // Store the original size if it's the first time hovering
                if (originalSize == Vector3.zero)
                {
                    originalSize = rectTransform.localScale;
                }

                // Increase the size of the image
                rectTransform.localScale = originalSize * 1.2f;  // Adjust this factor for size
            }
            else
            {
                // Reset to the original size
                rectTransform.localScale = originalSize;
            }
        }
    }

    // Reset the size of the UI image when no longer hovering
    public void ResetImageSize(GameObject uiElement)
    {
        RectTransform rectTransform = uiElement.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.localScale = originalSize;
        }
    }

    // Apply hover design (e.g., shadows, color changes, etc.)
    public void ApplyHoverDesign(GameObject uiElement)
    {
        Image image = uiElement.GetComponent<Image>();
        if (image != null)
        {
            // Example: Change color and add a shadow effect when hovering 

            // Add shadow component if not already added
            Shadow shadow = uiElement.GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = uiElement.AddComponent<Shadow>();
                shadow.effectColor = Color.black;
                shadow.effectDistance = new Vector2(5, -5);
            }
        }
    }

    // Reset hover design (restore original color and remove shadow)
    public void ResetHoverDesign(GameObject uiElement)
    {
        Image image = uiElement.GetComponent<Image>();
        if (image != null)
        {
            // Reset color to white instantly
            image.color = Color.white;

            // Remove shadow component if it exists
            Shadow shadow = uiElement.GetComponent<Shadow>();
            if (shadow != null)
            {
                Destroy(shadow);
            }
        }
    }
}
