using System.Collections.Generic;

/// Read-only view over a ProgressionService that answers the game-flow questions the
/// menu / period hub / experiment-select screens ask: what's unlocked, what's next,
/// which period doors are open, and overall completion. Uses ExperimentCatalog as the
/// roster + prerequisite source of truth. Plain C# so it is unit-testable.
public class ProgressionFlow
{
    private readonly ProgressionService _service;

    public ProgressionFlow(ProgressionService service) { _service = service; }

    /// An experiment is unlocked if its catalog prerequisite has been passed
    /// (or it has none). Unknown ids are treated as locked.
    public bool IsUnlocked(string moduleId)
    {
        var entry = ExperimentCatalog.Get(moduleId);
        if (entry == null) return false;
        return _service.IsUnlocked(moduleId, entry.prerequisiteModuleId);
    }

    public bool IsPassed(string moduleId) => _service.IsPassed(moduleId);

    /// The first experiment in roster order that is unlocked but not yet passed —
    /// i.e. what the player should tackle next. Null when everything is passed.
    public CatalogEntry NextExperiment()
    {
        foreach (var e in ExperimentCatalog.Entries)
            if (!_service.IsPassed(e.moduleId) && IsUnlocked(e.moduleId))
                return e;
        return null;
    }

    /// Every experiment in the period has been passed.
    public bool IsPeriodComplete(ExperimentPeriod period)
    {
        bool any = false;
        foreach (var e in ExperimentCatalog.InPeriod(period))
        {
            any = true;
            if (!_service.IsPassed(e.moduleId)) return false;
        }
        return any;   // an empty period is not "complete"
    }

    /// A period door is open once every earlier period is complete. The first
    /// period (Tutorial) is always open.
    public bool IsPeriodUnlocked(ExperimentPeriod period)
    {
        foreach (ExperimentPeriod p in System.Enum.GetValues(typeof(ExperimentPeriod)))
        {
            if (p >= period) break;
            if (!IsPeriodComplete(p)) return false;
        }
        return true;
    }

    public int PassedCount()
    {
        int n = 0;
        foreach (var e in ExperimentCatalog.Entries) if (_service.IsPassed(e.moduleId)) n++;
        return n;
    }

    /// 0..1 fraction of the whole roster passed.
    public float OverallCompletion01()
        => ExperimentCatalog.Count == 0 ? 0f : (float)PassedCount() / ExperimentCatalog.Count;

    public bool AllComplete() => PassedCount() == ExperimentCatalog.Count;
}
