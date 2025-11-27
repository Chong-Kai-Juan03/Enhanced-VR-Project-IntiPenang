using UnityEngine;
using UnityEngine.UI;

public class HorizontalDropdownScroller : MonoBehaviour
{
    [SerializeField] private ScrollRect scrollRect; // The ScrollRect component
    [SerializeField] private Button leftButton, rightButton; // Navigation buttons
    [SerializeField] private int dropdownCount = 5; // Total number of dropdowns
    private int currentPairIndex = 0; // Keeps track of which pair is currently active
    private float stepSize; // Size of the step to move ScrollRect

    void Start()
    {
        // Calculate the step size (1f divided by number of steps)
        stepSize = 1f / (dropdownCount - 2);

        UpdateScrollPosition();
        UpdateButtonStates();

        leftButton.onClick.AddListener(ScrollLeft);
        rightButton.onClick.AddListener(ScrollRight);
    }

    void ScrollLeft()
    {
        if (currentPairIndex > 0)
        {
            currentPairIndex--;
            UpdateScrollPosition();
            UpdateButtonStates();
        }
    }

    void ScrollRight()
    {
        if (currentPairIndex < dropdownCount - 2)
        {
            currentPairIndex++;
            UpdateScrollPosition();
            UpdateButtonStates();
        }
    }

    void UpdateScrollPosition()
    {
        // Adjust the horizontalNormalizedPosition of the ScrollRect
        scrollRect.horizontalNormalizedPosition = currentPairIndex * stepSize;
    }

    void UpdateButtonStates()
    {
        leftButton.interactable = currentPairIndex > 0;
        rightButton.interactable = currentPairIndex < dropdownCount - 2;
    }
}