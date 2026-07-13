#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// W5.12 (user 2026-07-13): the hand-built Methane station pads still render as
/// coloured cubes on the table — the DynamicStage builder hides its own pads
/// (padMr.enabled = false, W5.8) but the authored Station_* objects were missed.
/// The pads are purely cosmetic: the trigger COLLIDER + sensors that detect each
/// step stay, and the guides/labels tell the player where to act — so the cube
/// mesh just clutters the view. This disables the MeshRenderer on every
/// Station_* pad while leaving all functionality intact. Idempotent.
public static class HideStationPads
{
    [MenuItem("Tools/PharmaSynth/Hide Station Pads")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[HidePads] exit Play mode first."); return; }

        int hidden = 0;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null || !t.name.StartsWith("Station_")) continue;
            var mr = t.GetComponent<MeshRenderer>();
            if (mr != null && mr.enabled) { mr.enabled = false; EditorUtility.SetDirty(mr); hidden++; }
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"<color=#4CD07D>[HidePads] hid {hidden} station pad mesh(es) — colliders + sensors kept, "
                  + "guides do the pointing. No more coloured cubes on the table.</color>");
    }
}
#endif
