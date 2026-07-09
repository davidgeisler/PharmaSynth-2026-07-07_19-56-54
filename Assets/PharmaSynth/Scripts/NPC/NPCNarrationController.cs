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

    [Header("Events")]
    public UnityEvent onNarrationFinished;

    /// Per-line hooks: the overhead bubble AND the HUD dialogue bar both mirror these.
    public event Action<string, float> LineStarted;
    public event Action LineEnded;

    /// True while a line is on screen (between BeginLine and EndLine).
    public bool IsSpeaking { get; private set; }

    private Coroutine narrationRoutine;

    /// Edit-mode/test binding for the auto-hidden bubble panel.
    public void SetPanelRoot(GameObject g) => panelRoot = g;

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
        if (subtitleText != null) subtitleText.text = subtitle;
        if (panelRoot != null) panelRoot.SetActive(true);
        if (skipButton != null) skipButton.SetActive(true);
        IsSpeaking = true;
        LineStarted?.Invoke(subtitle, seconds);
    }

    /// Coroutine-free line end: clears text, hides bubble + skip, raises LineEnded.
    public void EndLine()
    {
        if (subtitleText != null) subtitleText.text = string.Empty;
        if (panelRoot != null) panelRoot.SetActive(false);
        if (skipButton != null) skipButton.SetActive(false);
        if (!IsSpeaking) return; // idempotent — visuals reset above either way
        IsSpeaking = false;
        LineEnded?.Invoke();
    }

    private IEnumerator SayRoutine(string subtitle, float seconds, AudioClip clip)
    {
        float waitSeconds = Mathf.Max(0.1f, seconds);
        if (narratorAudioSource != null && clip != null)
        {
            narratorAudioSource.clip = clip;
            narratorAudioSource.Play();
            waitSeconds = clip.length;
        }

        BeginLine(subtitle, waitSeconds);
        yield return new WaitForSeconds(waitSeconds);
        EndLine();
    }

    public void SkipNarration()
    {
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

            if (narratorAudioSource != null && line.voiceClip != null)
            {
                narratorAudioSource.clip = line.voiceClip;
                narratorAudioSource.Play();
                waitSeconds = line.voiceClip.length;
            }

            BeginLine(line.subtitle, waitSeconds);
            yield return new WaitForSeconds(waitSeconds);
        }

        EndLine();
        onNarrationFinished?.Invoke();
    }
}
