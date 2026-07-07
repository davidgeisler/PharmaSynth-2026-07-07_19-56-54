using System;
using UnityEngine;
using UnityEngine.Events;

/// Outcome of an experiment attempt, consumed by the grade screen and progression gate.
public struct ExperimentResult
{
    public GradeBreakdown grade;      // per-criterion contributions + Total (0..100)
    public float overallMastery;      // BKT mean P(learned) across tracked skills
    public bool gradePassed;          // Total >= threshold%
    public bool masteryPassed;        // mastery >= threshold
    public bool passed;               // gate = gradePassed AND masteryPassed
    public int mistakeCount;
    public float elapsedSeconds;
}

/// Runtime orchestrator for a single experiment attempt. Owns the TaskGraph,
/// BKT MasteryModel, MistakeLog and grader built from an ExperimentModuleDefinition,
/// and raises the events the HUD / tablet / wrist-watch / grade screen subscribe to.
///
/// All scoring/mastery logic lives in the (unit-tested) pure classes; this is thin
/// glue plus per-frame time + auto-check. Drives the new v2 data model; the legacy
/// ExperimentFlowManager remains for the one inherited Ethyl module until migrated.
public class ExperimentRunner : MonoBehaviour
{
    [SerializeField] private ExperimentModuleDefinition module;
    [SerializeField] private GradingConfig gradingConfig = new GradingConfig();
    [SerializeField] private bool startOnAwake = false;

    [Header("Inspector hooks")]
    public UnityEvent onExperimentStarted;
    public UnityEvent onExperimentFinished;

    // Code-facing events (HUD/tablet/watch/NPC controllers subscribe here).
    public event Action<ExperimentModuleDefinition> ExperimentStarted;
    public event Action<ExperimentTask> TaskCompleted;
    public event Action<TaskPhase> PhaseCompleted;
    public event Action<float> ProgressChanged;                 // 0..1
    public event Action<LabErrorType, string> MistakeRecorded;
    public event Action<ExperimentResult> ExperimentFinished;

    private TaskGraph _graph;
    private MasteryModel _mastery;
    private MistakeLog _mistakes;
    private ExperimentGrader _grader;
    private float _elapsed;
    private bool _running;
    private bool _finished;

    public bool IsRunning => _running && !_finished;
    public float ElapsedSeconds => _elapsed;
    public float Progress01 => _graph != null ? _graph.Progress01 : 0f;
    public int MistakeCount => _mistakes != null ? _mistakes.Count : 0;
    public float OverallMastery => _mastery != null ? _mastery.OverallMastery() : 0f;
    public TaskGraph Graph => _graph;
    public MistakeLog Mistakes => _mistakes;
    public ExperimentModuleDefinition Module => module;

    public void SetModule(ExperimentModuleDefinition m) => module = m;

    private void Awake()
    {
        if (startOnAwake) StartExperiment();
    }

    /// Build a fresh attempt from the module data and begin.
    public void StartExperiment()
    {
        if (module == null)
        {
            Debug.LogError("[ExperimentRunner] No module assigned.");
            return;
        }

        _graph = module.BuildTaskGraph();
        _mastery = module.BuildMasteryModel();
        _mistakes = new MistakeLog();
        _grader = new ExperimentGrader(module.BuildScoreCalculator(), gradingConfig);
        _elapsed = 0f;
        _running = true;
        _finished = false;

        _graph.TaskCompleted += OnGraphTaskCompleted;
        _graph.PhaseCompleted += OnGraphPhaseCompleted;
        _mistakes.MistakeRecorded += OnMistakeRecorded;

        ExperimentStarted?.Invoke(module);
        onExperimentStarted?.Invoke();
        ProgressChanged?.Invoke(0f);
    }

    private void OnGraphTaskCompleted(ExperimentTask t)
    {
        if (_mastery.IsTracked(t.skill))
            _mastery.Observe(t.skill, true);      // a correct step is positive evidence
        TaskCompleted?.Invoke(t);
        ProgressChanged?.Invoke(_graph.Progress01);
    }

    private void OnGraphPhaseCompleted(TaskPhase p) => PhaseCompleted?.Invoke(p);
    private void OnMistakeRecorded(LabErrorType t, string m) => MistakeRecorded?.Invoke(t, m);

    /// Complete a task (from a trigger/UI/auto-check). An out-of-order attempt is
    /// auto-recorded as a WrongStep mistake — the error matrix wiring.
    public TaskCompletionResult CompleteTask(string taskId)
    {
        if (!IsRunning) return TaskCompletionResult.UnknownTask;
        TaskCompletionResult r = _graph.TryComplete(taskId);
        if (r == TaskCompletionResult.BlockedByPrerequisite)
            RecordMistake(LabErrorType.WrongStep, "Attempted '" + taskId + "' out of order");
        return r;
    }

    /// Report an error from the safety/interaction systems. Lowers the associated
    /// skill's mastery and feeds the grade penalty.
    public void RecordMistake(LabErrorType type, string message)
    {
        if (_mistakes == null) return;
        _mistakes.Record(type, message);
        LabSkill skill = MistakeLog.SkillFor(type);
        if (_mastery != null && _mastery.IsTracked(skill))
            _mastery.Observe(skill, false);
    }

    /// Bind a world-state predicate so the task auto-completes when satisfied.
    public void RegisterCondition(string taskId, Func<bool> condition) => _graph?.RegisterCondition(taskId, condition);

    /// Advance the experiment clock (called by Update; exposed for deterministic tests).
    public void AdvanceTime(float dt)
    {
        if (IsRunning && dt > 0f) _elapsed += dt;
    }

    private void Update()
    {
        if (!IsRunning) return;
        AdvanceTime(Time.deltaTime);
        _graph.Tick();   // evaluate world-state auto-check conditions
    }

    /// Compute the result without ending the attempt (for a live grade preview).
    public ExperimentResult Evaluate(float quizFraction) => BuildResult(quizFraction);

    /// End the attempt and emit the final result to the grade screen + gate.
    public ExperimentResult Finish(float quizFraction = 0f)
    {
        ExperimentResult result = BuildResult(quizFraction);
        if (!_finished)
        {
            _finished = true;
            _running = false;
            ExperimentFinished?.Invoke(result);
            onExperimentFinished?.Invoke();
        }
        return result;
    }

    /// Retry rebuilds a fresh attempt from the module (deterministic reset).
    public void Retry() => StartExperiment();

    private ExperimentResult BuildResult(float quizFraction)
    {
        float threshold = module != null ? module.masteryThreshold : 0.9f;
        float par = module != null ? module.parTimeSeconds : 600f;

        GradeBreakdown grade = _grader.Grade(_graph, _mistakes, _elapsed, par, quizFraction);
        float mastery = _mastery.OverallMastery();
        bool gradePass = grade.Total >= threshold * 100f;
        bool masteryPass = mastery >= threshold;

        return new ExperimentResult
        {
            grade = grade,
            overallMastery = mastery,
            gradePassed = gradePass,
            masteryPassed = masteryPass,
            passed = gradePass && masteryPass,
            mistakeCount = _mistakes.Count,
            elapsedSeconds = _elapsed
        };
    }
}
