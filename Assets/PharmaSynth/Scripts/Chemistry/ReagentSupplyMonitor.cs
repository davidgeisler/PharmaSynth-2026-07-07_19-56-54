using System;
using System.Collections.Generic;
using UnityEngine;

/// Pure shortfall analysis: which incomplete pour-steps can no longer be finished
/// because the remaining supply of their reagent (summed across all bottles) is
/// less than what the step still needs. Edit-mode testable.
public static class ReagentSupplyMath
{
    public struct Need
    {
        public string taskId;
        public string chemicalName;
        public float requiredMl;
        public float deliveredMl;
    }

    public static List<string> FindShortfalls(IEnumerable<Need> needs,
        Func<string, bool> isTaskComplete,
        IReadOnlyDictionary<string, float> availableMlByChemical)
    {
        var shortfalls = new List<string>();
        if (needs == null) return shortfalls;
        foreach (var n in needs)
        {
            if (n.requiredMl <= 0f) continue;                          // instant steps never starve
            if (isTaskComplete != null && isTaskComplete(n.taskId)) continue;
            float still = Mathf.Max(0f, n.requiredMl - n.deliveredMl);
            if (still <= 0f) continue;
            float avail = 0f;
            if (availableMlByChemical != null && n.chemicalName != null)
                availableMlByChemical.TryGetValue(n.chemicalName, out avail);
            if (avail + 0.5f < still) shortfalls.Add(n.taskId);        // small epsilon for pour jitter
        }
        return shortfalls;
    }
}

/// Watches the live stage while an experiment runs: if a required pour-step can no
/// longer be satisfied by the reagent left in the scene's bottles, it raises
/// SupplyExhausted (once per attempt) so Pharmee can offer the restart.
public class ReagentSupplyMonitor : MonoBehaviour
{
    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private float pollSeconds = 2f;

    /// taskIds that starved. Latched until the next attempt starts.
    public event Action<List<string>> SupplyExhausted;

    private float _next;
    private bool _latched;

    public void SetRunner(ExperimentRunner r)
    {
        if (runner != null) runner.ExperimentStarted -= OnStarted;
        runner = r;
        if (runner != null && isActiveAndEnabled) runner.ExperimentStarted += OnStarted;
    }

    private void OnEnable() { if (runner != null) runner.ExperimentStarted += OnStarted; }
    private void OnDisable() { if (runner != null) runner.ExperimentStarted -= OnStarted; }
    private void OnStarted(ExperimentModuleDefinition m) => _latched = false;

    private void Update()
    {
        if (_latched || runner == null || !runner.IsRunning) return;
        if (Time.time < _next) return;
        _next = Time.time + Mathf.Max(0.5f, pollSeconds);
        var shortfalls = EvaluateNow();
        if (shortfalls.Count > 0)
        {
            _latched = true;
            SupplyExhausted?.Invoke(shortfalls);
        }
    }

    /// One poll pass (public for headless tests): gathers needs from every live
    /// LiquidTaskBinding and supply from every bottle holding the right chemical.
    public List<string> EvaluateNow()
    {
        var needs = new List<ReagentSupplyMath.Need>();
        var binds = UnityEngine.Object.FindObjectsByType<LiquidTaskBinding>(FindObjectsSortMode.None);
        foreach (var b in binds)
            foreach (var s in b.ExpectedSteps)
            {
                if (s == null || s.reagent == null || s.requiredMl <= 0f) continue;
                needs.Add(new ReagentSupplyMath.Need
                {
                    taskId = s.taskId,
                    chemicalName = s.reagent.chemicalName,
                    requiredMl = s.requiredMl,
                    deliveredMl = b.AccumulatedFor(s.taskId),
                });
            }
        if (needs.Count == 0) return new List<string>();

        // Available supply: SOURCE bottles only (vessels with a task binding are
        // reaction targets, not supplies).
        var avail = new Dictionary<string, float>();
        var bottles = UnityEngine.Object.FindObjectsByType<LiquidPhysics>(FindObjectsSortMode.None);
        foreach (var lp in bottles)
        {
            if (lp.currentChemical == null || lp.currentLiquidVolume <= 0f) continue;
            if (lp.GetComponent<LiquidTaskBinding>() != null) continue;
            string key = lp.currentChemical.chemicalName;
            avail.TryGetValue(key, out float v);
            avail[key] = v + lp.currentLiquidVolume;
        }

        bool Complete(string taskId) => runner != null && runner.Graph != null && runner.Graph.IsComplete(taskId);
        return ReagentSupplyMath.FindShortfalls(needs, Complete, avail);
    }
}
