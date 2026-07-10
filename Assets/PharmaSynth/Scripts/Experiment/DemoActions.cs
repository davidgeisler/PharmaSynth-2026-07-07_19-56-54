using UnityEngine;

/// Demo-session auto-complete verbs (pure statics over existing runner/quiz
/// seams — no gate-state coupling, so they survive flow redesigns). Used by the
/// demo HUD buttons so panelists can sweep through an experiment quickly.
public static class DemoActions
{
    /// Complete the first available required task (skipping the DataSheet phase —
    /// the quiz submit owns that terminal task). Returns the taskId, or null when
    /// nothing is completable.
    public static string CompleteCurrentStep(ExperimentRunner runner)
    {
        if (runner == null || runner.Graph == null || !runner.IsRunning) return null;
        foreach (var t in runner.Graph.AvailableTasks())
        {
            if (t.phase == TaskPhase.DataSheet) continue;
            runner.CompleteTask(t.taskId);
            return t.taskId;
        }
        return null;
    }

    /// Complete every pre-DataSheet task (the ChemicalTests PhaseCompleted event
    /// then drives the normal post-experiment flow). Returns how many completed.
    public static int CompleteAllTasks(ExperimentRunner runner)
    {
        int n = 0;
        while (n < 500 && CompleteCurrentStep(runner) != null) n++;
        return n;
    }

    /// Answer every quiz question correctly. Returns true when the quiz is open
    /// and fully answered (caller then presses/calls Submit).
    public static bool AutoAnswerQuiz(PostLabController postLab)
    {
        if (postLab == null || !postLab.IsOpen) return false;
        var bank = postLab.Bank;
        int count = bank != null ? bank.Count : 0;
        for (int i = 0; i < count; i++)
            postLab.Answer(i, bank.questions[i].correctIndex);
        return postLab.AllAnswered;
    }
}
