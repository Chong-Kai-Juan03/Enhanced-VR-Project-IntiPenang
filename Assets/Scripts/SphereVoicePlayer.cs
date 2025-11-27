using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class SphereVoicePlayer : MonoBehaviour
{
    [Tooltip("Voice clip to play when this sphere is active.")]
    public AudioClip voiceClip;

    [Tooltip("Play automatically when the sphere activates.")]
    public bool playOnEnable = true;

    private AudioSource voiceSource;

    private void Awake()
    {
        voiceSource = GetComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        voiceSource.loop = false;
        voiceSource.spatialBlend = 1f; // makes the voice 3D
    }

    private void OnEnable()
    {
        if (!playOnEnable || voiceClip == null)
            return;

        //  if another sound is already playing, skip to avoid overlapping voices
        if (AudioManager.Instance != null && AudioManager.Instance.IsAnyAudioPlaying())
        {
            Debug.Log($"[SphereVoicePlayer] Skipping {gameObject.name} voice — another sound is playing.");
            return;
        }

        // Play the voice clip
        voiceSource.clip = voiceClip;
        voiceSource.volume = AudioManager.Instance != null
            ? AudioManager.Instance.voiceVolume
            : 1.2f;
        voiceSource.Play();

        Debug.Log($"[SphereVoicePlayer] Started voice for {gameObject.name}");

        //  print out active sources for debugging
        AudioManager.Instance?.DebugPrintPlayingSources();
    }
    private void OnDisable()
    {
        if (voiceSource.isPlaying)
        {
            voiceSource.Stop();
            Debug.Log($"[SphereVoicePlayer] Stopped voice for {gameObject.name}");
        }
    }
}
