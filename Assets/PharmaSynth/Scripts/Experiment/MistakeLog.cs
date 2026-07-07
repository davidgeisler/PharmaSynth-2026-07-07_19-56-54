using System;
using System.Collections.Generic;

/// The full error taxonomy the safety/error matrix reports (plan §3.7).
/// Extends the inherited 4 hardcoded penalties (wrong step/reagent/glass/fire)
/// with the missing overheat, PPE, fume-hood and chemical-contact cases.
public enum LabErrorType
{
    WrongReagent,
    WrongStep,
    DroppedGlassware,
    Overheat,
    FireSafety,
    MissingPPE,
    FumeHoodViolation,
    ChemicalContact,
    HazardousAction
}

/// Records mistakes during an experiment attempt and exposes counts.
/// (Audit gap: the legacy FlowManager fired a MistakeRecorded event but never
/// kept a count — the grade screen needs "Number of Mistakes".)
///
/// Plain C# so it is unit-testable and reusable by both the legacy FlowManager
/// and the new experiment runner.
public class MistakeLog
{
    private readonly struct Entry
    {
        public readonly LabErrorType Type;
        public readonly string Message;
        public Entry(LabErrorType type, string message) { Type = type; Message = message; }
    }

    private readonly List<Entry> _entries = new List<Entry>();

    /// Fired on each recorded mistake (type, human-readable message).
    public event Action<LabErrorType, string> MistakeRecorded;

    public int Count => _entries.Count;

    public void Record(LabErrorType type, string message)
    {
        _entries.Add(new Entry(type, message));
        MistakeRecorded?.Invoke(type, message);
    }

    public int CountOf(LabErrorType type)
    {
        int n = 0;
        for (int i = 0; i < _entries.Count; i++)
            if (_entries[i].Type == type) n++;
        return n;
    }

    public int CountOfAny(params LabErrorType[] types)
    {
        int n = 0;
        for (int i = 0; i < _entries.Count; i++)
            for (int j = 0; j < types.Length; j++)
                if (_entries[i].Type == types[j]) { n++; break; }
        return n;
    }

    public void Clear() => _entries.Clear();

    /// Which rubric criterion a given error deducts from — procedural mistakes hit
    /// Procedure, safety/handling mistakes hit Materials & PPE, broken glass hits
    /// Sanitation (it creates a cleanup hazard).
    public static RubricCategory CategoryFor(LabErrorType type)
    {
        switch (type)
        {
            case LabErrorType.WrongReagent:
            case LabErrorType.WrongStep:
            case LabErrorType.Overheat:
                return RubricCategory.Procedure;
            case LabErrorType.DroppedGlassware:
                return RubricCategory.Sanitation;
            default:
                return RubricCategory.MaterialsAndPPE; // fire, PPE, fume hood, contact, hazardous
        }
    }

    /// Which competency a mistake reflects poorly on, for the BKT observation.
    public static LabSkill SkillFor(LabErrorType type)
    {
        switch (type)
        {
            case LabErrorType.Overheat:
                return LabSkill.Heating;
            case LabErrorType.FireSafety:
            case LabErrorType.MissingPPE:
            case LabErrorType.FumeHoodViolation:
            case LabErrorType.ChemicalContact:
            case LabErrorType.HazardousAction:
                return LabSkill.Safety;
            default:
                return LabSkill.Transfer; // wrong reagent/step, dropped glass = handling
        }
    }
}
