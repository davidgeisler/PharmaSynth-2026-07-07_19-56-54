#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

/// Reagent-label compositor (§3, client style pick: MODERN). For every
/// Reagent_* bottle on the shelf: renders LabelBase_Modern + the chemical's
/// name (crisp TMP text — never AI typography) to a PNG, builds a material,
/// and mounts a label quad on the bottle facing the aisle.
/// Tools ▸ PharmaSynth ▸ Generate Reagent Labels — idempotent, re-run anytime.
public static class LabelForge
{
    const string BasePath = "Assets/PharmaSynth/Art/Generated/Labels/LabelBase_Modern.png";
    const string OutDir = "Assets/PharmaSynth/Art/Generated/Labels/Composited";
    const int W = 512, H = 683;

    [MenuItem("Tools/PharmaSynth/Generate Reagent Labels")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[LabelForge] exit Play mode first."); return; }
        var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(BasePath);
        if (baseTex == null) { Debug.LogError("[LabelForge] missing base " + BasePath); return; }
        Directory.CreateDirectory(OutDir);

        var shelf = GameObject.Find("ReagentShelf");
        if (shelf == null) { Debug.LogError("[LabelForge] no ReagentShelf"); return; }

        int made = 0;
        foreach (Transform bottle in shelf.transform)
        {
            var lp = bottle.GetComponent<LiquidPhysics>();
            string chem = lp != null && lp.currentChemical != null ? lp.currentChemical.chemicalName : null;
            if (string.IsNullOrEmpty(chem)) continue;

            string safe = chem.Replace(" ", "").Replace("%", "").Replace("/", "-");
            string pngPath = OutDir + "/Label_" + safe + ".png";
            RenderLabel(baseTex, chem, pngPath);

            string matPath = OutDir + "/Label_" + safe + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
            mat.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(mat);

            MountQuad(bottle, mat);
            made++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log("<color=#4CD07D>[LabelForge] composited + mounted " + made + " labels</color>");
    }

    /// Render base + centred black name text via an off-scene ortho camera.
    static void RenderLabel(Texture2D baseTex, string chem, string pngPath)
    {
        var root = new GameObject("~LabelForge");
        try
        {
            var far = new Vector3(1000f, 1000f, 1000f);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.transform.SetParent(root.transform);
            quad.transform.position = far;
            quad.transform.localScale = new Vector3(0.75f, 1f, 1f);
            var unlit = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { mainTexture = baseTex };
            quad.GetComponent<MeshRenderer>().sharedMaterial = unlit;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(root.transform);
            // Quad faces -Z, camera sits at z-1 looking +Z; text floats just in front.
            textGo.transform.position = far + new Vector3(0f, -0.06f, -0.01f);
            textGo.transform.rotation = Quaternion.identity;
            var tmp = textGo.AddComponent<TextMeshPro>();
            tmp.rectTransform.sizeDelta = new Vector2(0.6f, 0.5f);
            tmp.text = chem;
            tmp.color = Color.black;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.2f; tmp.fontSizeMax = 1.6f;
            tmp.ForceMeshUpdate();

            var camGo = new GameObject("Cam");
            camGo.transform.SetParent(root.transform);
            camGo.transform.position = far + new Vector3(0f, 0f, -1f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 0.5f;
            cam.nearClipPlane = 0.01f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.white;

            var rt = new RenderTexture(W, H, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(unlit);
            AssetDatabase.ImportAsset(pngPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    /// Curved label band hugging the bottle (user 2026-07-10: flat quads read
    /// as pasted nameplates). Thin tubes get NO label — the proximity tag
    /// already names them and a plate overpowers the glass.
    const float MinLabelRadius = 0.016f;

    static void MountQuad(Transform bottle, Material mat)
    {
        var old = bottle.Find("NameLabel");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var rs = bottle.GetComponentsInChildren<Renderer>();
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);

        float radius = Mathf.Max(b.extents.x, b.extents.z);
        if (radius < MinLabelRadius) return;                     // thin tube: skip

        Vector3 outward = new Vector3(0.2f, 0f, -2.5f) - bottle.position;   // toward the room
        outward.y = 0f; outward = outward.normalized;

        var band = new GameObject("NameLabel", typeof(MeshFilter), typeof(MeshRenderer));
        band.transform.SetParent(bottle, true);
        band.GetComponent<MeshFilter>().sharedMesh = ArcMesh();
        band.GetComponent<MeshRenderer>().sharedMaterial = mat;

        // Body radius (not the cap/neck): sample at the label's height where
        // possible — approximated as 92% of the widest extent, +2 mm clearance.
        float bandRadius = radius * 0.92f + 0.002f;
        float bandHeight = Mathf.Min(b.size.y * 0.42f, bandRadius * 2.4f);
        band.transform.localScale = Vector3.one;
        var ls = band.transform.lossyScale;
        band.transform.localScale = new Vector3(bandRadius / Mathf.Max(ls.x, 1e-4f),
                                                bandHeight / Mathf.Max(ls.y, 1e-4f),
                                                bandRadius / Mathf.Max(ls.z, 1e-4f));
        band.transform.position = new Vector3(b.center.x, b.center.y - b.size.y * 0.08f, b.center.z);
        band.transform.rotation = Quaternion.LookRotation(-outward);        // arc centre (-Z) toward the room
    }

    static Mesh _arc;

    /// Shared unit arc mesh: radius 1, height 1, 140° facing -Z. Saved as an
    /// asset so scene references survive; scaled per bottle by the transform.
    static Mesh ArcMesh()
    {
        string path = OutDir + "/LabelArc.asset";
        if (_arc == null) _arc = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (_arc != null) return _arc;

        const int SEG = 16;
        const float ARC = 140f * Mathf.Deg2Rad;
        var verts = new Vector3[(SEG + 1) * 2];
        var uvs = new Vector2[verts.Length];
        var norms = new Vector3[verts.Length];
        var tris = new int[SEG * 6];
        for (int i = 0; i <= SEG; i++)
        {
            float a = -ARC * 0.5f + ARC * i / SEG;
            float x = Mathf.Sin(a), z = -Mathf.Cos(a);
            verts[i * 2] = new Vector3(x, -0.5f, z);
            verts[i * 2 + 1] = new Vector3(x, 0.5f, z);
            float u = (float)i / SEG;
            uvs[i * 2] = new Vector2(u, 0f);
            uvs[i * 2 + 1] = new Vector2(u, 1f);
            norms[i * 2] = norms[i * 2 + 1] = new Vector3(x, 0f, z);
        }
        for (int i = 0; i < SEG; i++)
        {
            // Wound so faces point OUTWARD (away from the bottle axis).
            int v = i * 2, t = i * 6;
            tris[t] = v; tris[t + 1] = v + 1; tris[t + 2] = v + 2;
            tris[t + 3] = v + 1; tris[t + 4] = v + 3; tris[t + 5] = v + 2;
        }
        _arc = new Mesh { name = "LabelArc", vertices = verts, uv = uvs, normals = norms, triangles = tris };
        AssetDatabase.CreateAsset(_arc, path);
        return _arc;
    }
}
#endif
