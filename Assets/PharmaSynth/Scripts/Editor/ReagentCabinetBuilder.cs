#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// Builds the raw-reagent storage (user 2026-07-10: the manuscript's ~54
/// materials must exist in the lab): three open shelf units against the wall,
/// stocked from RawReagentCatalog with nature-appropriate labware — reagent
/// bottles, amber bottles for the light-sensitive, powder jars, dropper bottles,
/// consumable boxes (litmus/matches/cotton/filter) and an ice bucket. Every
/// bottle is grabbable, pourable, spill-graded and hover-explained. Chemicals
/// already displayed on the legacy ReagentShelf are skipped. Re-runnable: the
/// ReagentCabinets root is cleared and rebuilt deterministically.
public static class ReagentCabinetBuilder
{
    const string RootName = "ReagentCabinets";
    const float UnitW = 1.16f, UnitH = 1.95f, UnitD = 0.6f;   // depth matches the counters (user 2026-07-11)
    const float RootY = 0.3f;            // lifted off the floor (user 2026-07-11: base overlapped it)
    const int SlotsPerShelf = 7;
    static readonly float[] ShelfY = { 0.5f, 0.9f, 1.3f, 1.65f };
    const string TripoBucketPath = "Assets/PharmaSynth/Art/Generated/Refs/IceBucket.prefab";
    static readonly Color Body = new Color(0.78f, 0.8f, 0.82f);
    static readonly Color Board = new Color(0.3f, 0.33f, 0.4f);

    const string MatDir = "Assets/PharmaSynth/Art/Generated/Materials/";
    /// AI-generated PBR materials (user 2026-07-11) — carcass laminate, cardboard
    /// for the consumable boxes, brushed steel for the ice bucket. Load-or-null.
    static Material Laminate => Load("LabLaminate");
    static Material Cardboard => Load("Cardboard");
    static Material Steel => Load("BrushedSteel");
    static Material Load(string name) => AssetDatabase.LoadAssetAtPath<Material>(MatDir + name + ".mat");

    /// LabwareKind → SceneAssetLibrary prefab (the bottle kinds; box/bucket are procedural).
    public static string PrefabFor(RawReagentCatalog.LabwareKind kind)
    {
        switch (kind)
        {
            case RawReagentCatalog.LabwareKind.ReagentBottle: return "Vial_WithLabel";
            case RawReagentCatalog.LabwareKind.AmberBottle: return "Vial_Brown_WithLabel";
            case RawReagentCatalog.LabwareKind.PowderJar: return "Beaker_100mL_WithLiquid";
            case RawReagentCatalog.LabwareKind.DropperBottle: return "WashBottle_WithLabel";
            default: return null;
        }
    }

    [MenuItem("Tools/PharmaSynth/Build Reagent Cabinets")]
    public static void Build()
    {
        if (Application.isPlaying) { Debug.LogWarning("[ReagentCabinets] exit Play mode first."); return; }

        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>("Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset");
        var registry = AssetDatabase.LoadAssetAtPath<ReactionRegistry>("Assets/PharmaSynth/ScriptableObjects/Reactions/MasterReactionRegistry.asset");
        var runner = Object.FindAnyObjectByType<ExperimentRunner>();
        if (lib == null) { Debug.LogError("[ReagentCabinets] SceneAssetLibrary not found."); return; }

        // Chemicals already on display at the legacy shelf → skip duplicates.
        var shelfNames = new HashSet<string>();
        var shelf = GameObject.Find("ReagentShelf");
        if (shelf != null)
            foreach (var lp in shelf.GetComponentsInChildren<LiquidPhysics>(true))
                if (lp.currentChemical != null) shelfNames.Add(lp.currentChemical.chemicalName);

        // Root: rebuild, but PRESERVE manual placement (user 2026-07-11: units
        // were repositioned by hand) — capture each existing unit's transform
        // before clearing and rebuild it exactly there. Only a unit that never
        // existed gets the computed wall-anchor default.
        var root = GameObject.Find(RootName);
        if (root == null) root = new GameObject(RootName);
        var keepPos = new Dictionary<string, Vector3>();
        var keepRot = new Dictionary<string, Quaternion>();
        for (int i = root.transform.childCount - 1; i >= 0; i--)
        {
            var child = root.transform.GetChild(i);
            if (child.name.StartsWith("CabinetUnit_"))
            {
                keepPos[child.name] = child.position;
                keepRot[child.name] = child.rotation;
            }
            Object.DestroyImmediate(child.gameObject);
        }

        // Wall anchor: cast toward +x from mid-room (the second island's freed
        // side). Fallback keeps everything usable if the cast misses.
        float wallX = 4.4f;
        if (Physics.Raycast(new Vector3(0f, 1.2f, -2.6f), Vector3.right, out var wallHit, 9f, ~0, QueryTriggerInteraction.Ignore))
            wallX = wallHit.point.x;
        float ux = wallX - UnitD * 0.5f - 0.02f;

        // Two units (user 2026-07-11: the third is unnecessary — denser shelves +
        // the top shelf give plenty of room, and the freed wall keeps the counter clear).
        var groups = new[]
        {
            RawReagentCatalog.GroupAcids,
            RawReagentCatalog.GroupOrganics + "|" + RawReagentCatalog.GroupTests + "|" + RawReagentCatalog.GroupConsumables,
        };
        float[] uz = { -1.7f, -3.05f };

        // AI-generated laminate on the carcass (falls back to flat colour if the
        // material asset is missing); dark board for the shelf ledges.
        var bodyMat = Laminate != null ? Laminate : MakeMat(Body);
        var boardMat = MakeMat(Board);
        int stocked = 0, skipped = 0;

        for (int u = 0; u < groups.Length; u++)
        {
            string unitName = "CabinetUnit_" + (u + 1);
            Vector3 unitPos = keepPos.TryGetValue(unitName, out var kp) ? kp : new Vector3(ux, RootY, uz[u]);
            Quaternion unitRot = keepRot.TryGetValue(unitName, out var kr) ? kr : Quaternion.identity;
            var unit = BuildUnit(root.transform, unitName, unitPos, unitRot, bodyMat, boardMat);
            var rows = new List<RawReagentCatalog.Row>();
            foreach (var row in RawReagentCatalog.Rows)
                if (groups[u].Contains(row.group)) rows.Add(row);

            int slot = 0;
            foreach (var row in rows)
            {
                if (shelfNames.Contains(row.chemicalName)) { skipped++; continue; }
                int shelfIdx = Mathf.Min(slot / SlotsPerShelf, ShelfY.Length - 1);
                float along = -UnitW * 0.5f + 0.11f + (slot % SlotsPerShelf) * 0.145f;
                // Slots in UNIT-LOCAL space (survives moved/rotated units): shelves
                // run along local z, bottles sit toward the open −x face.
                var pos = unit.transform.TransformPoint(new Vector3(-0.12f, ShelfY[shelfIdx] + 0.01f, along));
                if (StockRow(row, pos, unit.transform, lib, registry, runner)) { stocked++; slot++; }
            }
        }

        RelocatePrinter();

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[ReagentCabinets] built {groups.Length} units at x={ux:F2} (base y={RootY}): {stocked} materials stocked, " +
                  $"{skipped} already on the ReagentShelf. Run Re-Home Scene Items next.");
    }

    /// The copier ('Environment/Printe') sat inside the old unit-1 footprint. Move
    /// it onto the free right-side table and make it grabbable (user 2026-07-11).
    /// Only relocates while it still sits in the cabinet wall zone, so a manual
    /// re-arrangement is never snapped back by a re-run.
    static void RelocatePrinter()
    {
        var printer = FindDeepByName("Printe");
        if (printer == null) printer = FindDeepByName("Printer");
        if (printer == null) { Debug.LogWarning("[ReagentCabinets] no printer found to relocate."); return; }

        bool inCabinetZone = printer.position.x > 3.6f && printer.position.z > -2.3f;
        if (inCabinetZone)
        {
            // Table_2 (1) top: y≈1.01, z span ≈ −4.5..−6.8.
            printer.position = new Vector3(4.02f, 1.01f, -5.2f);
            printer.rotation = Quaternion.Euler(0f, -90f, 0f);   // face the room
            PrefabUtility.RecordPrefabInstancePropertyModifications(printer);
        }

        var go = printer.gameObject;
        if (go.GetComponentInChildren<Collider>() == null)
        {
            var col = go.AddComponent<BoxCollider>();
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                col.center = go.transform.InverseTransformPoint(b.center);
                Vector3 ls = go.transform.lossyScale;
                col.size = new Vector3(b.size.x / Mathf.Max(1e-4f, Mathf.Abs(ls.x)),
                                       b.size.y / Mathf.Max(1e-4f, Mathf.Abs(ls.y)),
                                       b.size.z / Mathf.Max(1e-4f, Mathf.Abs(ls.z)));
            }
        }
        AddPropPhysics(go, 5f);   // grabbable, kinematic-parked like the stools
        var label = go.GetComponent<ProximityLabel>();
        if (label == null) label = go.AddComponent<ProximityLabel>();
        label.SetLabel("Printer", 1.4f);
        Debug.Log("[ReagentCabinets] printer relocated to the side table + grabbable.");
    }

    static Transform FindDeepByName(string name)
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t.name == name) return t;
        return null;
    }

    static GameObject BuildUnit(Transform parent, string name, Vector3 pos, Quaternion rot, Material body, Material board)
    {
        var unit = new GameObject(name);
        unit.transform.SetParent(parent, false);
        unit.transform.SetPositionAndRotation(pos, rot);

        // Carcass: back + two sides + top/bottom (open front toward −x).
        MakePanel(unit.transform, "Back", new Vector3(UnitD * 0.5f - 0.02f, UnitH * 0.5f, 0f), new Vector3(0.04f, UnitH, UnitW), body);
        MakePanel(unit.transform, "SideL", new Vector3(0f, UnitH * 0.5f, -UnitW * 0.5f), new Vector3(UnitD, UnitH, 0.04f), body);
        MakePanel(unit.transform, "SideR", new Vector3(0f, UnitH * 0.5f, UnitW * 0.5f), new Vector3(UnitD, UnitH, 0.04f), body);
        MakePanel(unit.transform, "Top", new Vector3(0f, UnitH, 0f), new Vector3(UnitD, 0.05f, UnitW), board);
        MakePanel(unit.transform, "Base", new Vector3(0f, 0.05f, 0f), new Vector3(UnitD, 0.1f, UnitW), board);
        foreach (float y in ShelfY)
            MakePanel(unit.transform, "Shelf_" + y, new Vector3(0f, y, 0f), new Vector3(UnitD - 0.06f, 0.03f, UnitW - 0.08f), board);
        return unit;
    }

    static void MakePanel(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat)
    {
        var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
        p.name = name;
        p.transform.SetParent(parent, false);
        p.transform.localPosition = localPos;
        p.transform.localScale = size;
        p.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static bool StockRow(RawReagentCatalog.Row row, Vector3 pos, Transform parent,
                         SceneAssetLibrary lib, ReactionRegistry registry, ExperimentRunner runner)
    {
        switch (row.labware)
        {
            case RawReagentCatalog.LabwareKind.SmallBox: return StockBox(row, pos, parent);
            case RawReagentCatalog.LabwareKind.IceBucket: return StockBucket(row, pos, parent);
        }

        string prefabName = PrefabFor(row.labware);
        var prefab = lib.GetPrefab(prefabName);
        if (prefab == null) { Debug.LogWarning("[ReagentCabinets] missing prefab " + prefabName); return false; }
        var chem = lib.GetChemical(row.chemicalName);
        if (chem == null) { Debug.LogWarning("[ReagentCabinets] chemical not in library: " + row.chemicalName + " — run Generate Raw Reagent Data first."); return false; }

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        inst.name = "Raw_" + row.chemicalName.Replace(" ", "");
        // Bottle-sized: normalise the tallest axis to ~0.17 m (jars 0.12).
        float targetH = row.labware == RawReagentCatalog.LabwareKind.PowderJar ? 0.12f : 0.17f;
        var rends = inst.GetComponentsInChildren<Renderer>(true);
        if (rends.Length > 0)
        {
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            if (b.size.y > 0.001f) inst.transform.localScale *= targetH / b.size.y;
        }
        inst.transform.position = pos;

        var lp = inst.GetComponent<LiquidPhysics>();
        if (lp == null) lp = inst.AddComponent<LiquidPhysics>();
        lp.registry = registry;
        lp.currentChemical = chem;
        lp.currentLiquidVolume = 150f;
        TintLiquid(inst, chem);   // bake the fill colour (the runtime lerp is play-mode only)

        var rb = PhysicsProfiles.EnsurePhysics(inst, prefabName);
        var grab = inst.GetComponent<XRGrab>();
        GrabTuning.Apply(grab);
        if (inst.GetComponent<GrabPhysicsPolicy>() == null) inst.AddComponent<GrabPhysicsPolicy>();
        var respawn = inst.GetComponent<DropRespawn>();
        if (respawn == null) respawn = inst.AddComponent<DropRespawn>();
        respawn.SetHome(inst.transform.position, inst.transform.rotation);
        ShelfPourWiring.WireBottle(inst.gameObject, runner, registry);
        var label = inst.GetComponent<ProximityLabel>();
        if (label == null) label = inst.AddComponent<ProximityLabel>();
        label.SetLabel(row.chemicalName, 1.4f);
        return true;
    }

    /// Chemical → the Tripo prefab the user generates from the reference images
    /// in Art/Generated/Refs (same convention as the ice bucket): once the model
    /// exists, a rebuild swaps it in automatically.
    static string TripoNameFor(string chemicalName)
    {
        switch (chemicalName)
        {
            case "Matchsticks": return "Matchbox";
            case "Filter Paper": return "FilterPaper";
            case "Cotton Swabs": return "CottonSwabs";
            case "Litmus Paper": return "LitmusBox";
            default: return null;
        }
    }

    static GameObject TryTripo(string name, Vector3 pos, Transform parent, float targetH)
    {
        if (string.IsNullOrEmpty(name)) return null;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PharmaSynth/Art/Generated/Refs/" + name + ".prefab");
        if (prefab == null) return null;
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        var rends = inst.GetComponentsInChildren<Renderer>(true);
        if (rends.Length > 0)
        {
            var b = CombinedBounds(rends);
            if (b.size.y > 0.001f) inst.transform.localScale *= targetH / b.size.y;
            inst.transform.position = pos;
            b = CombinedBounds(inst.GetComponentsInChildren<Renderer>(true));
            inst.transform.position = new Vector3(pos.x, pos.y + (pos.y - b.min.y) + 0.005f, pos.z);
        }
        else inst.transform.position = pos;
        if (inst.GetComponentInChildren<Collider>() == null)
        {
            var col = inst.AddComponent<BoxCollider>();
            col.size = new Vector3(targetH * 1.6f, targetH, targetH * 1.2f);
            col.center = new Vector3(0f, targetH * 0.4f, 0f);
        }
        return inst;
    }

    static bool StockBox(RawReagentCatalog.Row row, Vector3 pos, Transform parent)
    {
        // The user's Tripo model wins when it exists; the cardboard cube is the fallback.
        var tripo = TryTripo(TripoNameFor(row.chemicalName), pos, parent, 0.06f);
        GameObject box;
        if (tripo != null)
        {
            box = tripo;
            box.name = "Raw_" + row.chemicalName.Replace(" ", "");
        }
        else
        {
            box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Raw_" + row.chemicalName.Replace(" ", "");
            box.transform.SetParent(parent, false);
            box.transform.position = pos + new Vector3(0f, 0.03f, 0f);
            box.transform.localScale = new Vector3(0.12f, 0.06f, 0.085f);
            // AI-generated kraft cardboard (falls back to the row's flat colour).
            box.GetComponent<Renderer>().sharedMaterial = Cardboard != null ? Cardboard : MakeMat(row.color);
        }
        AddPropPhysics(box, 0.05f);
        var label = box.AddComponent<ProximityLabel>();
        label.SetLabel(row.chemicalName, 1.4f);
        var item = box.AddComponent<LabItem>();
        item.itemId = "raw-" + LabInfoDatabase.Norm(row.chemicalName);
        item.displayName = row.chemicalName;

        // The interactive consumables ride ON the box, ready to grab.
        if (row.chemicalName == "Litmus Paper")
            for (int i = 0; i < 3; i++) MakeLitmusStrip(parent, pos + new Vector3(0f, 0.06f, -0.02f + i * 0.02f));
        else if (row.chemicalName == "Matchsticks")
            for (int i = 0; i < 3; i++) MakeMatchstick(parent, pos + new Vector3(0f, 0.062f, -0.02f + i * 0.02f));
        return true;
    }

    static bool StockBucket(RawReagentCatalog.Row row, Vector3 pos, Transform parent)
    {
        GameObject bucket;
        // The user's Tripo P1 ice-bucket model (generated in the AI panel,
        // 2026-07-11) — the procedural cylinder is only the fallback.
        var tripo = AssetDatabase.LoadAssetAtPath<GameObject>(TripoBucketPath);
        if (tripo != null)
        {
            bucket = (GameObject)PrefabUtility.InstantiatePrefab(tripo, parent);
            bucket.name = "Raw_IceBucket";
            var rends = bucket.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                var b = CombinedBounds(rends);
                if (b.size.y > 0.001f) bucket.transform.localScale *= 0.16f / b.size.y;
                // Tripo pivots at the model centre — seat the mesh base on the shelf.
                bucket.transform.position = pos;
                b = CombinedBounds(bucket.GetComponentsInChildren<Renderer>(true));
                bucket.transform.position = new Vector3(pos.x, pos.y + (pos.y - b.min.y) + 0.005f, pos.z);
            }
            else bucket.transform.position = pos + new Vector3(0f, 0.08f, 0f);
        }
        else
        {
            bucket = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bucket.name = "Raw_IceBucket";
            bucket.transform.SetParent(parent, false);
            bucket.transform.position = pos + new Vector3(0f, 0.06f, 0f);
            bucket.transform.localScale = new Vector3(0.16f, 0.06f, 0.16f);
            // AI-generated brushed steel (falls back to flat metallic grey).
            bucket.GetComponent<Renderer>().sharedMaterial = Steel != null ? Steel : MakeMat(new Color(0.75f, 0.82f, 0.88f));
            for (int i = 0; i < 4; i++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Ice_" + i;
                cube.transform.SetParent(bucket.transform, false);
                cube.transform.localPosition = new Vector3((i % 2 - 0.5f) * 0.35f, 0.9f, (i / 2 - 0.5f) * 0.35f);
                cube.transform.localScale = new Vector3(0.22f, 0.5f, 0.22f);
                cube.GetComponent<Renderer>().sharedMaterial = MakeMat(new Color(0.9f, 0.96f, 1f, 0.9f));
                Object.DestroyImmediate(cube.GetComponent<Collider>());
            }
        }
        if (bucket.GetComponentInChildren<Collider>() == null)
        {
            var col = bucket.AddComponent<BoxCollider>();
            col.size = new Vector3(0.16f, 0.14f, 0.16f);
            col.center = new Vector3(0f, 0.02f, 0f);
        }
        AddPropPhysics(bucket, 0.6f);
        var label = bucket.GetComponent<ProximityLabel>();
        if (label == null) label = bucket.AddComponent<ProximityLabel>();
        label.SetLabel("Ice Bath", 1.4f);
        var item = bucket.GetComponent<LabItem>();
        if (item == null) item = bucket.AddComponent<LabItem>();
        item.itemId = "raw-ice";
        item.displayName = "Ice";
        return true;
    }

    /// Bake the chemical's colour into the vessel so it reads in the editor and
    /// on load — the LiquidPhysics colour lerp is a play-mode coroutine and never
    /// fires for pre-placed bottles. Two channels:
    ///   • the Liquid mesh gets the fill colour (visible through GLASS vessels);
    ///   • the lid/cap gets a STRONG tint — the vial bodies are opaque, so the
    ///     cap is the visible colour code (standard lab practice).
    static void TintLiquid(GameObject bottle, ChemicalData chem)
    {
        if (chem == null) return;
        Color c = chem.liquidColor; c.a = 1f;
        foreach (var r in bottle.GetComponentsInChildren<Renderer>(true))
        {
            string n = r.name.ToLowerInvariant();
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            if (n.Contains("liquid"))
            {
                mpb.SetColor("_LiquidColour", c);
                if (chem.state == PhysicalState.Powder || chem.state == PhysicalState.Solid)
                    mpb.SetFloat("_WobbleX", 0f);   // solids don't slosh
                r.SetPropertyBlock(mpb);
            }
            else if (n.Contains("lid") || n.Contains("cap"))
            {
                // Near-white chemicals get a neutral grey cap instead of "white =
                // uncoloured" (keeps the code readable across the shelf).
                Color capC = (c.r > 0.88f && c.g > 0.88f && c.b > 0.85f)
                    ? new Color(0.45f, 0.5f, 0.55f) : c;
                mpb.SetColor("_BaseColor", capC);
                mpb.SetColor("_Color", capC);
                r.SetPropertyBlock(mpb);
            }
        }
    }

    static void MakeLitmusStrip(Transform parent, Vector3 pos)
    {
        var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        strip.name = "LitmusStrip";
        strip.transform.SetParent(parent, false);
        strip.transform.position = pos;
        strip.transform.localScale = new Vector3(0.012f, 0.002f, 0.055f);
        strip.GetComponent<Renderer>().sharedMaterial = MakeMat(LitmusMath.NeutralViolet);
        AddPropPhysics(strip, 0.003f);
        strip.AddComponent<LitmusStrip>().Bind(strip.GetComponent<Renderer>());
        var item = strip.AddComponent<LabItem>();
        item.itemId = "litmus-strip"; item.displayName = "Litmus Paper";
    }

    static void MakeMatchstick(Transform parent, Vector3 pos)
    {
        var stick = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stick.name = "Matchstick";
        stick.transform.SetParent(parent, false);
        stick.transform.position = pos;
        stick.transform.localScale = new Vector3(0.005f, 0.005f, 0.07f);
        stick.GetComponent<Renderer>().sharedMaterial = MakeMat(new Color(0.75f, 0.55f, 0.3f));
        var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "Head";
        head.transform.SetParent(stick.transform, false);
        head.transform.localPosition = new Vector3(0f, 0f, 0.48f);
        head.transform.localScale = new Vector3(1.6f, 1.6f, 0.12f);
        head.GetComponent<Renderer>().sharedMaterial = MakeMat(new Color(0.6f, 0.12f, 0.1f));
        Object.DestroyImmediate(head.GetComponent<Collider>());
        AddPropPhysics(stick, 0.004f);
        stick.AddComponent<Matchstick>();
        var item = stick.AddComponent<LabItem>();
        item.itemId = "matchstick"; item.displayName = "Matchstick";
    }

    /// Grabbable-prop physics for the procedural consumables (kinematic-parked,
    /// dynamic on release — the shelf policy).
    static void AddPropPhysics(GameObject go, float mass)
    {
        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.isKinematic = true;
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        var grab = go.GetComponent<XRGrab>();
        if (grab == null) grab = go.AddComponent<XRGrab>();
        GrabTuning.Apply(grab);
        if (go.GetComponent<GrabPhysicsPolicy>() == null) go.AddComponent<GrabPhysicsPolicy>();
        var respawn = go.GetComponent<DropRespawn>();
        if (respawn == null) respawn = go.AddComponent<DropRespawn>();
        respawn.SetHome(go.transform.position, go.transform.rotation);
    }

    static Material MakeMat(Color c)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.color = c;
        return m;
    }

    static Bounds CombinedBounds(Renderer[] rends)
    {
        var b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b;
    }
}
#endif
