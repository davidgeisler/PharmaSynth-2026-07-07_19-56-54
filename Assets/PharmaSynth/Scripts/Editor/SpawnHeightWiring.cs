#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// Wires the fixed per-scene eye height onto the open scene's XR rig (user
/// 2026-07-11: menu room and lab need DIFFERENT fixed heights, not relative to
/// the player's real height). Run once per scene (MainMenu + SampleScene).
/// Idempotent. Tune the two constants below and re-run to adjust.
public static class SpawnHeightWiring
{
    /// ONE eye height everywhere (user 2026-07-11: "take the lab's height and
    /// apply it to the cube room" — per-scene divergence kept confusing tests;
    /// the cube room's furnishings were lowered to suit instead).
    public const float EyeHeight = 1.40f;

    [MenuItem("Tools/PharmaSynth/Wire Spawn Height (Fixed Per Scene)")]
    public static void Wire()
    {
        if (Application.isPlaying) { Debug.LogWarning("[SpawnHeight] exit Play mode first."); return; }

        var xr = GameObject.Find("XR Origin (XR Rig)");
        if (xr == null) { Debug.LogError("[SpawnHeight] no 'XR Origin (XR Rig)' in the open scene."); return; }

        Transform offset = FindDeep(xr, "Camera Offset");
        Transform cam = FindDeep(xr, "Main Camera");
        if (offset == null || cam == null)
        { Debug.LogError("[SpawnHeight] rig missing Camera Offset / Main Camera child."); return; }

        float target = EyeHeight;

        var fixedHeight = xr.GetComponent<SeatedHeightBoost>();
        if (fixedHeight == null) fixedHeight = xr.AddComponent<SeatedHeightBoost>();
        fixedHeight.Bind(cam, offset);
        fixedHeight.SetTarget(target);
        EditorUtility.SetDirty(fixedHeight);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(xr.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("<color=#4CD07D>[SpawnHeight] fixed eye height " + target + " m wired in '" + xr.scene.name + "'.</color>");
    }

    static Transform FindDeep(GameObject root, string exact)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == exact) return t;
        return null;
    }
}
#endif
