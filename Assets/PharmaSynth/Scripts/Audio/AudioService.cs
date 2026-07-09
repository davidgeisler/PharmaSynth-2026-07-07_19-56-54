using UnityEngine;

/// Audio mixer categories. Volume sliders in Settings map 1:1 to these.
public enum AudioCategory { Sfx, Ambient, Voice, Music }

/// Pure volume maths — a perceptual linear(0..1)→decibel curve for an AudioMixer
/// exposed parameter. Kept separate so it is unit-testable without a mixer asset.
public static class VolumeUtil
{
    public const float MinDb = -80f;

    /// 0 → silence (-80 dB), 1 → 0 dB, with a log taper that sounds linear to the ear.
    public static float LinearToDb(float linear01)
    {
        linear01 = Mathf.Clamp01(linear01);
        if (linear01 <= 0.0001f) return MinDb;
        return Mathf.Clamp(Mathf.Log10(linear01) * 20f, MinDb, 0f);
    }
}

/// One-stop audio playback + volume control. Holds a per-category AudioSource and a
/// SoundBank; gameplay calls Play("key") / PlayAt(...) and sets category volumes
/// (persisted to PlayerPrefs, and pushed to an optional AudioMixer's exposed
/// "<Category>Volume" params). Clips are assigned in the SoundBank later — the whole
/// service works (as silent no-ops) with zero audio assets, so it can ship now.
public class AudioService : MonoBehaviour
{
    public static AudioService Instance { get; private set; }

    [SerializeField] private SoundBank bank;
    [SerializeField] private AudioSource sfxSource;      // one-shot
    [SerializeField] private AudioSource ambientSource;  // looping
    [SerializeField] private AudioSource voiceSource;    // Pharmee beeps
    [SerializeField] private AudioSource musicSource;    // looping
    [SerializeField] private UnityEngine.Audio.AudioMixer mixer;   // optional

    private readonly float[] _vol = { 1f, 1f, 1f, 1f };
    private bool _loaded;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        LoadVolumes();
    }

    // ---- volume ----------------------------------------------------------

    public float VolumeOf(AudioCategory c) => _vol[(int)c];

    public void SetVolume(AudioCategory c, float linear01)
    {
        _vol[(int)c] = Mathf.Clamp01(linear01);
        Apply(c);
        PlayerPrefs.SetFloat(PrefKey(c), _vol[(int)c]);
    }

    public void LoadVolumes()
    {
        if (_loaded) return;
        for (int i = 0; i < _vol.Length; i++)
        {
            _vol[i] = PlayerPrefs.GetFloat(PrefKey((AudioCategory)i), 1f);
            Apply((AudioCategory)i);
        }
        _loaded = true;
    }

    private void Apply(AudioCategory c)
    {
        // Push to the mixer if wired; also gate the raw AudioSources as a fallback.
        if (mixer != null) mixer.SetFloat(c + "Volume", VolumeUtil.LinearToDb(_vol[(int)c]));
        var src = SourceFor(c);
        if (src != null && mixer == null) src.volume = _vol[(int)c];
    }

    private static string PrefKey(AudioCategory c) => "audio.vol." + c;

    // ---- playback --------------------------------------------------------

    /// Play a bank sound by key on its category source (no-op if key/clip missing).
    public void Play(string key)
    {
        var e = bank != null ? bank.Get(key) : null;
        if (e == null || e.clip == null) return;
        var src = SourceFor(e.category);
        if (src == null) return;
        float v = _vol[(int)e.category] * Mathf.Clamp01(e.volume);
        if (e.category == AudioCategory.Ambient || e.category == AudioCategory.Music)
        {
            src.clip = e.clip; src.loop = true; src.volume = mixer == null ? v : 1f; src.Play();
        }
        else if (e.category == AudioCategory.Voice)
        {
            // Pharmee's beeps must NOT stack — a new line interrupts the current beep
            // instead of piling PlayOneShots on top of each other (which got deafening).
            src.loop = false; src.clip = e.clip;
            src.volume = mixer == null ? v : Mathf.Clamp01(e.volume);
            src.Play();
        }
        else src.PlayOneShot(e.clip, mixer == null ? v : Mathf.Clamp01(e.volume));
    }

    /// Play a bank sound at a world position (spawns a temporary one-shot source).
    public void PlayAt(string key, Vector3 pos)
    {
        var e = bank != null ? bank.Get(key) : null;
        if (e == null || e.clip == null) return;
        AudioSource.PlayClipAtPoint(e.clip, pos, _vol[(int)e.category] * Mathf.Clamp01(e.volume));
    }

    public void StopAmbient() { if (ambientSource != null) ambientSource.Stop(); }

    // One-arg void wrappers for UI slider onValueChanged persistent listeners.
    public void SetSfxVolume(float v)     => SetVolume(AudioCategory.Sfx, v);
    public void SetAmbientVolume(float v) => SetVolume(AudioCategory.Ambient, v);
    public void SetVoiceVolume(float v)   => SetVolume(AudioCategory.Voice, v);
    public void SetMusicVolume(float v)   => SetVolume(AudioCategory.Music, v);

    private AudioSource SourceFor(AudioCategory c)
    {
        switch (c)
        {
            case AudioCategory.Sfx: return sfxSource;
            case AudioCategory.Ambient: return ambientSource;
            case AudioCategory.Voice: return voiceSource;
            case AudioCategory.Music: return musicSource;
            default: return sfxSource;
        }
    }

    /// Edit-mode/test binding (Awake/serialization don't run on AddComponent).
    public void Bind(SoundBank b, AudioSource sfx, AudioSource ambient, AudioSource voice, AudioSource music)
    { bank = b; sfxSource = sfx; ambientSource = ambient; voiceSource = voice; musicSource = music; }
}
