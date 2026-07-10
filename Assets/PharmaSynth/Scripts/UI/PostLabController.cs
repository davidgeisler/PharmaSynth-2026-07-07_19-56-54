using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// The post-lab "Documentation" phase (manual's Data Sheet + quiz), shown on a
/// world-space tablet once the Chemical Tests phase is complete. The player enters
/// the yield and answers the module's multiple-choice questions; submitting
/// completes the terminal data-sheet task and ends the attempt with the quiz score
/// feeding the grader's Documentation criterion.
///
/// All UI refs are optional so the open→answer→submit→finish logic is unit-testable
/// headlessly (a scene-built canvas drives the same public methods).
public class PostLabController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private QuizBankLibrary library;

    [Header("UI (optional — logic works without them)")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text questionCounterText;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private Button[] optionButtons = new Button[0];
    [SerializeField] private TMP_Text[] optionLabels = new TMP_Text[0];
    [SerializeField] private TMP_Text explanationText;
    [SerializeField] private TMP_InputField yieldInput;   // legacy/optional
    [SerializeField] private TMP_Text yieldValueText;     // stepper display "Yield: NN %"
    [SerializeField] private Button submitButton;

    [Tooltip("Open automatically when ChemicalTests completes. The gatekeeper's review flow sets this false and opens the quiz itself after Jimenez's briefing.")]
    [SerializeField] private bool autoOpen = true;

    private QuizBank _bank;
    private int[] _answers;      // chosen option per question, -1 = unanswered
    private int _current;
    private float _yieldPercent = -1f;
    private bool _open;

    public bool IsOpen => _open;
    public QuizBank Bank => _bank;
    public int CurrentIndex => _current;

    public void SetRefs(ExperimentRunner r, QuizBankLibrary lib) { runner = r; library = lib; }

    private void OnEnable()  { if (runner != null) runner.PhaseCompleted += OnPhaseCompleted; }
    private void OnDisable() { if (runner != null) runner.PhaseCompleted -= OnPhaseCompleted; }

    public void SetAutoOpen(bool on) => autoOpen = on;

    private void OnPhaseCompleted(TaskPhase phase)
    {
        // Chemical Tests done → time to document. (Modules whose last phase is
        // ChemicalTests still open here; Submit simply finishes.)
        if (autoOpen && phase == TaskPhase.ChemicalTests) Open();
    }

    /// Open the quiz for the currently running module.
    public void Open()
    {
        var moduleId = runner != null && runner.Module != null ? runner.Module.moduleId : null;
        OpenFor(library != null ? library.GetBank(moduleId) : null);
    }

    /// Open for a specific bank (bank may be null → a yield-only data sheet).
    public void OpenFor(QuizBank bank)
    {
        _bank = bank;
        int n = bank != null ? bank.Count : 0;
        _answers = new int[n];
        for (int i = 0; i < n; i++) _answers[i] = -1;
        _current = 0;
        _yieldPercent = 0f;
        _open = true;
        if (root != null) root.SetActive(true);
        if (titleText != null) titleText.text = "Data Sheet & Documentation";
        RefreshYield();
        Render();
    }

    /// Record the yield the player entered (percent). Optional for the grade.
    public void SetYield(float percent) { _yieldPercent = Mathf.Clamp(percent, 0f, 100f); RefreshYield(); }
    public float Yield => _yieldPercent;

    /// Yield stepper buttons (e.g. −5 / +5). Clamped 0..100.
    public void AdjustYield(int delta) { SetYield(_yieldPercent + delta); }

    private void RefreshYield()
    {
        if (yieldValueText != null) yieldValueText.text = "Yield:  " + Mathf.RoundToInt(Mathf.Max(0f, _yieldPercent)) + " %";
    }

    /// Hooked to each option button (index passed by the button wiring).
    public void OnOptionSelected(int optionIndex)
    {
        if (!_open || _bank == null || _current < 0 || _current >= _bank.Count) return;
        AnswerCurrent(optionIndex);
        if (explanationText != null)
        {
            var q = _bank.questions[_current];
            explanationText.text = q.explanation;
        }
        if (_current < _bank.Count - 1) { _current++; Render(); }
        else Render();   // stay on last; Submit becomes available
    }

    /// Record an answer for the current question and advance the cursor no further.
    public void AnswerCurrent(int optionIndex) { Answer(_current, optionIndex); }

    public void Answer(int questionIndex, int optionIndex)
    {
        if (_answers == null || questionIndex < 0 || questionIndex >= _answers.Length) return;
        _answers[questionIndex] = optionIndex;
        if (submitButton != null) submitButton.gameObject.SetActive(AllAnswered);
    }

    public bool AllAnswered
    {
        get
        {
            if (_answers == null) return true;               // no questions → nothing to answer
            for (int i = 0; i < _answers.Length; i++) if (_answers[i] < 0) return false;
            return true;
        }
    }

    /// Fraction correct (0..1) — drives the grader's Documentation sub-score.
    public float ScoreFraction() => _bank != null ? _bank.Score(_answers) : 1f;

    /// Read the yield the player typed into the input field (if any).
    private void ReadYieldFromField()
    {
        if (yieldInput != null && float.TryParse(yieldInput.text, out var v)) _yieldPercent = v;
    }

    /// Void wrapper for the Submit button's UnityEvent (persistent listeners need void).
    public void Submit() { SubmitAndFinish(); }

    /// Finish the attempt: complete the terminal data-sheet task and grade.
    public ExperimentResult SubmitAndFinish()
    {
        ReadYieldFromField();
        // Complete the data-sheet / record task so the graph reaches 100%.
        if (runner != null && runner.Graph != null)
        {
            string recordId = FindDataSheetTaskId(runner.Graph);
            if (!string.IsNullOrEmpty(recordId)) runner.CompleteTask(recordId);
        }
        _open = false;
        if (root != null) root.SetActive(false);
        return runner != null ? runner.Finish(ScoreFraction()) : default;
    }

    private void Render()
    {
        if (_bank == null || _bank.Count == 0)
        {
            if (promptText != null) promptText.text = "Enter the yield you obtained, then submit your data sheet.";
            if (questionCounterText != null) questionCounterText.text = "";
            for (int i = 0; i < optionButtons.Length; i++)
                if (optionButtons[i] != null) optionButtons[i].gameObject.SetActive(false);
            if (submitButton != null) submitButton.gameObject.SetActive(true);
            return;
        }

        var q = _bank.questions[_current];
        if (questionCounterText != null) questionCounterText.text = "Question " + (_current + 1) + " / " + _bank.Count;
        if (promptText != null) promptText.text = q.prompt;
        for (int i = 0; i < optionButtons.Length; i++)
        {
            bool on = i < q.options.Count;
            if (optionButtons[i] != null) optionButtons[i].gameObject.SetActive(on);
            if (i < optionLabels.Length && optionLabels[i] != null && on) optionLabels[i].text = q.options[i];
        }
        if (explanationText != null && _answers[_current] < 0) explanationText.text = "";
        if (submitButton != null) submitButton.gameObject.SetActive(AllAnswered);
    }

    /// The terminal Data Sheet task (record-*) for a graph, or null if none.
    public static string FindDataSheetTaskId(TaskGraph graph)
    {
        if (graph == null) return null;
        foreach (var t in graph.Tasks)
            if (t != null && t.phase == TaskPhase.DataSheet && t.required) return t.taskId;
        return null;
    }
}
