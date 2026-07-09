using System;
using UnityEngine;

/// Persists every finished attempt into the ProgressionService save file — the
/// missing link between ExperimentRunner.ExperimentFinished and the unlock gates
/// (without this, passes were never saved and nothing ever unlocked at runtime).
/// Thin MonoBehaviour over the already-tested ProgressionService.RecordResult.
public class ResultRecorder : MonoBehaviour
{
    [SerializeField] private ExperimentRunner runner;

    /// Fired after the record is persisted (gatekeeper listens for unlock announce).
    public event Action<ModuleRecord> Recorded;

    public ModuleRecord LastRecord { get; private set; }

    /// Optional save-path override for tests (defaults to the real save file).
    public string SavePathOverride { get; set; }

    /// Edit-mode/test binding (Awake/OnEnable ordering is unreliable on AddComponent).
    public void SetRunner(ExperimentRunner r)
    {
        if (runner != null) runner.ExperimentFinished -= OnFinished;
        runner = r;
        if (runner != null && isActiveAndEnabled) runner.ExperimentFinished += OnFinished;
    }

    private void OnEnable()
    {
        if (runner != null) runner.ExperimentFinished += OnFinished;
    }

    private void OnDisable()
    {
        if (runner != null) runner.ExperimentFinished -= OnFinished;
    }

    private void OnFinished(ExperimentResult result)
    {
        var module = runner != null ? runner.Module : null;
        if (module == null || string.IsNullOrEmpty(module.moduleId)) return;
        var svc = new ProgressionService(SavePathOverride);
        svc.Load();
        LastRecord = Record(svc, module.moduleId, result);
        Recorded?.Invoke(LastRecord);
    }

    /// Pure seam: fold one attempt into the service (loads/saves are the caller's).
    public static ModuleRecord Record(ProgressionService svc, string moduleId, ExperimentResult result)
        => svc.RecordResult(moduleId, result);
}
