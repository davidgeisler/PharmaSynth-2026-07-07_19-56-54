using UnityEngine;
using UnityEngine.InputSystem;

/// Keyboard driver for in-editor testing of the experiment loop without needing
/// full XR interaction. Lets you watch the HUD, Pharmee, and grade screen react.
///   B = begin/restart · 1-5 = complete step N · F = finish · R = retry
/// Disabled in builds unless enableInBuild is set.
public class DevExperimentDriver : MonoBehaviour
{
    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private ExperimentStarter starter;
    [SerializeField] private bool enableInBuild = false;

    public void Setup(ExperimentRunner r, ExperimentStarter s) { runner = r; starter = s; }

    private void Update()
    {
        if (!Application.isEditor && !enableInBuild) return;
        var kb = Keyboard.current;
        if (kb == null || runner == null) return;

        if (kb.bKey.wasPressedThisFrame)
        {
            if (starter != null) starter.Begin(); else runner.StartExperiment();
            Debug.Log("[Dev] Begin");
        }
        if (kb.digit1Key.wasPressedThisFrame) CompleteIndex(0);
        if (kb.digit2Key.wasPressedThisFrame) CompleteIndex(1);
        if (kb.digit3Key.wasPressedThisFrame) CompleteIndex(2);
        if (kb.digit4Key.wasPressedThisFrame) CompleteIndex(3);
        if (kb.digit5Key.wasPressedThisFrame) CompleteIndex(4);
        if (kb.fKey.wasPressedThisFrame) { var res = runner.Finish(1f); Debug.Log("[Dev] Finish → grade " + res.grade.Total.ToString("0") + "% passed=" + res.passed); }
        if (kb.rKey.wasPressedThisFrame) { runner.Retry(); Debug.Log("[Dev] Retry"); }
    }

    private void CompleteIndex(int i)
    {
        if (runner.Graph == null) { Debug.LogWarning("[Dev] Not started — press B first"); return; }
        var tasks = runner.Graph.Tasks;
        if (i < 0 || i >= tasks.Count) return;
        var res = runner.CompleteTask(tasks[i].taskId);
        Debug.Log("[Dev] step " + (i + 1) + " '" + tasks[i].label + "' → " + res);
    }
}
