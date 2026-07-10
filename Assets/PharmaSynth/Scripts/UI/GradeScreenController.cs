using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// End-of-experiment grade screen. Populated from an ExperimentResult: overall
/// grade %, mistakes, time, per-criteria breakdown, and PASSED / TRY AGAIN based
/// on the two-part gate. Retry/Continue are wired via UnityEvents.
public class GradeScreenController : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text gradeText;
    [SerializeField] private TMP_Text mistakesText;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text breakdownText;
    [SerializeField] private GameObject passedVisuals;   // confetti + happy Pharmee
    [SerializeField] private GameObject failedVisuals;
    [SerializeField] private GameObject continueButton;  // hidden until passed

    [Header("Events")]
    public UnityEvent onRetry;
    public UnityEvent onContinue;

    [Header("Auto-show")]
    [SerializeField] private ExperimentRunner runner;

    private void OnEnable()
    {
        if (root != null) root.SetActive(false);
        if (runner != null) runner.ExperimentFinished += Show;
    }

    private void OnDisable()
    {
        if (runner != null) runner.ExperimentFinished -= Show;
    }

    public void SetRunner(ExperimentRunner r)
    {
        if (runner != null) runner.ExperimentFinished -= Show;
        runner = r;
        if (runner != null && isActiveAndEnabled) runner.ExperimentFinished += Show;
    }

    /// Subscribe this to ExperimentRunner.ExperimentFinished.
    public void Show(ExperimentResult r)
    {
        if (root != null) root.SetActive(true);
        if (gradeText != null) gradeText.text = Mathf.RoundToInt(r.grade.Total) + "%";
        if (mistakesText != null) mistakesText.text = r.mistakeCount.ToString();
        if (timeText != null) timeText.text = ExperimentHudController.FormatTime(r.elapsedSeconds);
        if (resultText != null) resultText.text = r.passed ? "PASSED" : "TRY AGAIN";
        if (breakdownText != null) breakdownText.text = BuildBreakdown(r);
        if (passedVisuals != null) passedVisuals.SetActive(r.passed);
        if (failedVisuals != null) failedVisuals.SetActive(!r.passed);
        if (continueButton != null) continueButton.SetActive(r.passed); // gate: can't advance until passed
        if (AudioService.Instance != null) AudioService.Instance.Play(r.passed ? "grade-pass" : "grade-fail");
        // Confetti burst over the panel on a pass (VFX-set completion 2026-07-10).
        if (r.passed && Application.isPlaying)
        {
            var origin = (passedVisuals != null ? passedVisuals.transform
                        : (root != null ? root.transform : transform));
            EffectVfx.Confetti(origin.position + Vector3.up * 0.2f);
        }
    }

    public void Hide() { if (root != null) root.SetActive(false); }

    public void OnRetryPressed() { Hide(); onRetry?.Invoke(); }
    public void OnContinuePressed() { Hide(); onContinue?.Invoke(); }

    /// Per-criteria lines + the gate reason (why passed / what fell short).
    public static string BuildBreakdown(ExperimentResult r)
    {
        var b = r.grade;
        var sb = new StringBuilder(256);
        sb.Append("Procedure: ").Append(Mathf.RoundToInt(b.Procedure)).Append('\n');
        sb.Append("Chemical Tests: ").Append(Mathf.RoundToInt(b.ChemicalTests)).Append('\n');
        sb.Append("Materials & PPE: ").Append(Mathf.RoundToInt(b.MaterialsAndPPE)).Append('\n');
        sb.Append("Time Management: ").Append(Mathf.RoundToInt(b.TimeManagement)).Append('\n');
        sb.Append("Sanitation: ").Append(Mathf.RoundToInt(b.Sanitation)).Append('\n');
        sb.Append("Documentation: ").Append(Mathf.RoundToInt(b.Documentation)).Append('\n');
        sb.Append("Mastery: ").Append(Mathf.RoundToInt(r.overallMastery * 100f)).Append('%');
        if (!r.passed)
        {
            if (!r.gradePassed) sb.Append("\n<color=#FF7070>Grade below the pass mark.</color>");
            if (!r.masteryPassed) sb.Append("\n<color=#FF7070>Mastery not yet demonstrated — keep practicing.</color>");
        }
        return sb.ToString();
    }
}
