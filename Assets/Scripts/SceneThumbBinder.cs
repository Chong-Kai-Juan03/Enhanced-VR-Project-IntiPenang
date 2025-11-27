using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Text.RegularExpressions;
using System.Linq;

public class SceneThumbBinder : MonoBehaviour
{
    public enum MatchKey { Id, S3Key }

    [Header("Match")]
    public MatchKey matchBy = MatchKey.S3Key;
    [Tooltip("Paste the Id or S3Key that this button represents")]
    public string dataKey;

    [Header("UI")]
    public Image thumbnail;
    public TMP_Text titleTMP;
    public Text titleText;
    public Sprite placeholder;

    [Header("Startup Gate")]
    [Tooltip("If true, this card must finish its first bind before the loader can close.")]
    public bool participatesInStartupGate = false;

    [Header("Debug")]
    public bool debug = true;

    public SceneDto BoundData { get; private set; }

    // -------- gate bookkeeping (static across all binders) --------
    private static int s_pendingGateCards = 0;
    private static bool s_gateFired = false;


    void Awake()
    {
        // Subscribe even if GameObject is inactive
        SceneDataBus.DataChanged += Refresh;
    }

    void OnDestroy()
    {
        // Clean unsubscribe to avoid leaks
        SceneDataBus.DataChanged -= Refresh;
    }


    void OnValidate()
    {
        if (!thumbnail) thumbnail = GetComponentInChildren<Image>();
        if (!titleTMP) titleTMP = GetComponentInChildren<TMP_Text>();
        if (!titleText) titleText = GetComponentInChildren<Text>();
    }

    void OnEnable()
    {
        if (thumbnail && placeholder) thumbnail.sprite = placeholder;

        // Register for data updates
        //SceneDataBus.DataChanged += Refresh;

        // Gate: count only once per enable
        if (participatesInStartupGate)
        {
            s_pendingGateCards++;
            if (debug) Debug.Log($"[Binder] Gate +1 → pending={s_pendingGateCards}", this);
        }

        if (SceneDataBus.HasData) Refresh();
    }

    void OnDisable()
    {
        //SceneDataBus.DataChanged -= Refresh;

        // If this card was pending but got disabled before finishing, release it
        if (participatesInStartupGate)
        {
            // Don’t allow negative counts
            if (s_pendingGateCards > 0) s_pendingGateCards--;
            if (debug) Debug.Log($"[Binder] Gate (disabled) -1 → pending={s_pendingGateCards}", this);
            TryCloseGate();
        }
    }

    // Call when this card is definitely “ready” for startup purposes
    void MarkGateDone()
    {
        if (!participatesInStartupGate) return;
        participatesInStartupGate = false; // ensure we only mark once
        if (s_pendingGateCards > 0) s_pendingGateCards--;
        if (debug) Debug.Log($"[Binder] Gate done -1 → pending={s_pendingGateCards}", this);
        TryCloseGate();
    }

    static void TryCloseGate()
    {
        if (!s_gateFired && s_pendingGateCards <= 0)
        {
            s_gateFired = true;

        }

    }

    void Log(string msg) { if (debug) Debug.Log("[Binder] " + msg, this); }
    void Warn(string msg) { if (debug) Debug.LogWarning("[Binder] " + msg, this); }
    void Err(string msg) { Debug.LogError("[Binder] " + msg, this); }

    public void Refresh()
    {
        var key = (dataKey ?? "").Trim();
        if (string.IsNullOrEmpty(key)) { Warn("Empty dataKey; nothing to bind."); MarkGateDone(); return; }

        if (matchBy == MatchKey.S3Key)
        {
            if (!SceneDataBus.TryGetByS3(key, out var dto))
            {
                Warn($"No DTO for S3Key='{key}'. byS3 count={GetCountS3()}");
                SetTitle("(Not found)");
                MarkGateDone(); // don’t block startup because this one is missing
                return;
            }
            BindDto(dto);
        }
        else
        {
            if (!SceneDataBus.TryGetById(key, out var dto))
            {
                Warn($"No DTO for Id='{key}'. byId count={GetCountId()}");
                SetTitle("(Not found)");
                MarkGateDone();
                return;
            }
            BindDto(dto);
        }
    }

    void BindDto(SceneDto dto)
    {
        BoundData = dto;
        Log($"BindDto: Id='{dto.Id}', S3Key='{dto.S3Key}', Title='{dto.SceneTitle}'");

        // Title
        var t = string.IsNullOrEmpty(dto.SceneTitle) ? "(No title)" : dto.SceneTitle;
        SetTitle(t);

        // Thumbnail pipeline
        if (thumbnail == null) { Warn("No thumbnail Image assigned."); MarkGateDone(); return; }
        if (string.IsNullOrEmpty(dto.ImageUrl)) { Warn("DTO has empty ImageUrl."); MarkGateDone(); return; }

        if (SceneDataBus.TryGetCached(dto.ImageUrl, out var cached))
        {
            thumbnail.sprite = cached;
            Log("Used cached sprite: " + dto.ImageUrl);
            MarkGateDone();
            return;
        }

        StartCoroutine(SceneJsonUtil.LoadSprite(dto.ImageUrl, spr =>
        {
            SceneDataBus.PutCache(dto.ImageUrl, spr);
            thumbnail.sprite = spr;
            Log("Downloaded & applied sprite.");

            //Debug.Log("[Binder] All thumbnails ready → Hide bootstrap");
            //var hm = FindObjectOfType<HomeManager>();
            //if (hm != null)
            //{
            //    Debug.Log("[FB] Data received → hiding Bootstrap");
            //    hm.HideBootstrapAndShowHome();
            //}

            MarkGateDone();
        },
        err =>
        {
            Err("LoadSprite error: " + err + " | url=" + dto.ImageUrl);
            // Still release the gate so the app can start
            MarkGateDone();
        }));
    }

    void SetTitle(string s)
    {
        var formatted = FormatTitle_FirstTwoThenRest(s);

        if (titleTMP)
        {
            titleTMP.enableWordWrapping = true;
            titleTMP.overflowMode = TextOverflowModes.Ellipsis;
            titleTMP.maxVisibleLines = 2;
            titleTMP.alignment = TextAlignmentOptions.Center;
            titleTMP.text = formatted;
        }
        if (titleText)
        {
            titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.text = formatted;
        }
        if (!titleTMP && !titleText) Warn("No title text component assigned.");
    }

    // --- formatting helpers (unchanged) ---
    private string FormatTitle_FirstTwoThenRest(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var parts = Regex.Split(raw.Trim(), @"\s+").Where(p => p.Length > 0).ToArray();

        if (parts.Length == 1) return InsertZeroWidthSpacesEveryN(parts[0], 10);
        if (parts.Length == 2) return parts[0] + " " + parts[1];

        var line1 = parts[0] + " " + parts[1];
        var line2 = string.Join(" ", parts, 2, parts.Length - 2);
        line2 = string.Join(" ", line2.Split(' ')
                         .Select(tok => tok.Length >= 14 ? InsertZeroWidthSpacesEveryN(tok, 8) : tok));
        return line1 + "\n" + line2;
    }

    private string InsertZeroWidthSpacesEveryN(string s, int n)
    {
        if (string.IsNullOrEmpty(s) || n <= 0) return s;
        var separators = new[] { "-", "_", "/", "\\", "(", ")", "[", "]" };
        foreach (var sep in separators) s = s.Replace(sep, "\u200B" + sep + "\u200B");

        var result = new System.Text.StringBuilder(s.Length + s.Length / n);
        int run = 0;
        foreach (var ch in s)
        {
            result.Append(ch);
            run++;
            if (run >= n) { result.Append('\u200B'); run = 0; }
        }
        return result.ToString();
    }

    // --- debug peeks (unchanged) ---
    int GetCountId()
    {
        var f = typeof(SceneDataBus).GetField("byId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var dict = f?.GetValue(null) as System.Collections.IDictionary;
        return dict != null ? dict.Count : -1;
    }
    int GetCountS3()
    {
        var f = typeof(SceneDataBus).GetField("byS3", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var dict = f?.GetValue(null) as System.Collections.IDictionary;
        return dict != null ? dict.Count : -1;
    }

    public string GetS3Key() => BoundData?.S3Key ?? "";
    public string GetId() => BoundData?.Id ?? "";
}
