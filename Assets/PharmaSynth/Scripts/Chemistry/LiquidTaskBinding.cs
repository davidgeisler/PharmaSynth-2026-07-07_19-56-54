using System.Collections.Generic;
using UnityEngine;

/// Bridges a vessel's LiquidPhysics chemistry events to the experiment logic in a
/// context-aware way: adding a reagent completes the task that expects it (the
/// TaskGraph's prerequisite check enforces order), while a reagent no step expects
/// is a genuine wrong-reagent mistake. This replaces LiquidPhysics's old naive,
/// context-free "no reaction rule == wrong" detection (audit critical #3).
public class LiquidTaskBinding : MonoBehaviour
{
    [System.Serializable]
    public class ReagentStep
    {
        public ChemicalData reagent;
        public string taskId;
    }

    [SerializeField] private LiquidPhysics vessel;
    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private List<ReagentStep> expectedReagents = new List<ReagentStep>();

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

    private void OnLiquidAdded(ChemicalData chem, float amount) => HandleReagent(chem);

    private void OnWrongReagentMixed(ChemicalData current, ChemicalData incoming)
    {
        // Already handled by HandleReagent via LiquidAdded; nothing extra needed here.
    }

    /// Context-aware handling of a reagent addition. Public so it is directly testable.
    public void HandleReagent(ChemicalData chem)
    {
        if (runner == null || chem == null) return;

        string taskId = TaskForReagent(chem);
        if (string.IsNullOrEmpty(taskId))
        {
            // No step in this experiment expects this reagent → wrong reagent.
            runner.RecordMistake(LabErrorType.WrongReagent, "Unexpected reagent: " + chem.chemicalName);
            return;
        }

        // Correct reagent for a known step. CompleteTask enforces order and will
        // auto-record a WrongStep mistake if prerequisites aren't met yet.
        runner.CompleteTask(taskId);
    }

    public string TaskForReagent(ChemicalData chem)
    {
        for (int i = 0; i < expectedReagents.Count; i++)
            if (expectedReagents[i] != null && expectedReagents[i].reagent == chem)
                return expectedReagents[i].taskId;
        return null;
    }

    // Runtime helper for authoring/binding.
    public void AddExpected(ChemicalData reagent, string taskId)
        => expectedReagents.Add(new ReagentStep { reagent = reagent, taskId = taskId });

    public void SetVesselAndRunner(LiquidPhysics v, ExperimentRunner r) { vessel = v; runner = r; }
}
