using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// Plays data-driven cutscenes at the right narrative moments by subscribing to
/// the ExperimentRunner: Intro when the experiment starts, ReagentPrep when that
/// phase completes, and Success/Failure when it finishes. Beats are delivered as
/// Pharmee subtitles + face expressions (VR-safe; no camera animation).
///
/// The end-cutscene ALWAYS plays (success OR failure variant) — a user requirement.
public class CutsceneDirector : MonoBehaviour
{
    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private NPCNarrationController narration;
    [SerializeField] private MonoBehaviour faceBehaviour; // optional IPharmeeFace

    [Header("Cutscenes")]
    [SerializeField] private CutsceneData intro;
    [SerializeField] private CutsceneData reagentPrep;
    [SerializeField] private CutsceneData success;
    [SerializeField] private CutsceneData failure;

    public UnityEvent onCutsceneStarted;
    public UnityEvent onCutsceneFinished;

    private IPharmeeFace _face;
    private Coroutine _routine;
    private bool _subscribed;

    public bool IsPlaying { get; private set; }

    public void SetRunner(ExperimentRunner r)
    {
        Unsubscribe();
        runner = r;
        _face = faceBehaviour as IPharmeeFace;
        Subscribe();
    }

    private void OnEnable() { _face = faceBehaviour as IPharmeeFace; Subscribe(); }
    private void OnDisable() => Unsubscribe();

    private void Subscribe()
    {
        if (_subscribed || runner == null) return;
        runner.ExperimentStarted += OnStarted;
        runner.PhaseCompleted += OnPhaseCompleted;
        runner.ExperimentFinished += OnFinished;
        _subscribed = true;
    }
    private void Unsubscribe()
    {
        if (!_subscribed || runner == null) return;
        runner.ExperimentStarted -= OnStarted;
        runner.PhaseCompleted -= OnPhaseCompleted;
        runner.ExperimentFinished -= OnFinished;
        _subscribed = false;
    }

    private void OnStarted(ExperimentModuleDefinition m) => Play(intro);
    private void OnPhaseCompleted(TaskPhase p) { if (p == TaskPhase.ReagentPrep) Play(reagentPrep); }
    private void OnFinished(ExperimentResult r) => Play(SelectOutro(r));

    /// The end cutscene always resolves to something (success or failure variant).
    public CutsceneData SelectOutro(ExperimentResult r) => r.passed ? success : failure;

    public void Play(CutsceneData data)
    {
        if (data == null || data.beats == null || data.beats.Count == 0) return;
        if (_routine != null) StopCoroutine(_routine);
        if (isActiveAndEnabled) _routine = StartCoroutine(PlayRoutine(data));
    }

    private IEnumerator PlayRoutine(CutsceneData data)
    {
        IsPlaying = true;
        onCutsceneStarted?.Invoke();
        for (int i = 0; i < data.beats.Count; i++)
        {
            var b = data.beats[i];
            if (b == null) continue;
            _face?.SetExpression(b.face);
            if (narration != null) narration.Say(b.subtitle, b.seconds);
            yield return new WaitForSeconds(Mathf.Max(0.2f, b.seconds));
        }
        IsPlaying = false;
        onCutsceneFinished?.Invoke();
        _routine = null;
    }

    public void Skip()
    {
        if (_routine != null) StopCoroutine(_routine);
        if (narration != null) narration.SkipNarration();
        IsPlaying = false;
        _routine = null;
        onCutsceneFinished?.Invoke();
    }
}
