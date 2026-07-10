#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Builds the lab's hazard-alarm fixture (manuscript: "flashing lights, warning
/// messages, alarm beeps"): a small red ceiling box + one red point light +
/// LabAlarm, centred over the lab. Idempotent.
public static class LabAlarmBuilder
{
    [MenuItem("Tools/PharmaSynth/Build Lab Alarm")]
    public static void Build()
    {
        if (Application.isPlaying) { Debug.LogWarning("[LabAlarmBuilder] exit Play mode first."); return; }

        var existing = GameObject.Find("LabAlarmFixture");
        GameObject go = existing != null ? existing : GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "LabAlarmFixture";
        if (go.TryGetComponent<Collider>(out var col)) Object.DestroyImmediate(col);
        go.transform.position = new Vector3(0f, 2.72f, -2.2f);   // ceiling, mid-lab
        go.transform.localScale = new Vector3(0.16f, 0.1f, 0.16f);

        var rend = go.GetComponent<Renderer>();
        if (rend != null && (rend.sharedMaterial == null || rend.sharedMaterial.name.StartsWith("Default")))
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.35f, 0.08f, 0.08f);
            mat.EnableKeyword("_EMISSION");
            rend.sharedMaterial = mat;
        }

        var lightT = go.transform.Find("AlarmLight");
        GameObject lightGo = lightT != null ? lightT.gameObject : new GameObject("AlarmLight");
        lightGo.transform.SetParent(go.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, -1.2f, 0f);
        // Explicit null check — `??` bypasses Unity's fake-null and explodes in editor code.
        var l = lightGo.GetComponent<Light>();
        if (l == null) l = lightGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.18f, 0.12f);
        l.range = 9f;
        l.intensity = 0f;
        l.shadows = LightShadows.None;
        l.enabled = false;

        var alarm = go.GetComponent<LabAlarm>();
        if (alarm == null) alarm = go.AddComponent<LabAlarm>();
        alarm.Bind(l, rend);
        EditorUtility.SetDirty(alarm);

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[LabAlarmBuilder] alarm fixture built at the lab ceiling.");
    }
}
#endif
