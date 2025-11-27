using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

// -------------------------------------------------------------
// Data model: one row from your Firebase JSON
// -------------------------------------------------------------
[Serializable]
public class SceneDto
{
    public string Id;
    public string SceneTitle;
    public string S3Key;
    public string ImageUrl;
    public string Building;
    public string Level;
}

// -------------------------------------------------------------
// JSON + Image helpers (uses your JsonHelper.FromJson<T>)
// -------------------------------------------------------------
public static class SceneJsonUtil


{
    public static List<SceneDto> Parse(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            Debug.LogWarning("[Core] Parse: rawJson empty");
            return new List<SceneDto>();
        }
        var arr = JsonHelper.FromJson<SceneDto>(rawJson);   // using your JsonHelper
        var list = (arr != null && arr.Length > 0) ? new List<SceneDto>(arr) : new List<SceneDto>();
        Debug.Log($"[Core] Parse: {list.Count} items");
        if (list.Count > 0)
        {
            int n = Mathf.Min(3, list.Count);
            for (int i = 0; i < n; i++)
            {
                Debug.Log($"[Core] item[{i}] Id='{list[i].Id}', S3Key='{list[i].S3Key}', Title='{list[i].SceneTitle}'");
            }
        }
        return list;
    }


    // Escape spaces/unsafe chars so UnityWebRequest doesn't fail
    public static string SafeUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        return Uri.EscapeUriString(url);
    }

    // Download an image URL and return a Sprite (for UI Image)
    public static IEnumerator LoadSprite(string url, Action<Sprite> onOk, Action<string> onFail)
    {
        url = SafeUrl(url);
        if (string.IsNullOrEmpty(url)) { onFail?.Invoke("Empty URL"); yield break; }

        using (var req = UnityWebRequestTexture.GetTexture(url))
        {
            req.disposeDownloadHandlerOnDispose = true;
            req.timeout = 15; // seconds
            Debug.Log($"[Core] LoadSprite: GET {url}");
            yield return req.SendWebRequest();

        #if UNITY_2020_2_OR_NEWER
                    if (req.result != UnityWebRequest.Result.Success)
        #else
                if (req.isNetworkError || req.isHttpError)
        #endif
            {
                var err = $"HTTP fail ({req.responseCode}): {req.error}";
                Debug.LogError("[Core] LoadSprite: " + err);
                onFail?.Invoke(err);
                yield break;
            }

            var tex = DownloadHandlerTexture.GetContent(req);
            if (tex == null)
            {
                onFail?.Invoke("Null texture");
                Debug.LogError("[Core] LoadSprite: Null texture");
                yield break;
            }

            var spr = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            Debug.Log($"[Core] LoadSprite: OK {tex.width}x{tex.height}");
            onOk?.Invoke(spr);
        }
    }
}

// -------------------------------------------------------------
// In-memory data bus: index by Id / S3Key + sprite cache
// -------------------------------------------------------------
public static class SceneDataBus
{
    public static bool HasData { get; private set; }
    public static event Action DataChanged;

    private static readonly Dictionary<string, SceneDto> byId = new Dictionary<string, SceneDto>();
    private static readonly Dictionary<string, SceneDto> byS3 = new Dictionary<string, SceneDto>();
    private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

    // Called once when your Firebase bridge delivers the JSON string
    public static void ApplyJson(string rawJson)
    {
        var list = SceneJsonUtil.Parse(rawJson);

        byId.Clear(); byS3.Clear();
        foreach (var d in list)
        {
            if (!string.IsNullOrEmpty(d.Id)) byId[d.Id] = d;
            if (!string.IsNullOrEmpty(d.S3Key)) byS3[d.S3Key] = d;
        }

        HasData = true;
        Debug.Log($"[Core] Store ready: byId={byId.Count}, byS3={byS3.Count}");
        DataChanged?.Invoke();
    }


    public static bool TryGetById(string id, out SceneDto dto) => byId.TryGetValue(id, out dto);
    public static bool TryGetByS3(string s3Key, out SceneDto dto) => byS3.TryGetValue(s3Key, out dto);

    public static bool TryGetCached(string url, out Sprite sprite) => spriteCache.TryGetValue(url, out sprite);
    public static void PutCache(string url, Sprite sprite)
    {
        if (!string.IsNullOrEmpty(url) && sprite != null) spriteCache[url] = sprite;
    }
}

// -------------------------------------------------------------
// Optional: a tiny receiver you can call from your bridge
// -------------------------------------------------------------
// Usage from your existing FirebaseBridge (C# or JS->C# hook):
// FindObjectOfType<SceneDataReceiver>()?.OnFirebaseJsonReceived(jsonString);
public class SceneDataReceiver : MonoBehaviour
{
    public void OnFirebaseJsonReceived(string rawJson)
    {
        Debug.Log("[SceneDataReceiver] JSON received. Length = " + (rawJson == null ? 0 : rawJson.Length));
        SceneDataBus.ApplyJson(rawJson);
    }
}

// -------------------------------------------------------------
// (Optional) Empty MonoBehaviour so the file name matches
// You don't have to use this; it's here to keep your original class.
// -------------------------------------------------------------
public class SceneDataCore : MonoBehaviour { }
