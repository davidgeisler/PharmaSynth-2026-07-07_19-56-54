using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// Grab-able tablet's procedure card: Materials/Apparatus/Procedure checklist
/// built from the module's graphTasks, auto-ticking as tasks complete, with the
/// current available step marked. Also shows the balanced reaction footer.
///
/// Renders a text checklist into a single TMP_Text (simple, robust); a per-item
/// prefab version can layer on later. The line-building logic is a pure static so
/// it is unit-testable.
public class TabletChecklistController : MonoBehaviour
{
    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private TMP_Text checklistText;
    [SerializeField] private TMP_Text reactionText;
    [SerializeField, TextArea] private string balancedReaction = "";

    private void OnEnable()
    {
        if (runner == null) return;
        runner.ExperimentStarted += OnStarted;
        runner.TaskCompleted += OnChanged;
        runner.ProgressChanged += OnProgress;
    }

    private void OnDisable()
    {
        if (runner == null) return;
        runner.ExperimentStarted -= OnStarted;
        runner.TaskCompleted -= OnChanged;
        runner.ProgressChanged -= OnProgress;
    }

    private void OnStarted(ExperimentModuleDefinition m)
    {
        if (reactionText != null) reactionText.text = balancedReaction;
        Rebuild();
    }

    private void OnChanged(ExperimentTask t) => Rebuild();
    private void OnProgress(float p) => Rebuild();

    private void Rebuild()
    {
        if (checklistText == null || runner == null || runner.Graph == null) return;
        checklistText.text = BuildChecklistText(runner.Graph);
    }

    /// Text checklist grouped by phase: ☑ done, ▶ current (available), ☐ pending.
    public static string BuildChecklistText(TaskGraph graph)
    {
        if (graph == null) return string.Empty;
        var sb = new StringBuilder(256);
        TaskPhase? lastPhase = null;
        foreach (var t in graph.Tasks)
        {
            if (lastPhase == null || lastPhase.Value != t.phase)
            {
                if (lastPhase != null) sb.Append('\n');
                sb.Append("<b>").Append(PhaseLabel(t.phase)).Append("</b>\n");
                lastPhase = t.phase;
            }
            string mark = graph.IsComplete(t.taskId) ? "☑" : (graph.IsAvailable(t.taskId) ? "▶" : "☐");
            sb.Append(mark).Append(' ').Append(t.label).Append('\n');
        }
        return sb.ToString().TrimEnd();
    }

    public static string PhaseLabel(TaskPhase p)
    {
        switch (p)
        {
            case TaskPhase.ReagentPrep: return "Reagent Preparation";
            case TaskPhase.Synthesis: return "Synthesis";
            case TaskPhase.ChemicalTests: return "Chemical Tests";
            case TaskPhase.DataSheet: return "Data Sheet";
            default: return p.ToString();
        }
    }
}
