using System.Collections.Generic;
using UnityEngine;

/// Bridges a vessel's LiquidPhysics chemistry events to the experiment logic in a
/// context-aware way: adding a reagent completes the task that expects it (the
/// TaskGraph's prerequisite check enforces order), while a reagent no step expects
/// is a genuine wrong-reagent mistake. Steps may require a MINIMUM poured amount
/// (requiredMl) — deliveries accumulate until the threshold is met, so a one-frame
/// splash no longer completes a step (client depletion mechanic, 2026-07-09).
public class LiquidTaskBinding : MonoBehaviour
{
    [System.Serializable]
    public class ReagentStep
    {
        public ChemicalData reagent;
        public string taskId;
        [Tooltip("Minimum ml poured in before the step completes. 0 = any amount (legacy).")]
        public float requiredMl;
    }

    [SerializeField] private LiquidPhysics vessel;
    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private List<ReagentStep> expectedReagents = new List<ReagentStep>();
    [SerializeField] private FumeHoodZone fumeHood;   // toxic reagents must be handled here

    private readonly Dictionary<string, float> _accumulated = new Dictionary<string, float>();

    public IReadOnlyList<ReagentStep> ExpectedSteps => expectedReagents;

    private void OnEnable()
    {
        if (vessel != null)
        {
            vessel.LiquidAdded += OnLiquidAdded;
            vessel.WrongReagentMixed += OnWrongReagentMixed;
        }
    }

    private void OnDisable()
    {
        if (vessel != null)
        {
            vessel.LiquidAdded -= OnLiquidAdded;
            vessel.WrongReagentMixed -= OnWrongReagentMixed;
        }
    }

    private void OnLiquidAdded(ChemicalData chem, float amount) => HandleReagent(chem, amount);

    private void OnWrongReagentMixed(ChemicalData current, ChemicalData incoming)
    {
        // Already handled by HandleReagent via LiquidAdded; nothing extra needed here.
    }

    /// Legacy single-arg path (self-tests, scripted deliveries): treated as a FULL
    /// delivery — the step completes regardless of its requiredMl threshold.
    public void HandleReagent(ChemicalData chem) => Handle(chem, 0f, true);

    /// Amount-aware handling: pours accumulate toward the step's requiredMl.
    public void HandleReagent(ChemicalData chem, float amountMl) => Handle(chem, amountMl, false);

    private void Handle(ChemicalData chem, float amountMl, bool fullDelivery)
    {
        if (runner == null || chem == null) return;

        // Fume-hood safety: a toxic/volatile reagent handled outside the hood is a violation.
        if (chem.requiresFumeHood && (fumeHood == null || !fumeHood.IsOccupied))
            runner.RecordMistake(LabErrorType.FumeHoodViolation, chem.chemicalName + " must be handled in the fume hood");

        var step = StepForReagent(chem);
        if (step == null)
        {
            // No step in this experiment expects this reagent → wrong reagent.
            runner.RecordMistake(LabErrorType.WrongReagent, "Unexpected reagent: " + chem.chemicalName);
            return;
        }

        // Already done? Ignore extra pours of the same reagent (no double-completes).
        if (runner.Graph != null && runner.Graph.IsComplete(step.taskId)) return;

        if (!fullDelivery && step.requiredMl > 0f)
        {
            _accumulated.TryGetValue(step.taskId, out float have);
            have += Mathf.Max(0f, amountMl);
            _accumulated[step.taskId] = have;
            if (have < step.requiredMl) return;    // keep pouring — not enough yet
        }

        // Enough reagent delivered. CompleteTask enforces order and will
        // auto-record a WrongStep mistake if prerequisites aren't met yet.
        runner.CompleteTask(step.taskId);
    }

    /// Delivered-so-far toward a step (the supply monitor reads this).
    public float AccumulatedFor(string taskId)
        => _accumulated.TryGetValue(taskId, out float v) ? v : 0f;

    public ReagentStep StepForReagent(ChemicalData chem)
    {
        for (int i = 0; i < expectedReagents.Count; i++)
            if (expectedReagents[i] != null && expectedReagents[i].reagent == chem)
                return expectedReagents[i];
        return null;
    }

    public string TaskForReagent(ChemicalData chem)
    {
        var s = StepForReagent(chem);
        return s != null ? s.taskId : null;
    }

    // Runtime helpers for authoring/binding.
    public void AddExpected(ChemicalData reagent, string taskId, float requiredMl = 0f)
        => expectedReagents.Add(new ReagentStep { reagent = reagent, taskId = taskId, requiredMl = requiredMl });

    public void SetVesselAndRunner(LiquidPhysics v, ExperimentRunner r) { vessel = v; runner = r; }
}
