using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using System.Linq;
using System.Collections.Generic;

public class TourManager : MonoBehaviour
{
    public GameObject[] objSites; // Array of site objects to display
    public GameObject canvasMainMenu; // Reference to the main menu canvas
    public GameObject topBar; // Reference to the top bar UI element
    public GameObject dropDownMenu; // Reference to the dropdown menu
    public GameObject sidebar; // Reference to the dropdown menu
    public GameObject bottombar; // Reference to the dropdown menu
    public GameObject mapPanel; // Reference to the dropdown menu
    public GameObject FirebaseBridge; // Reference to the FirebaseBridge
    public GameObject USAINSObject;

    public bool isCameraMove = true; // Flag to control camera movement

    private GameObject lastHoveredObject = null;
    private GameObject currentHoveredUIElement = null;
    private Camera mainCamera;  // Main Camera reference
    private GameObject lastActiveSiteBeforeGuide;

    public float zoomDuration = 0.2f;  // Duration for zoom effect
    public float zoomFOV = 3f;  // Target FOV during zoom
    private float originalFOV;  // Store original FOV


    // Reference to the HoverController script
    public HoverController hoverController;


    [Header("Auto Tour Prompt Tags")]
    public GameObject promptStartTag; // The "Move mouse to stop" tag
    public GameObject promptStopTag;  // The "STOPPED" tag
    public TextMeshProUGUI timerText;  // The 00:00 timer text

    // Variable handle the auto tour
    public GameObject autoTourPanel;
    private Coroutine autoTourCoroutine;
    private bool isAutoTourRunning = false;
    private Vector3 lastCameraRotation;
    public float siteStayDuration = 15f; // seconds per site
    public Sidebar sidebarController; // Reference to Sidebar script
    private HideAllHotspot hotspotHider;

    private List<int> topVisitedIndexes = new List<int>();

    [System.Serializable]
    public class SceneVisitEntry
    {
        public int index;
        public string title;
        public int views;
    }

    void Start()
    {
        Debug.Log("TourManager started.");
        FirebaseBridge.SetActive(true);

        // Make sure NO pano is active at boot, so visits won’t fire early.
        if (objSites != null)
            foreach (var s in objSites) if (s) s.SetActive(false);

        // Ensure the top bar is hidden at the start
        topBar.SetActive(false);

        mainCamera = Camera.main;
        if (mainCamera != null)
        {
            originalFOV = mainCamera.fieldOfView;
        }

        // Find HoverController component
        if (hoverController == null)
        {
            hoverController = FindObjectOfType<HoverController>();
        }
        RequestTopScenesFromFirebase();


    }

    void Update()
    {
        // Check if pointer is over any UI element (including dropdown)
        if (IsPointerOverUIElement())
        {
            // If hovering over dropdown, disable camera movement
            if (EventSystem.current.currentSelectedGameObject == dropDownMenu)
            {
                Debug.Log("Pointer is over dropdown menu, disabling camera movement.");
                isCameraMove = false;
            }
            else
            {
                Debug.Log("Pointer is over other UI elements, camera movement disabled.");
                isCameraMove = false;
            }
        }
        else
        {
            // If not hovering over UI, enable camera movement
            isCameraMove = true;
        }

        // Handle non-UI element raycasts (like 3D objects)
        RaycastHit hit;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out hit, 100.0f))
        {
            GameObject currentHoveredObject = hit.transform.gameObject;

            if (Input.GetMouseButtonDown(0))
            {
                HandleUIClick(currentHoveredObject);
            }

            if (currentHoveredObject.CompareTag("NextSite"))
            {
                hoverController.DisplayHoverText(currentHoveredObject.name);
                HighlightObject(currentHoveredObject);

                // Reset previously hovered object if it exists and is different
                if (lastHoveredObject != null && lastHoveredObject != currentHoveredObject)
                {
                    ResetObject(lastHoveredObject);
                }

                lastHoveredObject = currentHoveredObject;
            }
        }
        else
        {
            // If nothing is hovered, reset the last hovered 3D object and UI element
            if (lastHoveredObject != null)
            {
                ResetObject(lastHoveredObject);
                lastHoveredObject = null;
                hoverController.ClearHoverText();
            }
        }

        // Handle hover over UI elements and adjust image size accordingly
        if (hoverController.IsPointerOverUIElement(out GameObject uiElement))
        {
            // Handle hover over UI element with "NextSite" tag
            if (uiElement.CompareTag("NextSite"))
            {
                hoverController.AdjustImageSize(uiElement, true);  // Make image bigger when hovering
                hoverController.ApplyHoverDesign(uiElement);  // Apply hover design

                // Reset the previous UI element if it's different from the current one
                if (currentHoveredUIElement != null && currentHoveredUIElement != uiElement)
                {
                    hoverController.ResetImageSize(currentHoveredUIElement);
                    hoverController.ResetHoverDesign(currentHoveredUIElement);
                }

                // Track the current hovered UI element
                currentHoveredUIElement = uiElement;
            }
        }
        else
        {
            // If no UI element is hovered, reset the last hovered UI element
            if (currentHoveredUIElement != null)
            {
                hoverController.ResetImageSize(currentHoveredUIElement);
                hoverController.ResetHoverDesign(currentHoveredUIElement);
                currentHoveredUIElement = null;
            }
        }
    }

    // Check if the pointer is over any UI element
    bool IsPointerOverUIElement()
    {
        return EventSystem.current.IsPointerOverGameObject();
    }

    IEnumerator ZoomAndLoadSite(int siteNumber)
    {
        // Animate the camera zoom to the target FOV
        float elapsedTime = 0f;
        while (elapsedTime < zoomDuration)
        {
            mainCamera.fieldOfView = Mathf.Lerp(originalFOV, zoomFOV, elapsedTime / zoomDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Ensure FOV is exactly the zoomFOV after the loop
        mainCamera.fieldOfView = zoomFOV;

        // After zoom, load the new site
        LoadSite(siteNumber);

        // Reset camera FOV to the original
        elapsedTime = 0f;
        while (elapsedTime < zoomDuration)
        {
            mainCamera.fieldOfView = Mathf.Lerp(zoomFOV, originalFOV, elapsedTime / zoomDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Ensure FOV is exactly the original FOV after resetting
        mainCamera.fieldOfView = originalFOV;
    }


    // Handle UI element click
    void HandleUIClick(GameObject uiElement)
    {
        Debug.Log("Clicked on UI element: " + uiElement.name);

        // Example: GetSiteToload from a script on the UI element
        NewSites newSiteScript = uiElement.GetComponent<NewSites>();
        if (newSiteScript != null)
        {
            int siteToLoad = newSiteScript.GetSiteToload();
            StartCoroutine(ZoomAndLoadSite(siteToLoad));
        }
    }

    // Highlight a 3D object
    void HighlightObject(GameObject obj)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = UnityEngine.Color.gray;  // Change to a highlight color
        }
    }

    // Reset a 3D object
    void ResetObject(GameObject obj)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = UnityEngine.Color.white;  // Reset to the original color
        }
    }

    public void LoadSite(int siteNumber)
    {
        // Hide all sites
        for (int i = 0; i < objSites.Length; i++)
        {
            objSites[i].SetActive(false);
        }
        // Show the selected site
        objSites[siteNumber].SetActive(true);

        mapPanel.SetActive(false);

        // Save the last active site
        lastActiveSiteBeforeGuide = objSites[siteNumber];

        sidebar.SetActive(true);
        topBar.SetActive(true);
        //dropDownMenu.SetActive(true);
        //bottombar.SetActive(true);

        // Show the selected site   
        var siteRoot = objSites[siteNumber];
        siteRoot.SetActive(true);

        // Hide the main menu
        canvasMainMenu.SetActive(false);

        // Enable camera movement
        isCameraMove = true;
        GetComponent<CameraController>().ResetCamera();
    }


    public void OpenIntiWebsite()
    {
        string url = "https://newinti.edu.my/";
        Application.OpenURL(url);
    }

    public void OpenMapPanel()
    {
        if (USAINSObject != null)
        {
            mapPanel.SetActive(true);
            isCameraMove = false; // disable camera movement
            dropDownMenu.SetActive(true);
            sidebar.SetActive(true); // Reference to the dropdown menu
            bottombar.SetActive(false);
            topBar.SetActive(true);

            // Hide all sites
            for (int i = 0; i < objSites.Length; i++)
            {
                objSites[i].SetActive(false);
            }

        }
    }

    public void CloseMapPanel()
    {
        if (USAINSObject != null)
        {
            mapPanel.SetActive(false);
            isCameraMove = true; // disable camera movement
            dropDownMenu.SetActive(true);
            sidebar.SetActive(true); // Reference to the dropdown menu
            bottombar.SetActive(true);
            topBar.SetActive(true);

            // Re-enable the last active site
            if (lastActiveSiteBeforeGuide != null)
            {
                lastActiveSiteBeforeGuide.SetActive(true);
            }
        }
    }



    public void OpenUSAINSGUIDE()
    {
        if (USAINSObject != null)
        {
            USAINSObject.SetActive(true);
            isCameraMove = false; // disable camera movement
            dropDownMenu.SetActive(true);
            sidebar.SetActive(true); // Reference to the dropdown menu
            bottombar.SetActive(false);
            topBar.SetActive(true);

            // Hide all sites
            for (int i = 0; i < objSites.Length; i++)
            {
                objSites[i].SetActive(false);
            }

        }
    }

    public void CloseUSAINSGUIDE()
    {
        if (USAINSObject != null)
        {
            USAINSObject.SetActive(false);
            isCameraMove = true; // disable camera movement
            dropDownMenu.SetActive(true);
            sidebar.SetActive(true); // Reference to the dropdown menu
            bottombar.SetActive(true);
            topBar.SetActive(true);

            // Re-enable the last active site
            if (lastActiveSiteBeforeGuide != null)
            {
                lastActiveSiteBeforeGuide.SetActive(true);
            }
        }
    }

    public void OpenAutoTourPanel()
    {
        autoTourPanel.SetActive(true);
        isCameraMove = false; // disable camera movement
        dropDownMenu.SetActive(true);
        sidebar.SetActive(true); // Reference to the dropdown menu
        bottombar.SetActive(false);
        topBar.SetActive(true);

        // Hide all sites
        for (int i = 0; i < objSites.Length; i++)
        {
            objSites[i].SetActive(false);
        }
    }

    public void CloseAutoTourPanel()
    {
        autoTourPanel.SetActive(false);
        isCameraMove = true; // disable camera movement
        dropDownMenu.SetActive(true);
        sidebar.SetActive(true); // Reference to the dropdown menu
        bottombar.SetActive(true);
        topBar.SetActive(true);

        // Re-enable the last active site
        if (lastActiveSiteBeforeGuide != null)
        {
            lastActiveSiteBeforeGuide.SetActive(true);
        }
    }


    // =====================================================================
    //  Normal & Top Scenes Auto Tour (Unified Entry Point)
    // =====================================================================
    public void StartAutoTour(bool isTopScenes = false)
    {
        Debug.Log($"[AutoTour] ▶ Starting Auto Tour (TopScenes={isTopScenes})");

        // --- Initialize hotspot hider ---
        if (hotspotHider == null)
            hotspotHider = FindObjectOfType<HideAllHotspot>();
        if (hotspotHider != null)
            hotspotHider.HideHotspots();

        // Ensure camera exists
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("[AutoTour] No Main Camera found!");
                return;
            }
        }

        // Ensure camera is active
        if (!mainCamera.gameObject.activeInHierarchy)
        {
            Debug.Log("[AutoTour] Activating Main Camera...");
            mainCamera.gameObject.SetActive(true);
        }

        // Collect scene indexes
        List<int> indexesToTour = new List<int>();

        if (isTopScenes && topVisitedIndexes != null && topVisitedIndexes.Count > 0)
        {
            indexesToTour = topVisitedIndexes;
            Debug.Log($"[AutoTour] Using Top Scenes list ({indexesToTour.Count} scenes)");
        }
        else
        {
            if (objSites != null && objSites.Length > 0)
            {
                for (int i = 0; i < objSites.Length; i++)
                    indexesToTour.Add(i);

                Debug.Log($"[AutoTour] No top scenes found — using all {objSites.Length} scenes as fallback.");
            }
            else
            {
                Debug.LogError("[AutoTour] objSites not assigned!");
                return;
            }
        }

        // Stop any running tour first
        if (autoTourCoroutine != null)
            StopCoroutine(autoTourCoroutine);

        // Start coroutine
        autoTourCoroutine = StartCoroutine(AutoTourRoutine(indexesToTour));
    }

    // =====================================================================
    //  Shared Auto Tour Routine (for both Normal & TopScenes)
    // =====================================================================
    private IEnumerator AutoTourRoutine(List<int> indexes)
    {
        Debug.Log("[AutoTour] Started.");
        isAutoTourRunning = true;

        // Hide UI for immersive view
        if (autoTourPanel) autoTourPanel.SetActive(false);
        if (sidebar) sidebar.SetActive(false);
        if (topBar) topBar.SetActive(false);
        if (dropDownMenu) dropDownMenu.SetActive(false);
        if (bottombar) bottombar.SetActive(false);

        // Enable camera auto-rotation when tour starts
        if (sidebarController != null)
            sidebarController.EnableAutoRotate();

        // Show "start tour" tag (with timer) and reset values
        if (promptStartTag != null) promptStartTag.SetActive(true);
        if (promptStopTag != null) promptStopTag.SetActive(false);
        if (timerText != null) timerText.text = "00:00";

        // Track camera rotation baseline
        if (mainCamera != null)
            lastCameraRotation = mainCamera.transform.eulerAngles;

        // Track total elapsed time
        float totalElapsed = 0f;

        Debug.Log($"[AutoTour] Visiting {indexes.Count} scenes: [{string.Join(",", indexes)}]");

        // Loop through selected scenes (top scenes or all)
        for (int k = 0; k < indexes.Count; k++)
        {
            int i = indexes[k];
            if (i < 0 || i >= objSites.Length)
            {
                Debug.LogWarning($"[AutoTour] Skipping invalid index {i}");
                continue;
            }

            if (!isAutoTourRunning)
                yield break;

            Debug.Log($"[AutoTour] Visiting site {i + 1}/{objSites.Length}: {objSites[i].name}");
            LoadSite(i);

            float elapsed = 0f;

            while (elapsed < siteStayDuration)
            {
                // Update overall timer every frame
                totalElapsed += Time.deltaTime;

                if (timerText != null)
                {
                    int minutes = Mathf.FloorToInt(totalElapsed / 60f);
                    int seconds = Mathf.FloorToInt(totalElapsed % 60f);
                    timerText.text = $"{minutes:00}:{seconds:00}";
                }

                // Detect manual user input (mouse drag or movement)
                if (Input.GetMouseButton(0) ||
                    Mathf.Abs(Input.GetAxis("Mouse X")) > 0.05f ||
                    Mathf.Abs(Input.GetAxis("Mouse Y")) > 0.05f)
                {
                    Debug.Log("[AutoTour] User input detected — stopping auto tour.");

                    // Disable auto-rotation immediately like your original version
                    if (sidebarController != null)
                        sidebarController.DisableAutoRotate();

                    StopAutoTour();
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Update camera rotation reference
            if (mainCamera != null)
                lastCameraRotation = mainCamera.transform.eulerAngles;
        }

        Debug.Log("[AutoTour] Completed all sites.");
        isAutoTourRunning = false;

        // Disable auto-rotation when tour finishes
        if (sidebarController != null)
            sidebarController.DisableAutoRotate();

        // Hide "start tour" tag and show "stopped" tag for 1 sec
        if (promptStartTag != null)
            promptStartTag.SetActive(false);

        if (promptStopTag != null)
        {
            promptStopTag.SetActive(true);
            yield return new WaitForSeconds(1f);
            promptStopTag.SetActive(false);
        }

        // Re-enable UI after full tour completion
        if (dropDownMenu) dropDownMenu.SetActive(true);
        if (bottombar) bottombar.SetActive(true);

        hotspotHider.ShowHotspots();

        // Return to main menu
        ReturnToMenu();
    }



    public void StopAutoTour()
    {
        if (autoTourCoroutine != null)
        {
            StopCoroutine(autoTourCoroutine);
            autoTourCoroutine = null;
        }

        isAutoTourRunning = false;
        Debug.Log("[AutoTour] Stopped manually or due to movement.");

        hotspotHider.ShowHotspots();

        // Re-enable UI elements
        dropDownMenu.SetActive(true);
        bottombar.SetActive(true);

        // Hide "start" tag
        if (promptStartTag != null)
            promptStartTag.SetActive(false);

        // Show "stopped" tag briefly
        if (promptStopTag != null)
            StartCoroutine(ShowStoppedPrompt());
    }

    private IEnumerator ShowStoppedPrompt()
    {
        promptStopTag.SetActive(true);
        yield return new WaitForSeconds(1f);
        promptStopTag.SetActive(false);
    }


    public void RequestTopScenesFromFirebase()
    {
    #if UNITY_WEBGL && !UNITY_EDITOR
        AnalyticsBridge.RequestTopVisited(gameObject.name, "OnTopScenesReceived");
    #else
        Debug.Log("[TourManager] Simulating top scenes (Editor mode)");
        // Editor test mock
        OnTopScenesReceived("[{\"index\":1,\"title\":\"Teletronic Lab\",\"views\":20},{\"index\":2,\"title\":\"IT Lab\",\"views\":15}]");
    #endif
    }

    public void OnTopScenesReceived(string json)
    {
        Debug.Log($"[TourManager] Received top visited scenes: {json}");
        var topList = JsonHelper.FromJson<SceneVisitEntry>(json);

        if (topList == null || topList.Length == 0)
        {
            Debug.LogWarning("[TourManager] No top scenes found.");
            topVisitedIndexes = new List<int>();
            return;
        }

        topVisitedIndexes = topList.Select(s => s.index).ToList();

        Debug.Log($"[TourManager] Top scenes ready: {string.Join(",", topVisitedIndexes)}");
    }

    public void OnClickAutoTourAll()
    {
        StartAutoTour(false);
    }

    public void OnClickAutoTourTopScenes()
    {
        StartAutoTour(true);
    }


    public void ReturnToMenu()
    {
        // Show the main menu
        canvasMainMenu.SetActive(true);
        // Hide all sites
        for (int i = 0; i < objSites.Length; i++)
        {
            objSites[i].SetActive(false);
        }
        // Hide the top bar
        topBar.SetActive(false);
        dropDownMenu.SetActive(false);
        sidebar.SetActive(false);
        bottombar.SetActive(false);
        // Disable camera movement
        isCameraMove = false;
        GetComponent<CameraController>().ResetCamera();
    }
}
