using System;
using System.Collections.Generic;
using UnityEngine;

/// Tunables for how mistakes erode rubric sub-scores. Client-adjustable.
[Serializable]
public class GradingConfig
{
    [Range(0f, 1f), Tooltip("Sub-score lost per procedural mistake (wrong reagent/step, overheat).")]
    public float procedureMistakeStep = 0.10f;
    [Range(0f, 1f), Tooltip("Sub-score lost per safety mistake (fire, PPE, fume hood, contact).")]
    public float safetyMistakeStep = 0.15f;
    [Range(0f, 1f), Tooltip("Sub-score lost per broken/dropped glassware (sanitation).")]
    public float sanitationMistakeStep = 0.20f;
}

/// Turns an experiment attempt's run-state (task completion + mistakes + time +
/// quiz) into the per-criterion sub-scores, then the final GradeBreakdown via
/// ScoreCalculator. This is the missing link between the TaskGraph/MistakeLog and
/// the grade screen numbers (Grade %, per-criteria breakdown).
///
/// Pure C#, unit-testable.
public class ExperimentGrader
{
    private readonly ScoreCalculator _calc;
    private readonly GradingConfig _cfg;

    public ExperimentGrader(ScoreCalculator calc, GradingConfig cfg = null)
    {
        _calc = calc ?? new ScoreCalculator(new RubricWeights());
        _cfg = cfg ?? new GradingConfig();
    }

    /// quizFraction: post-lab quiz score 0..1 (Documentation criterion).
    public GradeBreakdown Grade(TaskGraph graph, MistakeLog log, float elapsedSeconds, float parSeconds, float quizFraction)
    {
        // Completion ratio per rubric category, from the graph's tasks.
        var done = new Dictionary<RubricCategory, int>();
        var total = new Dictionary<RubricCategory, int>();
        if (graph != null)
        {
            foreach (var t in graph.Tasks)
            {
                total.TryGetValue(t.rubricCategory, out int tv); total[t.rubricCategory] = tv + 1;
                if (graph.IsComplete(t.taskId))
                { done.TryGetValue(t.rubricCategory, out int dv); done[t.rubricCategory] = dv + 1; }
            }
        }

        float Ratio(RubricCategory c)
        {
            total.TryGetValue(c, out int tv);
            if (tv == 0) return 1f; // no authored tasks in this category → full credit unless a penalty applies
            done.TryGetValue(c, out int dv);
            return (float)dv / tv;
        }

        int procMistakes = log != null ? log.CountOfAny(LabErrorType.WrongReagent, LabErrorType.WrongStep, LabErrorType.Overheat) : 0;
        int safetyMistakes = log != null ? log.CountOfAny(LabErrorType.FireSafety, LabErrorType.MissingPPE, LabErrorType.FumeHoodViolation, LabErrorType.ChemicalContact, LabErrorType.HazardousAction) : 0;
        int sanitationMistakes = log != null ? log.CountOf(LabErrorType.DroppedGlassware) : 0;

        var subScores = new Dictionary<RubricCategory, float>
        {
            { RubricCategory.Procedure,       Penalize(Ratio(RubricCategory.Procedure),       procMistakes,       _cfg.procedureMistakeStep) },
            { RubricCategory.ChemicalTests,   Ratio(RubricCategory.ChemicalTests) },
            { RubricCategory.MaterialsAndPPE, Penalize(Ratio(RubricCategory.MaterialsAndPPE), safetyMistakes,     _cfg.safetyMistakeStep) },
            { RubricCategory.TimeManagement,  ScoreCalculator.TimeSubScore(elapsedSeconds, parSeconds) },
            { RubricCategory.Sanitation,      Penalize(Ratio(RubricCategory.Sanitation),      sanitationMistakes, _cfg.sanitationMistakeStep) },
            { RubricCategory.Documentation,   Clamp01(quizFraction) },
        };

        return _calc.Compute(subScores);
    }

    private static float Penalize(float baseScore, int mistakes, float step)
    {
        return Clamp01(baseScore - mistakes * step);
    }

    private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
}
