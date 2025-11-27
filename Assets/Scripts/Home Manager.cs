using System.Collections;
using UnityEngine;
using TMPro;

public class HomeManager : MonoBehaviour
{
    // Reference to all your canvases
    public GameObject homepageCanvas;   // Reference to your homepage canvas
    public GameObject bootstrapCanvas;  // Reference to your Bootstrap canvas (InitializeScene)
    public GameObject[] otherCanvases;  // Array of other canvases (0 = Image Menu / Thumbnails)


    [Header("Bootstrap UI Elements")]
    public TextMeshProUGUI loadingText;
    public bool autoHideAfterLoad = true;  // Auto switch to homepage after finished

    private int _currentLoaded = 0;
    private int _totalToLoad = 79;
    private bool _finished = false;

    void Start()
    {
        // Show bootstrap at start, hide homepage until loading is complete
        if (bootstrapCanvas != null) bootstrapCanvas.SetActive(true);
        if (homepageCanvas != null) homepageCanvas.SetActive(false);

        // Keep thumbnails or others disabled except background ones if needed
        for (int i = 0; i < otherCanvases.Length; i++)
        {
            otherCanvases[i].SetActive(false);
        }

        // Initialize text
        if (loadingText != null)
            loadingText.text = "Loading Virtual Tour... [0/79]";
    }

    /// <summary>
    /// Called by FirebaseBridge after each mapping is applied.
    /// </summary>
    public void UpdateLoadingProgress(int current, int total)
    {
        _currentLoaded = current;
        _totalToLoad = total;

        if (loadingText != null)
        {
            loadingText.text = $"Loading Virtual Tour... [{current}/{total}]";
            Debug.Log($"[HomeManager] Loading Virtual Tour... [{current}/{total}]");
        }
        else
        {
            Debug.LogWarning("[HomeManager] loadingText reference is null!");
        }

        // When finished loading all scenes
        if (autoHideAfterLoad && !_finished && current >= total)
        {
            _finished = true;
            StartCoroutine(ShowHomepageAfterDelay());
        }
    }


    private IEnumerator ShowHomepageAfterDelay()
    {
        yield return new WaitForSeconds(0.3f); // short delay for smoother transition

        if (bootstrapCanvas != null) bootstrapCanvas.SetActive(false);
        if (homepageCanvas != null) homepageCanvas.SetActive(true);

        Debug.Log("[HomeManager] All scenes loaded, showing homepage.");
    }


    // Function to open a specific canvas and close the homepage
    public void OpenCanvas(int canvasIndex)
    {
        if (homepageCanvas != null) homepageCanvas.SetActive(false);

        // Bootstrap should not re-appear when switching canvases
        if (canvasIndex >= 0 && canvasIndex < otherCanvases.Length && otherCanvases[canvasIndex] != null)
        {
            otherCanvases[canvasIndex].SetActive(true);
        }
        else
        {
            Debug.LogError("[HomeManager] Invalid canvas index provided.");
        }
    }

    // Optional function to return to the homepage canvas
    public void ReturnToHomePage()
    {
        foreach (GameObject canvas in otherCanvases)
        {
            if (canvas != null) canvas.SetActive(false);
        }

        if (homepageCanvas != null) homepageCanvas.SetActive(true);
    }

    // Function to quit the application
    public void QuitApplication()
    {
        Debug.Log("Quit button clicked!");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
