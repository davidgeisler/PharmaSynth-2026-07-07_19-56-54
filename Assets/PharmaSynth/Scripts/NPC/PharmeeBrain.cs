using System;
using UnityEngine;

/// Pharmee's expressions, driven onto the robot's screen-face (material/animator).
public enum PharmeeFaceExpression { Neutral, Happy, Warning }

/// Pharmee's behavioural states.
public enum PharmeeState { Idle, Greeting, Instructing, Warning, Celebrating, Encouraging }

/// Optional visual face — implemented by the robot's face material/animator layer.
public interface IPharmeeFace { void SetExpression(PharmeeFaceExpression e); }

/// Robot-guide brain: reacts to ExperimentRunner events with subtitle dialogue and
/// a face expression (user requirement: "NPC robot must have dialogues"). Greets on
/// start, instructs the current step, warns on mistakes, celebrates or encourages at
/// the end. Dialogue lives in serialized data (client-reviewable); step instructions
/// come from each task's hint.
public class PharmeeBrain : MonoBehaviour
{
    [Serializable]
    public class DialogueSet
    {
        [TextArea] public string greeting = "Welcome to the lab! I'm Pharmee. Follow the steps on your tablet and I'll guide you.";
        [TextArea] public string celebrate = "Outstanding! You've completed the experiment. Great synthesis!";
        [TextArea] public string encourage = "Good effort — review the steps and give it another go. You've got this!";
        [TextArea] public string wrongReagent = "Hmm, that isn't the reagent this step needs. Check your tablet.";
        [TextArea] public string wrongStep = "Let's not skip ahead — complete the current step first.";
        [TextArea] public string overheat = "Careful — it's overheating! Ease off the heat.";
        [TextArea] public string safety = "Safety first! Mind the hazard and keep your PPE on.";
    }

    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private NPCNarrationController narration;
    [SerializeField] private MonoBehaviour faceBehaviour; // optional IPharmeeFace
    [SerializeField] private DialogueSet lines = new DialogueSet();
    [SerializeField] private float lineSeconds = 3.5f;
    [Tooltip("When a CutsceneDirector handles intro/outro, let it greet — the brain only instructs/warns.")]
    [SerializeField] private bool deferIntroToDirector = false;

    private IPharmeeFace _face;
    private bool _subscribed;
    private bool _assessment;   // assessment mode → no procedural hints (safety warnings stay)

    /// True while the current experiment is in assessment mode (Pharmee stays quiet).
    public bool AssessmentMode => _assessment;

    public PharmeeState State { get; private set; } = PharmeeState.Idle;
    public string LastLine { get; private set; } = "";
    public PharmeeFaceExpression LastExpression { get; private set; } = PharmeeFaceExpression.Neutral;

    /// Assign (or swap) the runner at runtime; subscribes immediately.
    public void SetRunner(ExperimentRunner r)
    {
        Unsubscribe();
        runner = r;
        _face = faceBehaviour as IPharmeeFace;
        Subscribe();
    }

    private void OnEnable()
    {
        _face = faceBehaviour as IPharmeeFace;
        Subscribe();
    }

    private void OnDisable() => Unsubscribe();

    private void Subscribe()
    {
        if (_subscribed || runner == null) return;
        runner.ExperimentStarted += OnStarted;
        runner.TaskCompleted += OnTaskCompleted;
        runner.MistakeRecorded += OnMistake;
        runner.ExperimentFinished += OnFinished;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || runner == null) return;
        runner.ExperimentStarted -= OnStarted;
        runner.TaskCompleted -= OnTaskCompleted;
        runner.MistakeRecorded -= OnMistake;
        runner.ExperimentFinished -= OnFinished;
        _subscribed = false;
    }

    private void OnStarted(ExperimentModuleDefinition m)
    {
        // Assessment mode (Dr. Jimenez observing): Pharmee gives no procedural hints.
        _assessment = m != null && m.assessmentMode;
        if (_assessment) return;
        // With a cutscene director present, it plays the intro then calls
        // InstructCurrent() on finish — the brain stays quiet here to avoid overlap.
        if (deferIntroToDirector) return;
        Speak(PharmeeState.Greeting, PharmeeFaceExpression.Happy, lines.greeting);
        InstructCurrent();
    }

    private void OnTaskCompleted(ExperimentTask t) => InstructCurrent();

    private void OnMistake(LabErrorType type, string message)
        => Speak(PharmeeState.Warning, PharmeeFaceExpression.Warning, WarnLineFor(type));

    private void OnFinished(ExperimentResult r)
    {
        // The end cutscene carries the celebrate/encourage line when a director is present.
        if (deferIntroToDirector) return;
        if (r.passed) Speak(PharmeeState.Celebrating, PharmeeFaceExpression.Happy, lines.celebrate);
        else Speak(PharmeeState.Encouraging, PharmeeFaceExpression.Neutral, lines.encourage);
    }

    /// Instruct the current available step (nearest to done). No-op when finished.
    public void InstructCurrent()
    {
        if (_assessment) return;                       // no procedural hints in assessment mode
        if (runner == null || runner.Graph == null) return;
        foreach (var t in runner.Graph.AvailableTasks())
        {
            Speak(PharmeeState.Instructing, PharmeeFaceExpression.Neutral, InstructionFor(t));
            return;
        }
        // nothing available (all complete) — stay in current state
    }

    private void Speak(PharmeeState state, PharmeeFaceExpression face, string line)
    {
        State = state;
        LastLine = line;
        LastExpression = face;
        _face?.SetExpression(face);
        if (narration != null) narration.Say(line, lineSeconds);
        // Pharmee's robotic "voice" — a beep per mood (no-op if no AudioService/clip).
        if (AudioService.Instance != null) AudioService.Instance.Play(BeepKey(state));
    }

    private static string BeepKey(PharmeeState s)
    {
        switch (s)
        {
            case PharmeeState.Warning: return "pharmee-warn";
            case PharmeeState.Celebrating: return "pharmee-celebrate";
            case PharmeeState.Greeting: return "pharmee-greet";
            default: return "pharmee-instruct";
        }
    }

    private string WarnLineFor(LabErrorType type)
    {
        switch (type)
        {
            case LabErrorType.WrongReagent: return lines.wrongReagent;
            case LabErrorType.WrongStep: return lines.wrongStep;
            case LabErrorType.Overheat: return lines.overheat;
            default: return lines.safety; // fire, PPE, fume hood, contact, hazardous, dropped glass
        }
    }

    /// The spoken instruction for a step: its hint, or a fallback from the label.
    public static string InstructionFor(ExperimentTask task)
    {
        if (task == null) return "";
        return !string.IsNullOrWhiteSpace(task.hint) ? task.hint : "Next: " + task.label;
    }
}
