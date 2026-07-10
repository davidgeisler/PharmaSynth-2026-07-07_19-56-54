using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// Focused checklist text for the holo procedures board (user 2026-07-10: the
/// flat pads rendered every phase at full detail into a fixed rect, so long
/// checklists overflowed and overlaid the reaction footer — unreadable).
/// Instead of shrinking text to fit, collapse what the player doesn't need:
/// completed phases fold to a "done n/n" line, future phases to a "(n steps)"
/// stub, and only the ACTIVE phase renders its full step list. Line count is
/// bounded by construction (phases + longest single phase), so the text always
/// fits its rect at a readable size.
public static class ChecklistPager
{
    /// The phase the player is working in: the phase of the first available
    /// task, else the first phase with an incomplete task (all-complete → null).
    public static TaskPhase? ActivePhase(TaskGraph graph)
    {
        if (graph == null) return null;
        foreach (var t in graph.Tasks)
            if (graph.IsAvailable(t.taskId)) return t.phase;
        foreach (var t in graph.Tasks)
            if (!graph.IsComplete(t.taskId)) return t.phase;
        return null;
    }

    public static string BuildFocusedText(TaskGraph graph)
    {
        if (graph == null) return string.Empty;
        TaskPhase? active = ActivePhase(graph);

        // Ordered distinct phases + counts (authoring order groups phases).
        var order = new List<TaskPhase>();
        var total = new Dictionary<TaskPhase, int>();
        var done = new Dictionary<TaskPhase, int>();
        foreach (var t in graph.Tasks)
        {
            if (!total.ContainsKey(t.phase)) { order.Add(t.phase); total[t.phase] = 0; done[t.phase] = 0; }
            total[t.phase]++;
            if (graph.IsComplete(t.taskId)) done[t.phase]++;
        }

        var sb = new StringBuilder(256);
        bool first = true;
        foreach (var phase in order)
        {
            if (!first) sb.Append('\n');
            first = false;
            string label = TabletChecklistController.PhaseLabel(phase);
            if (active.HasValue && phase == active.Value)
            {
                sb.Append("<b>").Append(label).Append("</b>\n");
                foreach (var t in graph.Tasks)
                {
                    if (t.phase != phase) continue;
                    string mark = graph.IsComplete(t.taskId) ? "<color=#5FD08A>•</color>"
                                : (graph.IsAvailable(t.taskId) ? "<color=#61D6FF>»</color>"
                                : "<color=#7C8AA5>□</color>");
                    sb.Append(mark).Append(' ').Append(GlyphSafe.Sanitize(t.label)).Append('\n');
                }
            }
            else if (done[phase] >= total[phase])
            {
                sb.Append("<b>").Append(label).Append("</b>  <color=#5FD08A>• done ")
                  .Append(done[phase]).Append('/').Append(total[phase]).Append("</color>\n");
            }
            else
            {
                sb.Append("<color=#7C8AA5>□ <b>").Append(label).Append("</b> (")
                  .Append(total[phase]).Append(" steps)</color>\n");
            }
        }
        return sb.ToString().TrimEnd();
    }

    /// One-line status header for the holo board — absorbs the retired wrist
    /// mini-panel's summary (current step · progress · mastery).
    public static string BuildHeader(ExperimentRunner runner)
    {
        if (runner == null || runner.Graph == null) return "";
        string current = "—";
        foreach (var t in runner.Graph.AvailableTasks()) { current = GlyphSafe.Sanitize(t.label); break; }
        return "<color=#61D6FF>»</color> " + current
             + "   <color=#61D6FF>" + ExperimentHudController.FormatPercent(runner.Progress01) + "</color>"
             + "   Mastery " + Mathf.RoundToInt(runner.OverallMastery * 100f) + "%";
    }
}
