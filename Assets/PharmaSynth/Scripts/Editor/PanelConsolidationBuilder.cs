#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// One procedures display (user 2026-07-10): the entrance LabTablet duplicated
/// the wrist holo board, and the wrist mini-panel duplicated the holo header —
/// three surfaces fighting over the same content, with the tablet's fixed rect
/// overflowing into its reaction footer. This menu retires the LabTablet
/// (deactivated, not deleted) and the MiniPanel, and upgrades the holo board to
/// the single panel: status header (ex mini-panel) + focused checklist + the
/// balanced-reaction footer (ex tablet). Idempotent.
public static class PanelConsolidationBuilder
{
    [MenuItem("Tools/PharmaSynth/Consolidate Procedure Panels")]
    public static void Consolidate()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[PanelConsolidation] exit Play mode first.");
            return;
        }

        var watch = FirstSceneObject<WristWatchController>();
        if (watch == null) { Debug.LogError("[PanelConsolidation] no WristWatchController in the open scene."); return; }
        var so = new SerializedObject(watch);

        // 1. Retire the entrance LabTablet, harvesting its reaction string first.
        string reaction = "";
        var tablet = FindInScene("LabTablet");
        if (tablet != null)
        {
            var tc = tablet.GetComponent<TabletChecklistController>();
            if (tc != null)
            {
                var tso = new SerializedObject(tc);
                var rp = tso.FindProperty("balancedReaction");
                if (rp != null) reaction = rp.stringValue;
            }
            if (tablet.activeSelf) tablet.SetActive(false);
        }

        // 2. Retire the wrist MiniPanel (the runtime also force-hides it).
        var panelProp = so.FindProperty("panel");
        if (panelProp != null && panelProp.objectReferenceValue is GameObject mini && mini.activeSelf)
            mini.SetActive(false);

        // 3. Upgrade the holo board: header + resized body + reaction footer.
        var holoProp = so.FindProperty("holoPanel");
        var bodyProp = so.FindProperty("holoBody");
        var holo = holoProp != null ? holoProp.objectReferenceValue as GameObject : null;
        var body = bodyProp != null ? bodyProp.objectReferenceValue as TMP_Text : null;
        if (holo == null || body == null)
        {
            Debug.LogError("[PanelConsolidation] holo board refs missing on WristWatchController.");
            return;
        }

        // Keep the procedures board readable as the player turns (user 2026-07-11:
        // "results/procedures panels must face the avatar"). PlaceHolo aims it on
        // summon; FaceCamera holds it facing the head every frame after.
        if (holo.GetComponent<FaceCamera>() == null)
        {
            var fc = holo.AddComponent<FaceCamera>();
            fc.yAxisOnly = true; fc.faceTowardCamera = false;
        }

        var summary = EnsureText(holo.transform, "HoloSummary", body,
            new Vector2(680f, 46f), new Vector2(0f, 368f), 26f, TextAlignmentOptions.Center,
            new Color(0.85f, 0.95f, 1f));
        var footer = EnsureText(holo.transform, "HoloReaction", body,
            new Vector2(680f, 40f), new Vector2(0f, -462f), 24f, TextAlignmentOptions.Center,
            new Color(0.65f, 0.85f, 1f, 0.9f));

        var bodyRt = body.rectTransform;
        bodyRt.sizeDelta = new Vector2(680f, 740f);
        bodyRt.anchoredPosition = new Vector2(0f, -75f);

        // 4. Wire the new refs + harvested reaction into the controller.
        so.FindProperty("holoSummary").objectReferenceValue = summary;
        so.FindProperty("holoReaction").objectReferenceValue = footer;
        if (!string.IsNullOrEmpty(reaction))
            so.FindProperty("balancedReaction").stringValue = reaction;
        so.ApplyModifiedPropertiesWithoutUndo();

        // 5. Re-seed the tour (its first stop taught the retired tablet).
        var guide = FirstSceneObject<LabTourGuide>();
        if (guide != null)
        {
            guide.SeedDefaults();
            EditorUtility.SetDirty(guide);
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[PanelConsolidation] LabTablet retired, MiniPanel retired, holo board upgraded"
                  + (tablet == null ? " (no LabTablet found — already retired?)" : "")
                  + (guide == null ? ", NO tour guide found" : ", tour re-seeded") + ".");
    }

    static T FirstSceneObject<T>() where T : Component
    {
        var all = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        return all.Length > 0 ? all[0] : null;
    }

    static GameObject FindInScene(string name)
    {
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == name) return root;
            var t = FindDeep(root.transform, name);
            if (t != null) return t.gameObject;
        }
        return null;
    }

    static Transform FindDeep(Transform t, string name)
    {
        for (int i = 0; i < t.childCount; i++)
        {
            var c = t.GetChild(i);
            if (c.name == name) return c;
            var deep = FindDeep(c, name);
            if (deep != null) return deep;
        }
        return null;
    }

    /// Load-or-create a TMP child on the holo board, styled after the body text.
    static TMP_Text EnsureText(Transform parent, string name, TMP_Text styleSource,
        Vector2 size, Vector2 anchoredPos, float fontSize, TextAlignmentOptions align, Color color)
    {
        var existing = parent.Find(name);
        TextMeshProUGUI text;
        if (existing != null) text = existing.GetComponent<TextMeshProUGUI>();
        else
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            text = go.AddComponent<TextMeshProUGUI>();
        }
        if (text == null) return null;
        if (styleSource != null && styleSource.font != null) text.font = styleSource.font;
        text.fontSize = fontSize;
        text.alignment = align;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        var rt = text.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
        return text;
    }
}
#endif
