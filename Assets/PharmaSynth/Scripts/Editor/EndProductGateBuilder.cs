#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Regular laboratory mode must NOT show ready-made end products (user
/// 2026-07-11) — attaches EndProductVisibility to both storage roots so
/// end-product bottles SetActive(false) in Play mode unless DemoSession.Active.
/// Idempotent; safe to re-run after any storage rework.
public static class EndProductGateBuilder
{
    [MenuItem("Tools/PharmaSynth/Wire End-Product Gate")]
    public static void Wire()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[EndProductGate] exit Play mode first.");
            return;
        }

        int wired = 0, gated = 0;
        foreach (var rootName in new[] { "ReagentShelf", "ReagentCabinets" })
        {
            var root = GameObject.Find(rootName);
            if (root == null) { Debug.LogWarning("[EndProductGate] no " + rootName + " in the open scene."); continue; }
            var gate = root.GetComponent<EndProductVisibility>();
            if (gate == null) gate = root.AddComponent<EndProductVisibility>();
            gated += gate.Rescan();   // edit-mode dry scan: count + validate wiring
            wired++;
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("<color=#4CD07D>[EndProductGate] " + wired + " roots wired, "
                  + gated + " end-product bottles will hide outside demo sessions</color>");
    }
}
#endif
