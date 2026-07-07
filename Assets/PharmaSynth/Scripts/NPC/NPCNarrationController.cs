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

    [Header("Sequence")]
    [SerializeField] private List<NarrationLine> tutorialLines = new List<NarrationLine>();
    [SerializeField] private bool playOnStart = true;

    [Header("Events")]
    public UnityEvent onNarrationFinished;

    private Coroutine narrationRoutine;

    private void Start()
    {
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

    private IEnumerator SayRoutine(string subtitle, float seconds, AudioClip clip)
    {
        if (subtitleText != null) subtitleText.text = subtitle;
        if (skipButton != null) skipButton.SetActive(true);

        float waitSeconds = Mathf.Max(0.1f, seconds);
        if (narratorAudioSource != null && clip != null)
        {
            narratorAudioSource.clip = clip;
            narratorAudioSource.Play();
            waitSeconds = clip.length;
        }

        yield return new WaitForSeconds(waitSeconds);

        if (subtitleText != null) subtitleText.text = string.Empty;
        if (skipButton != null) skipButton.SetActive(false);
    }

    public void SkipNarration()
    {
        if (narrationRoutine != null)
            StopCoroutine(narrationRoutine);

        if (narratorAudioSource != null)
            narratorAudioSource.Stop();

        if (subtitleText != null)
            subtitleText.text = string.Empty;

        if (skipButton != null)
            skipButton.SetActive(false);

        onNarrationFinished?.Invoke();
    }

    private IEnumerator PlayLinesRoutine()
    {
        if (skipButton != null)
            skipButton.SetActive(true);

        for (int i = 0; i < tutorialLines.Count; i++)
        {
            NarrationLine line = tutorialLines[i];
            if (line == null)
                continue;

            if (subtitleText != null)
                subtitleText.text = line.subtitle;

            float waitSeconds = Mathf.Max(0.1f, line.fallbackSeconds);

            if (narratorAudioSource != null && line.voiceClip != null)
            {
                narratorAudioSource.clip = line.voiceClip;
                narratorAudioSource.Play();
                waitSeconds = line.voiceClip.length;
            }

            yield return new WaitForSeconds(waitSeconds);
        }

        if (subtitleText != null)
            subtitleText.text = string.Empty;

        if (skipButton != null)
            skipButton.SetActive(false);

        onNarrationFinished?.Invoke();
    }
}
