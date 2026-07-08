using System;
using System.Collections.Generic;
using UnityEngine;

/// A post-lab multiple-choice question (tablet "Documentation" phase). Client-reviewable
/// content — kept as data so questions can be edited without code changes.
[Serializable]
public class QuizQuestion
{
    [TextArea(1, 3)] public string prompt = "";
    public List<string> options = new List<string>();
    [Tooltip("Index into options[] of the correct answer.")]
    public int correctIndex = 0;
    [TextArea(1, 2), Tooltip("Shown after answering — the teaching moment.")]
    public string explanation = "";

    public bool IsValid()
        => !string.IsNullOrEmpty(prompt) && options != null && options.Count >= 2
           && correctIndex >= 0 && correctIndex < options.Count;
}

/// The 3-question post-lab quiz for one experiment (manual's "Documentation" criterion,
/// VR-feasible form). One asset per module, keyed by moduleId.
[CreateAssetMenu(fileName = "QuizBank", menuName = "PharmaSynth/Quiz Bank")]
public class QuizBank : ScriptableObject
{
    public string moduleId = "";
    public List<QuizQuestion> questions = new List<QuizQuestion>();

    public int Count => questions.Count;

    /// Fraction correct given the player's chosen option index per question
    /// (−1 = unanswered). Drives the grader's documentation/quiz sub-score.
    public float Score(IReadOnlyList<int> answers)
    {
        if (questions.Count == 0 || answers == null) return 0f;
        int correct = 0;
        for (int i = 0; i < questions.Count && i < answers.Count; i++)
            if (answers[i] == questions[i].correctIndex) correct++;
        return (float)correct / questions.Count;
    }

    public bool AllValid()
    {
        if (questions.Count == 0) return false;
        foreach (var q in questions) if (q == null || !q.IsValid()) return false;
        return true;
    }
}
