#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// Adopts every scene item's CURRENT transform as its DropRespawn home (user
/// 2026-07-10: "I have manually relocated some equipment, please make those their
/// default spawn point"). Without this, manually moved props teleport back to
/// their old serialized homes after ~25 s idle / a kill-Z fall / a reset.
///
/// Tools ▸ PharmaSynth ▸ Re-Home Scene Items (Adopt Current) — run in SampleScene
/// edit mode after ANY manual re-arrangement, then save the scene.
public static class ReHomeSceneItems
{
    [MenuItem("Tools/PharmaSynth/Re-Home Scene Items (Adopt Current)")]
    public static void Adopt()
    {
        if (Application.isPlaying) { Debug.LogWarning("[ReHome] exit Play mode first."); return; }

        int n = 0;
        foreach (var dr in Object.FindObjectsByType<DropRespawn>(FindObjectsInactive.Include))
        {
            Undo.RecordObject(dr, "Re-Home Scene Items");
            dr.SetHome(dr.transform.position, dr.transform.rotation);
            EditorUtility.SetDirty(dr);
            n++;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("<color=#4CD07D>[ReHome] adopted current transforms as home for " + n + " item(s)</color>");
    }
}
#endif
