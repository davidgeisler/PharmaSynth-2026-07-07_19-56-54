using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class NPCNarrationController : MonoBehaviour
{
    [System.Serializable]
    public class NarrationLine
    {
        [TextArea(2, 4)] public string subtitle;
        public AudioClip voiceClip;
        public float fallbackSeconds = 3f;
    }

    [Header("References")]
    [SerializeField] private AudioSource narratorAudioSource;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private GameObject skipButton;
    [Tooltip("The visible bubble/panel behind the subtitle — shown only while a line is speaking.")]
    [SerializeField] private GameObject panelRoot;

    [Header("Sequence")]
    [SerializeField] private List<NarrationLine> tutorialLines = new List<NarrationLine>();
    [SerializeField] private bool playOnStart = true;

    [Header("Typewriter (user 2026-07-10: type lines out + talking blips)")]
    [SerializeField] private bool typewriter = true;
    [SerializeField] private float charsPerSecond = 32f;
    [SerializeField] private int blipEveryChars = 2;            // a voice blip every N revealed non-space chars
    [SerializeField] private string voiceBlipKey = "";          // this speaker's talking blip (SoundBank key)
    [SerializeField, Range(0f, 1f)] private float blipVolume = 0.5f;

    [Header("Voice-over (user 2026-07-10: NPCs speak their lines)")]
    [SerializeField] private VoiceBank voiceBank;               // hash-keyed clip lookup (optional)
    [SerializeField] private VoiceSpeaker speaker = VoiceSpeaker.Pharmee;

    [Header("Events")]
    public UnityEvent onNarrationFinished;

    /// Per-line hooks: the overhead bubble AND the HUD dialogue bar both mirror these.
    public event Action<string, float> LineStarted;
    public event Action LineEnded;

    /// True while a line is on screen (between BeginLine and EndLine).
    public bool IsSpeaking { get; private set; }

    /// Typewriter state — the HUD dialogue bar mirrors these to stay in sync.
    public bool Typewriter => typewriter;
    public float TypeCps() => typewriter ? Mathf.Max(1f, charsPerSecond) : 100000f;

    /// Characters currently revealed (the HUD bar reads this each frame so the two
    /// displays — and a skip — stay perfectly in sync). int.MaxValue = show all.
    public int VisibleCount { get; private set; } = int.MaxValue;

    /// True while the line is still typing out (before it's fully revealed).
    public bool IsRevealing { get; private set; }

    private Coroutine narrationRoutine;
    private AudioSource _blip;
    private bool _skipReveal;
    private bool _voiceActive;   // a real voice clip is playing → suppress blips

    /// Edit-mode/test binding for the auto-hidden bubble panel.
    public void SetPanelRoot(GameObject g) => panelRoot = g;

    /// Voice-over seam: the hash-keyed clip bank + which character this channel is.
    public void BindVoice(VoiceBank bank, VoiceSpeaker who) { voiceBank = bank; speaker = who; }

    /// The voice clip for a subtitle, if the bank has one (null = blip fallback).
    public AudioClip ResolveVoice(string subtitle)
        => voiceBank != null ? voiceBank.Get(speaker, VoiceLineId.For(subtitle)) : null;

    /// Assign this speaker's talking blip (SoundBank key) — played per few chars as the line types.
    public void SetVoiceBlip(string key, float volume = 0.5f) { voiceBlipKey = key; blipVolume = Mathf.Clamp01(volume); }

    private void Start()
    {
        if (panelRoot != null) panelRoot.SetActive(false); // silent until spoken to
        if (playOnStart)
            PlayTutorialNarration();
    }

    public void PlayTutorialNarration()
    {
        if (narrationRoutine != null)
            StopCoroutine(narrationRoutine);

        narrationRoutine = StartCoroutine(PlayLinesRoutine());
    }

    /// Reactive single-line narration (interrupts the current line). Used by
    /// PharmeeBrain for per-step instructions, warnings, and celebrations.
    public void Say(string subtitle, float seconds = 3f, AudioClip clip = null)
    {
        if (!isActiveAndEnabled) return; // edit-mode / disabled: no coroutine
        if (narrationRoutine != null)
            StopCoroutine(narrationRoutine);
        narrationRoutine = StartCoroutine(SayRoutine(subtitle, seconds, clip));
    }

    /// Coroutine-free line start: shows text + bubble + skip, raises LineStarted.
    /// Public so it is edit-mode testable and callable by cutscene staging.
    public void BeginLine(string subtitle, float seconds)
    {
        if (subtitleText != null) { subtitleText.text = subtitle; subtitleText.maxVisibleCharacters = int.MaxValue; }
        if (panelRoot != null) panelRoot.SetActive(true);
        if (skipButton != null) skipButton.SetActive(true);
        IsSpeaking = true;
        VisibleCount = int.MaxValue;      // instant path shows the whole line
        LineStarted?.Invoke(subtitle, seconds);
    }

    /// Coroutine-free line end: clears text, hides bubble + skip, raises LineEnded.
    public void EndLine()
    {
        if (subtitleText != null) subtitleText.text = string.Empty;
        if (panelRoot != null) panelRoot.SetActive(false);
        if (skipButton != null) skipButton.SetActive(false);
        IsRevealing = false; VisibleCount = int.MaxValue;
        if (!IsSpeaking) return; // idempotent — visuals reset above either way
        IsSpeaking = false;
        LineEnded?.Invoke();
    }

    private IEnumerator SayRoutine(string subtitle, float seconds, AudioClip clip)
    {
        if (clip == null) clip = ResolveVoice(subtitle);   // voice-over by text hash
        float waitSeconds = Mathf.Max(0.1f, seconds);
        _voiceActive = false;
        if (narratorAudioSource != null && clip != null)
        {
            narratorAudioSource.clip = clip;
            narratorAudioSource.Play();
            waitSeconds = Mathf.Max(waitSeconds, clip.length + 0.2f);
            _voiceActive = true;                            // real speech → no robot blips
        }
        yield return RevealAndHold(subtitle, waitSeconds);
        _voiceActive = false;
    }

    /// Type the line out character-by-character (with per-few-chars talking blips),
    /// then hold the finished line for the remaining dwell time, then end it. The HUD
    /// dialogue bar mirrors the reveal via LineStarted + the shared TypeCps().
    private IEnumerator RevealAndHold(string subtitle, float waitSeconds)
    {
        if (subtitleText != null)
        {
            subtitleText.text = subtitle;
            subtitleText.maxVisibleCharacters = typewriter ? 0 : int.MaxValue;
            subtitleText.ForceMeshUpdate();
        }
        if (panelRoot != null) panelRoot.SetActive(true);
        if (skipButton != null) skipButton.SetActive(true);
        IsSpeaking = true;
        VisibleCount = typewriter ? 0 : int.MaxValue;
        LineStarted?.Invoke(subtitle, waitSeconds);

        int total = subtitleText != null ? subtitleText.textInfo.characterCount
                                          : (subtitle != null ? subtitle.Length : 0);
        float revealTime = 0f;
        if (typewriter && total > 0)
        {
            IsRevealing = true; _skipReveal = false;
            float cps = TypeCps();
            float perChar = 1f / cps;
            int sinceBlip = 0, shown = 0;
            while (shown < total && IsSpeaking && !_skipReveal)   // skip fills instantly
            {
                shown++;
                if (subtitleText != null) subtitleText.maxVisibleCharacters = shown;
                VisibleCount = shown;
                if (!char.IsWhiteSpace(CharAt(shown - 1)) && ++sinceBlip >= Mathf.Max(1, blipEveryChars))
                { sinceBlip = 0; PlayBlip(); }
                yield return new WaitForSeconds(perChar);
            }
            if (subtitleText != null) subtitleText.maxVisibleCharacters = total;
            VisibleCount = total;
            IsRevealing = false; _skipReveal = false;
            revealTime = total / cps;
        }

        yield return new WaitForSeconds(Mathf.Max(0.6f, waitSeconds - revealTime));
        EndLine();
    }

    private char CharAt(int i)
    {
        if (subtitleText != null && subtitleText.textInfo != null
            && i >= 0 && i < subtitleText.textInfo.characterCount)
            return subtitleText.textInfo.characterInfo[i].character;
        return 'x';
    }

    private void PlayBlip()
    {
        if (_voiceActive) return;   // a spoken clip owns this line
        if (string.IsNullOrEmpty(voiceBlipKey) || AudioService.Instance == null) return;
        var e = AudioService.Instance.EntryOf(voiceBlipKey);
        if (e == null || e.clip == null) return;
        if (_blip == null)
        {
            var go = new GameObject("BlipSource");
            go.transform.SetParent(transform, false);
            _blip = go.AddComponent<AudioSource>();
            _blip.playOnAwake = false; _blip.spatialBlend = 0f;   // 2D — reads with the HUD line
        }
        _blip.pitch = AudioService.JitteredPitch(0.12f, UnityEngine.Random.value);
        _blip.PlayOneShot(e.clip, Mathf.Clamp01(e.volume) * blipVolume);
    }

    /// Skip button / fast-forward: the FIRST press completes the current typewriter
    /// reveal instantly (fills the line); a SECOND press (line already fully shown)
    /// ends/advances it. So fast readers aren't forced to wait, but one tap never
    /// skips text they haven't seen.
    public void SkipNarration()
    {
        if (IsRevealing) { _skipReveal = true; return; }   // first tap → fill the line

        if (narrationRoutine != null)
            StopCoroutine(narrationRoutine);

        if (narratorAudioSource != null)
            narratorAudioSource.Stop();

        EndLine();
        onNarrationFinished?.Invoke();
    }

    private IEnumerator PlayLinesRoutine()
    {
        for (int i = 0; i < tutorialLines.Count; i++)
        {
            NarrationLine line = tutorialLines[i];
            if (line == null)
                continue;

            float waitSeconds = Mathf.Max(0.1f, line.fallbackSeconds);
            var clip = line.voiceClip != null ? line.voiceClip : ResolveVoice(line.subtitle);
            _voiceActive = false;
            if (narratorAudioSource != null && clip != null)
            {
                narratorAudioSource.clip = clip;
                narratorAudioSource.Play();
                waitSeconds = Mathf.Max(waitSeconds, clip.length + 0.2f);
                _voiceActive = true;
            }

            yield return RevealAndHold(line.subtitle, waitSeconds);
            _voiceActive = false;
        }

        onNarrationFinished?.Invoke();
    }
}
