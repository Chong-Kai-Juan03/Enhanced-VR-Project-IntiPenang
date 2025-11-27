using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;
using static UnityEngine.GraphicsBuffer;

// -----------------------------------------------------------------------------
// FirebaseBridge — Lookup by stable Id, freshness by S3Key
// -----------------------------------------------------------------------------
// • Each mapping row (Inspector):
//     - Target Object (optional)
//     - Material (drag & drop; cloned at runtime)
//     - Fallback Texture (optional)
//     - Data Id  (Firebase push key to WATCH)  <-- identity / lookup
//     - Material Key (last applied S3Key)      <-- freshness/version
// • JSON array from index.html matches SceneDataCore.SceneDto:
//     { Id, S3Key, ImageUrl, Building?, Level?, SceneTitle? }
// • Flow per mapping:
//     - Find DTO via Data Id == dto.Id
//     - If Material Key == dto.S3Key → SKIP download
//     - Else download dto.ImageUrl, process, set texture, then set Material Key = dto.S3Key
// • Texture cache: S3Key → Texture2D (reused across mappings)
// -----------------------------------------------------------------------------

[System.Serializable]
public class SceneMapping
{
    [Tooltip("Leave empty → apply as GLOBAL skybox.")]
    public GameObject targetObject;

    [Tooltip("Drag your Skybox/Panoramic (or pano-capable) Material here.")]
    public Material material; // Direct asset reference (cloned at runtime)

    [Tooltip("Optional default pano used if download fails or ImageUrl is empty.")]
    public Texture2D fallbackTexture;

    [Tooltip("Firebase push key to WATCH (stable Id from JSON).")]
    public string dataId;        // <-- identity

    [Tooltip("The S3Key currently applied to this material. If equals incoming S3Key, we skip downloading.")]
    public string materialKey;   // <-- freshness/version

    // Buttons For Image Menu and Bottom Bar
    [Header("UI Buttons")]

    [Tooltip("Preview image for Button 1 (usually the Image inside that button).")]
    public Image image_button1;

    [Tooltip("Preview image for Button 2 (usually the Image inside that button).")]
    public Image image_button2;

    [Tooltip("Fallback Sprite for both buttons if download fails or skipped.")]
    public Sprite fallbackSprite;

}

public class FirebaseBridge : MonoBehaviour
{
    [Header("Scene Mappings (Inspector)")]
    public List<SceneMapping> sceneMappings = new List<SceneMapping>();

    [Header("Behavior")]
    [Tooltip("Keep this object across scene loads and auto re-apply last global skybox name")]
    [SerializeField] private bool persistAcrossScenes = true;

    [Tooltip("Persist each mapping's last applied S3Key in PlayerPrefs so we can skip downloads next run too.")]
    [SerializeField] private bool persistMaterialKeys = true;

    [Header("Global Fallback")]
    [Tooltip("If a GLOBAL mapping has no material, use this as fallback skybox material")]
    [SerializeField] private Material fallbackSkybox;

    // ---------- Caches ----------
    // Id → DTO (uses SceneDto from SceneDataCore.cs)
    private readonly Dictionary<string, SceneDto> _dtoById =
        new Dictionary<string, SceneDto>(System.StringComparer.Ordinal);

    // Material asset → runtime clone
    private readonly Dictionary<Material, Material> _runtimeMatCache =
        new Dictionary<Material, Material>();

    // S3Key → processed Texture2D (reused across mappings)
    private readonly Dictionary<string, Texture2D> _texByS3Key =
        new Dictionary<string, Texture2D>();

    // Keep last payload for re-apply
    private SceneDto[] _lastDtos;

    // ---------- Persistence keys ----------
    private const string PP_LAST_SKYBOX_NAME = "FB_LAST_SKYBOX_NAME";
    private const string PP_MATKEY_PREFIX = "FB_MAPP_KEY_"; // + dataId

    // ---------- Texture pipeline knobs ----------
    private const int TARGET_MAX_SIZE = 8192;
    private const int TARGET_ANISO = 9;

    [SerializeField] private HomeManager homeManager;  // Drag correct one manually

    private static readonly HashSet<string> _visitedIds = new HashSet<string>();


    // ====================== Unity Lifecycle =======================
    private void Awake()
    {
        if (persistAcrossScenes)
        {
            var all = FindObjectsOfType<FirebaseBridge>();
            if (all.Length > 1)
            {
                Debug.Log("[FB] Another FirebaseBridge exists, destroying this duplicate.");
                Destroy(gameObject);
                return;
            }
            DontDestroyOnLoad(gameObject);
            Debug.Log("[FB] FirebaseBridge set to DontDestroyOnLoad.");
        }

        SceneManager.sceneLoaded += OnSceneLoadedReapply;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoadedReapply;
    }

    private void Start()
    {
        // Restore per-mapping material keys (by dataId)
        if (persistMaterialKeys)
        {
            foreach (var m in sceneMappings)
            {
                if (!string.IsNullOrEmpty(m.dataId))
                {
                    var saved = PlayerPrefs.GetString(GetMatKeyPrefKey(m.dataId), string.Empty);
                    if (!string.IsNullOrEmpty(saved)) m.materialKey = saved;
                }
            }
        }

        RestoreLastGlobalSkybox();
    }

    private void OnSceneLoadedReapply(Scene scene, LoadSceneMode mode)
    {
        RestoreLastGlobalSkybox();
        if (_lastDtos != null && _lastDtos.Length > 0 && sceneMappings.Count > 0)
        {
            StartCoroutine(AutoApplyByIdCoroutine());
        }
    }

    private void RestoreLastGlobalSkybox()
    {
        var lastName = PlayerPrefs.GetString(PP_LAST_SKYBOX_NAME, string.Empty);
        if (!string.IsNullOrEmpty(lastName))
        {
            Debug.Log($"[FB] (Info) Last global skybox stored: {lastName}");
        }
    }

    // ==================== Entry point from JS =====================
    // index.html -> unityInstance.SendMessage("FirebaseBridge", "OnPreloadDataReceived", json);
    public void OnPreloadDataReceived(string json)
    {
        Debug.Log($"[FB] Received JSON: {json}");

        // Hand the data to the shared bus so thumbnails & other listeners update.
        SceneDataBus.ApplyJson(json);

        // Convert   JSON to C# objects
        var dtos = JsonHelper.FromJson<SceneDto>(json);
        if (dtos == null || dtos.Length == 0)
        {
            Debug.LogWarning("[FB] No scenes in payload.");
            return;
        }

        _lastDtos = dtos;
        _dtoById.Clear();
        foreach (var d in dtos)
        {
            if (!string.IsNullOrEmpty(d.Id))
                _dtoById[d.Id] = d;
        }
        Debug.Log($"[FB] Cached {_dtoById.Count} scenes by Id");

        StartCoroutine(StartLoadingWithDelay());
    }

    private IEnumerator StartLoadingWithDelay()
    {
        yield return new WaitForSeconds(3.0f); // Delay 3 second before actual loading starts
        StartCoroutine(AutoApplyByIdCoroutine());
    }


    // Iterate mappings and handle by Id only
    private IEnumerator AutoApplyByIdCoroutine()
    {
        for (int i = 0; i < sceneMappings.Count; i++)
        {
            var mapping = sceneMappings[i]; // define mapping before using it

            // Skip mapping with no dataId
            if (string.IsNullOrWhiteSpace(mapping.dataId))
            {
                Debug.Log("[FB] Mapping has empty Data Id — skipping.");
                continue;
            }

            // Find correct DTO based on dataId
            if (!_dtoById.TryGetValue(mapping.dataId.Trim(), out var dto))
            {
                Debug.Log($"[FB] No DTO for Data Id: '{mapping.dataId}'.");
                continue;
            }   

            // Pass the index into the coroutine
            yield return ApplyMappingByDto(mapping, dto, i);

            if (homeManager != null)
                homeManager.UpdateLoadingProgress(i + 1, sceneMappings.Count);
            else
                Debug.LogWarning("[FB] homeManager reference is null!");

        }
        // After all mappings, save PlayerPrefs, Unity knows which S3Key was last applied
        if (persistMaterialKeys)
            PlayerPrefs.Save();
    }

    private IEnumerator ApplyMappingByDto(SceneMapping mapping, SceneDto dto, int sceneIndex)
    {
        // -------- resolve runtime material --------
        var mat = GetRuntimeMaterial(mapping.material);
        if (mat == null)
        {
            if (mapping.targetObject == null && fallbackSkybox != null)
            {
                Debug.LogWarning("[FB] No material in mapping; using global fallback skybox.");
                mat = GetRuntimeMaterial(fallbackSkybox);
            }
            else
            {
                Debug.LogError("[FB] No material and no fallback available for mapping.");
                yield break;
            }
        }

        // -------- compare keys --------
        var incomingKey = NormalizeKey(dto.S3Key ?? "");
        var currentKey = NormalizeKey(mapping.materialKey ?? "");
        string targetName = mapping.targetObject ? mapping.targetObject.name : "(Global Skybox)";

        Debug.Log($"[FB][Compare]\n────────────────────────────────────────\n" +
                  $"Target Object: {targetName}\nMaterial Key: \"{currentKey}\"  VS  S3Key: \"{incomingKey}\"\n" +
                  $"────────────────────────────────────────");

        bool success = false;

        // -------- if same key → just use fallback texture --------
        if (!string.IsNullOrEmpty(incomingKey) && incomingKey == currentKey)
        {
            // Apply fallback 3D texture
            if (mapping.fallbackTexture != null && mat.HasProperty("_MainTex"))
            {
                mat.SetTexture("_MainTex", mapping.fallbackTexture);
                success = true;
                Debug.Log($"[FB] Keys match → applied fallback texture '{mapping.fallbackTexture.name}', skipped download.");
            }   

            // Apply fallback UI sprite too
            if (mapping.fallbackSprite != null)
            {
                if (mapping.image_button1 != null)
                    mapping.image_button1.sprite = mapping.fallbackSprite;
                if (mapping.image_button2 != null)
                    mapping.image_button2.sprite = mapping.fallbackSprite;

                Debug.Log($"[FB] Applied shared fallback sprite to both buttons for '{mapping.targetObject?.name}'.");
            }
        }

        else
        {
            // -------- otherwise, try to download new texture --------
            if (!string.IsNullOrEmpty(dto.ImageUrl))
            {
                var url = SceneJsonUtil.SafeUrl(dto.ImageUrl);
                using (var www = UnityWebRequestTexture.GetTexture(url, nonReadable: true))
                {
                    www.timeout = 20;
                    www.SetRequestHeader("Cache-Control", "no-cache, no-store, must-revalidate");
                    www.SetRequestHeader("Pragma", "no-cache");
                    www.SetRequestHeader("Expires", "0");

                    Debug.Log("[FB] GET " + url);
                    yield return www.SendWebRequest();

                #if UNITY_2020_2_OR_NEWER
                    var ok = www.result == UnityWebRequest.Result.Success;
                #else
                    var ok = !www.isNetworkError && !www.isHttpError;
                #endif
                    if (ok)
                    {
                        var downloaded = DownloadHandlerTexture.GetContent(www);
                        if (downloaded != null)
                        {
                            var processed = ProcessTexture(downloaded);
                            if (processed != null)
                            {
                                mat.SetTexture("_MainTex", processed);
                                _texByS3Key[incomingKey] = processed;
                                success = true;
                                Debug.Log($"[FB] Downloaded and applied texture for '{incomingKey}'.");
                            }

                            // Also apply to preview UI image if assigned
                            Sprite newSprite = Sprite.Create(
                                    processed,
                                    new Rect(0, 0, processed.width, processed.height),
                                    new Vector2(0.5f, 0.5f)
                                );

                                if (mapping.image_button1 != null)
                                {
                                    mapping.image_button1.sprite = newSprite;
                                    Debug.Log($"[FB] Applied image to Button 1 preview for '{mapping.targetObject?.name}'.");
                                }

                                if (mapping.image_button2 != null)
                                {
                                    mapping.image_button2.sprite = newSprite;
                                    Debug.Log($"[FB] Applied image to Button 2 preview for '{mapping.targetObject?.name}'.");
                                }


                        }
                    }
                    else
                    {
                        Debug.LogError($"[FB] Texture GET failed: {url} ({www.responseCode}) {www.error}");
                    }
                }
            }

            // -------- if download fails → fallback --------
            if (!success && mapping.fallbackTexture != null)
            {
                mat.SetTexture("_MainTex", mapping.fallbackTexture);
                success = true;
                Debug.Log($"[FB] Download failed → fallback texture '{mapping.fallbackTexture.name}' used.");
            }

            // Save new key only if success
            if (success && !string.IsNullOrEmpty(incomingKey))
            {
                mapping.materialKey = incomingKey;
                if (persistMaterialKeys)
                    PlayerPrefs.SetString(GetMatKeyPrefKey(mapping.dataId), incomingKey);
            }
        }

        if (mapping.targetObject == null)
        {
            // GLOBAL skybox apply (immediate visit log)
            RenderSettings.skybox = mat;
            DynamicGI.UpdateEnvironment();
            PlayerPrefs.SetString(PP_LAST_SKYBOX_NAME, dto.SceneTitle ?? mat.name);
            Debug.Log($"[FB] Applied GLOBAL skybox: '{dto.SceneTitle ?? mat.name}'");

            // ✅ Log visit immediately (since always visible)
            if (!_visitedIds.Contains(dto.Id))
            {
                _visitedIds.Add(dto.Id);
                AnalyticsBridge.Increment(dto.Id, dto.SceneTitle ?? "", sceneIndex);
                AnalyticsBridge.Log(dto.Id, dto.SceneTitle ?? "", sceneIndex);
                Debug.Log($"[FB]  Recorded visit for global skybox '{dto.SceneTitle}'.");
            }

            yield break;
        }

        var target = mapping.targetObject;
        var camSky = target.GetComponent<Skybox>();
        if (camSky != null)
        {
            camSky.material = mat;
            DynamicGI.UpdateEnvironment();
            Debug.Log($"[FB]  Applied Camera Skybox on '{target.name}'.");
        }
        else if (target.TryGetComponent<Renderer>(out var renderer))
        {
            renderer.material = mat;
            Debug.Log($"[FB]  Applied material to Renderer on '{target.name}'.");
        }
        else
        {
            RenderSettings.skybox = mat;
            DynamicGI.UpdateEnvironment();
            Debug.LogWarning($"[FB] '{target.name}' has no Skybox/Renderer → applied as GLOBAL skybox.");

            if (!_visitedIds.Contains(dto.Id))
            {
                _visitedIds.Add(dto.Id);
                AnalyticsBridge.Increment(dto.Id, dto.SceneTitle ?? "", sceneIndex);
                AnalyticsBridge.Log(dto.Id, dto.SceneTitle ?? "", sceneIndex);
                Debug.Log($"[FB] Recorded visit for '{dto.SceneTitle}' via fallback global apply.");
            }

            yield break;
        }

        // -------- start tracking when object becomes active --------
        StartCoroutine(TrackTargetActivation(target, dto.Id, dto.SceneTitle ?? target.name, sceneIndex));
    }



    // -------------------- Helpers --------------------
    // ======================================================================
    //  Visit tracking — log when GameObject becomes active
    // ======================================================================
    private IEnumerator TrackTargetActivation(GameObject target, string id, string title, int index)
    {
        if (target == null || string.IsNullOrEmpty(id)) yield break;

        bool wasActive = target.activeInHierarchy;

        while (true)
        {
            bool isActive = target.activeInHierarchy;

            // Only trigger when state changes from inactive -> active
            if (!wasActive && isActive)
            {
                AnalyticsBridge.Increment(id, title ?? "", index);
                AnalyticsBridge.Log(id, title ?? "", index);
                Debug.Log($"[FB] Visit recorded for '{title ?? target.name}' (id={id}) because GameObject became active.");
            }

            wasActive = isActive;
            yield return new WaitForSeconds(0.5f); // check every half second (lightweight)
        }
    }

    private static string NormalizeKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;
        key = key.Trim();
        key = key.Replace("\\", "/");     // unify separators
        while (key.Contains("  ")) key = key.Replace("  ", " "); // collapse double spaces
                                                                 // DO NOT lower-case: S3 is case-sensitive
        return key;
    }

    private string GetMatKeyPrefKey(string dataId)
    {
        return PP_MATKEY_PREFIX + (dataId == null ? string.Empty : dataId.Trim());
    }

    private Material GetRuntimeMaterial(Material src)
    {
        if (src == null) return null;
        if (_runtimeMatCache.TryGetValue(src, out var cached) && cached != null) return cached;
        var clone = new Material(src); // avoid mutating the asset
        _runtimeMatCache[src] = clone;
        return clone;
    }

    // WebGL-safe caps (smaller = safer)
    #if UNITY_WEBGL && !UNITY_EDITOR
    const int MAX_PANO_SIDE = 4096;   // you can drop to 2048 if memory is still tight
    const bool GENERATE_MIPS = false; // save memory on WebGL
    #else
        const int MAX_PANO_SIDE = 8192;
        const bool GENERATE_MIPS = true;
#endif

    private Texture2D ProcessTexture(Texture2D src)
    {
        if (src == null) return null;

        int deviceMax = SystemInfo.maxTextureSize > 0 ? SystemInfo.maxTextureSize : MAX_PANO_SIDE;
        int clamp = Mathf.Min(MAX_PANO_SIDE, deviceMax);

        // Decide final size (cap to clamp)
        int w = src.width, h = src.height;
        if (w > clamp || h > clamp)
        {
            GetDownscaleSize(w, h, clamp, out w, out h);
            Debug.Log($"[FB] Large pano {src.width}x{src.height} → downscale to {w}x{h}");
        }

        // Blit to a new texture (no CPU readback of src), then free the big source
        var finalTex = BlitToNewTexture(src, w, h, GENERATE_MIPS);
        if (finalTex == null) return null;

        // Free the original GPU texture ASAP
        if (src != null) Destroy(src);

        finalTex.wrapMode = TextureWrapMode.Clamp;
        finalTex.filterMode = FilterMode.Trilinear;
        finalTex.anisoLevel = 4;
        return finalTex;
    }

    private Texture2D BlitToNewTexture(Texture source, int w, int h, bool generateMips)
    {
        RenderTexture rt = null;
        var prev = RenderTexture.active;
        try
        {
            // Use Default so Unity chooses sRGB vs Linear correctly for the current project color space
            rt = RenderTexture.GetTemporary(
                w, h, 0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default
            );

            Graphics.Blit(source, rt);
            RenderTexture.active = rt;

            // Create as sRGB color texture (linear=false). This avoids gamma shifts.
            var dst = new Texture2D(w, h, TextureFormat.RGBA32, generateMips, /*linear:*/ false);
            dst.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            dst.Apply(generateMips, true);   // generate mips (if requested) and make non-readable
            return dst;
        }
        finally
        {
            RenderTexture.active = prev;
            if (rt != null) RenderTexture.ReleaseTemporary(rt);
        }
    }


    private void GetDownscaleSize(int w, int h, int maxSide, out int newW, out int newH)
    {
        float s = Mathf.Min((float)maxSide / w, (float)maxSide / h);
        if (s >= 1f) { newW = w; newH = h; return; }
        newW = Mathf.Max(1, Mathf.RoundToInt(w * s));
        newH = Mathf.Max(1, Mathf.RoundToInt(h * s));
    }
}
