using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Background Music")]
    public AudioClip[] songs;
    public Slider volumeSlider;
    [Range(0f, 1f)] public float bgVolume = 0.6f;
    private AudioSource bgSource;
    private int currentIndex = 0;

    [Header("Voice Settings")]
    [Range(0f, 2f)] public float voiceVolume = 1.2f;

    [Header("Auto Fade Settings")]
    [Range(0f, 1f)] public float reducedVolume = 0.2f;
    [Range(0.05f, 2f)] public float fadeSpeed = 1f;

    private Coroutine fadeRoutine;
    private bool bgPaused = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        bgSource = gameObject.AddComponent<AudioSource>();
        bgSource.loop = false;
        bgSource.playOnAwake = false;
        bgSource.volume = bgVolume;
    }

    void Start()
    {
        if (songs != null && songs.Length > 0)
            PlayNextSong();

        if (volumeSlider != null)
        {
            volumeSlider.value = bgVolume;
            volumeSlider.onValueChanged.AddListener(v => SetVolume(v));
        }

        StartCoroutine(MonitorAudioPriority());
    }

    // ===============================================================
    // 🔹 Background Music
    // ===============================================================
    private void PlayNextSong()
    {
        if (songs == null || songs.Length == 0) return;
        currentIndex = (currentIndex + 1) % songs.Length;
        bgSource.clip = songs[currentIndex];
        bgSource.volume = bgVolume;
        bgSource.loop = true;
        bgSource.Play();
    }

    public void SetVolume(float v)
    {
        bgVolume = v;
        bgSource.volume = v;
    }

    // ===============================================================
    // 🔹 Detect audio priority in scene
    // ===============================================================
    public bool IsAnyVoicePlaying()
    {
        var sources = FindObjectsOfType<AudioSource>(true)
            .Where(s => s.isPlaying && s != bgSource)
            .ToList();

        // Filter video or sphere voices (not bg music)
        return sources.Any();
    }

    private IEnumerator MonitorAudioPriority()
    {
        while (true)
        {
            bool voiceActive = IsAnyVoicePlaying();

            // When any voice/video plays → stop BGM
            if (voiceActive && bgSource.isPlaying && !bgPaused)
            {
                Debug.Log("[AudioManager] 🔇 Voice/video detected — pausing background music.");
                bgSource.Pause();
                bgPaused = true;
            }
            // When silence → resume BGM
            else if (!voiceActive && bgPaused)
            {
                Debug.Log("[AudioManager] 🎵 No active voice — resuming background music.");
                bgSource.UnPause();
                bgPaused = false;
            }

            yield return new WaitForSeconds(0.3f);
        }
    }

    // Optional for debug
    public void DebugPrintPlayingSources()
    {
        var sources = FindObjectsOfType<AudioSource>(true);
        foreach (var s in sources)
        {
            if (s.isPlaying)
                Debug.Log($"[AudioManager] 🔊 '{s.gameObject.name}' playing (clip: {s.clip?.name})");
        }
    }

    public bool IsAnyAudioPlaying() => IsAnyVoicePlaying();
}
