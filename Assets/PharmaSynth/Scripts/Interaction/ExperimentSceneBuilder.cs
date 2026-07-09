using System.Collections.Generic;
using UnityEngine;
using TMPro;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// Spawns an experiment's physical setup (stations, grabbable props, reagent vessels)
/// from its ExperimentLayout when a module loads — so all 11 experiments live in one
/// lab scene. The hand-built Methane objects stay as a grouped stage that is simply
/// toggled; every other experiment is built into a DynamicStage that is cleared and
/// rebuilt on each module change.
public class ExperimentSceneBuilder : MonoBehaviour
{
    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private SceneAssetLibrary assets;
    [SerializeField] private ReactionRegistry registry;
    [SerializeField] private List<ExperimentLayout> layouts = new List<ExperimentLayout>();
    [SerializeField] private GameObject methaneStage;   // existing hand-built Methane objects
    [SerializeField] private Transform labelsRoot;      // WorldLabels
    [SerializeField] private string methaneModuleId = "tutorial-methane";

    private Transform _stage;

    public void SetRefs(ExperimentRunner r, SceneAssetLibrary a, ReactionRegistry reg, List<ExperimentLayout> ls)
    { runner = r; assets = a; registry = reg; if (ls != null) layouts = ls; }

    public ExperimentLayout FindLayout(string moduleId)
    {
        foreach (var l in layouts) if (l != null && l.moduleId == moduleId) return l;
        return null;
    }

    /// Hook for ExperimentLauncher.onModuleLoaded.
    public void OnModuleLoaded(ExperimentModuleDefinition m) { if (m != null) Build(m.moduleId); }

    private Transform Stage()
    {
        if (_stage == null)
        {
            var go = new GameObject("DynamicStage");
            go.transform.SetParent(transform, false);
            _stage = go.transform;
        }
        return _stage;
    }

    private static void Kill(GameObject go)
    {
        if (Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go);
    }

    /// Build the setup for a module. Returns the number of spawned root objects.
    /// Public + returns count so edit-mode self-tests can verify it.
    public int Build(string moduleId)
    {
        var stage = Stage();
        for (int i = stage.childCount - 1; i >= 0; i--) Kill(stage.GetChild(i).gameObject);
        // Clear the dynamic labels we spawned last time.
        if (labelsRoot != null)
        {
            var dead = new List<GameObject>();
            foreach (Transform t in labelsRoot) if (t.name.StartsWith("DynLabel_")) dead.Add(t.gameObject);
            foreach (var d in dead) Kill(d);
        }

        bool methane = moduleId == methaneModuleId;
        if (methaneStage != null) methaneStage.SetActive(methane);
        if (methane) return 0;                      // Methane uses its hand-built stage

        var layout = FindLayout(moduleId);
        if (layout == null) { Debug.LogWarning("[SceneBuilder] no layout for " + moduleId); return 0; }

        int n = 0;
        foreach (var s in layout.stations) { BuildStation(stage, s); n++; }
        foreach (var p in layout.props)    { BuildProp(stage, p); n++; }
        foreach (var v in layout.vessels)  { BuildVessel(stage, v); n++; }
        return n;
    }

    // ---- builders ---------------------------------------------------------

    private void BuildStation(Transform stage, ExperimentLayout.Station s)
    {
        var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = "Station_" + s.taskId;
        pad.transform.SetParent(stage, false);
        pad.transform.position = new Vector3(s.pos.x, s.pos.y + 0.006f, s.pos.z);
        pad.transform.localScale = new Vector3(0.3f, 0.012f, 0.3f);
        var box = pad.GetComponent<BoxCollider>();
        box.isTrigger = true; box.size = new Vector3(1.2f, 40f, 1.2f); box.center = new Vector3(0f, 20f, 0f);

        bool simDriven = s.sim != StationSim.None;
        var st = pad.AddComponent<ExperimentTaskStation>();
        // Sim stations complete via the sustained verb's auto-check, not on zone-touch.
        st.Configure(runner, s.taskId, s.requiredItemId, !simDriven, false);

        if (simDriven)
        {
            var sensor = pad.AddComponent<ZoneItemSensor>();
            sensor.SetItemId(s.requiredItemId);
            TemperatureSim temp = null; CrystallizationController cryst = null;
            FiltrationController filt = null; GasCollection gas = null;
            switch (s.sim)
            {
                case StationSim.Heat:       temp  = pad.AddComponent<TemperatureSim>(); break;
                case StationSim.Crystallise: cryst = pad.AddComponent<CrystallizationController>(); break;
                case StationSim.Filter:     filt  = pad.AddComponent<FiltrationController>(); break;
                case StationSim.Collect:    gas   = pad.AddComponent<GasCollection>(); break;
            }
            var rig = pad.AddComponent<ZoneSimStation>();
            rig.Bind(runner, s.taskId, s.sim, sensor, temp, cryst, filt, gas, s.simTargetC);
        }

        MakeLabel(s.label, new Vector3(s.pos.x, s.pos.y + 0.32f, s.pos.z), 0.13f);
    }

    private void BuildProp(Transform stage, ExperimentLayout.Prop p)
    {
        var prefab = assets != null ? assets.GetPrefab(p.prefabName) : null;
        if (prefab == null) { Debug.LogWarning("[SceneBuilder] missing prefab " + p.prefabName); return; }
        var inst = Instantiate(prefab, stage);
        inst.name = "Prop_" + p.itemId;
        Normalise(inst, p.prefabName, p.targetHeight);
        RestPoseFor(inst, p.prefabName);
        Seat(inst.transform, p.pos);
        var item = inst.GetComponent<LabItem>() ?? inst.AddComponent<LabItem>();
        item.itemId = p.itemId; item.displayName = p.displayName;
        PhysicsProfiles.EnsurePhysics(inst, p.prefabName);
        inst.AddComponent<GrabPhysicsPolicy>();
        var respawn = inst.AddComponent<DropRespawn>();
        respawn.SetHome(inst.transform.position, inst.transform.rotation);
        if (p.pourable)
        {
            var lp = inst.GetComponent<LiquidPhysics>() ?? inst.AddComponent<LiquidPhysics>();
            lp.registry = registry;
            var chem = assets.GetChemical(p.fillChemical);
            if (chem != null)
            {
                lp.currentChemical = chem;
                lp.currentLiquidVolume = SupplyFor(p, chem);   // finite: ~need + 2 spare pours
            }
            var pourer = inst.GetComponent<LiquidPourer>() ?? inst.AddComponent<LiquidPourer>();
            if (pourer.spout == null)
            {
                var spout = new GameObject("Spout").transform;
                spout.SetParent(inst.transform, false);
                spout.localPosition = new Vector3(0f, 0.12f, 0f);
                pourer.spout = spout;
            }
        }
        var pl = inst.AddComponent<ProximityLabel>(); pl.SetLabel(p.displayName, 1.6f);
    }

    private void BuildVessel(Transform stage, ExperimentLayout.Vessel v)
    {
        var prefab = assets != null ? assets.GetPrefab(v.prefabName) : null;
        if (prefab == null) { Debug.LogWarning("[SceneBuilder] missing vessel prefab " + v.prefabName); return; }
        var inst = Instantiate(prefab, stage);
        inst.name = "Vessel_" + v.prefabName;
        Normalise(inst, v.prefabName, v.targetHeight);
        Seat(inst.transform, v.pos);
        PhysicsProfiles.EnsurePhysics(inst, v.prefabName);   // vessels stay kinematic (no release policy)
        var lp = inst.GetComponent<LiquidPhysics>() ?? inst.AddComponent<LiquidPhysics>();
        lp.registry = registry;
        if (!string.IsNullOrEmpty(v.startChemical))
        {
            var chem = assets.GetChemical(v.startChemical);
            if (chem != null) { lp.currentChemical = chem; lp.currentLiquidVolume = lp.maxVolume * 0.3f; }
        }
        var bind = inst.AddComponent<LiquidTaskBinding>();
        bind.SetVesselAndRunner(lp, runner);
        foreach (var b in v.bindings)
        {
            var reagent = assets.GetChemical(b.reagentChemical);
            if (reagent != null) bind.AddExpected(reagent, b.taskId, b.requiredMl);
        }
        var pl = inst.AddComponent<ProximityLabel>(); pl.SetLabel(v.displayName, 1.6f);
    }

    // ---- helpers ----------------------------------------------------------

    /// Finite bottle supply: the authored supplyMl, or 2.5x the summed requiredMl of
    /// every binding this chemical feeds (min 120 ml). A chemical no binding consumes
    /// keeps the legacy 60% fill (display/test reagents).
    private float SupplyFor(ExperimentLayout.Prop p, ChemicalData chem)
    {
        if (p.supplyMl > 0f) return p.supplyMl;
        float needed = 0f;
        var layout = FindLayout(runner != null && runner.Module != null ? runner.Module.moduleId : null);
        if (layout != null)
            foreach (var v in layout.vessels)
                foreach (var b in v.bindings)
                    if (b.reagentChemical == p.fillChemical && b.requiredMl > 0f) needed += b.requiredMl;
        return needed > 0f ? Mathf.Max(120f, needed * 2.5f) : 600f;
    }

    private static Bounds WB(GameObject g)
    {
        var rs = g.GetComponentsInChildren<Renderer>();
        Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(g.transform.position, Vector3.one * 0.1f);
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    private static void Normalise(GameObject g, float targetHeight)
        => Normalise(g, null, targetHeight);

    /// RealSizes-aware normalisation: known prefabs scale by their realistic
    /// LONGEST dimension (bounds-HEIGHT normalisation inflated flat tools 3-16x);
    /// unknown names keep the legacy height behaviour.
    private static void Normalise(GameObject g, string prefabName, float fallbackHeight)
    {
        g.transform.localScale = Vector3.one;
        var size = WB(g).size;
        if (RealSizes.TryGet(prefabName, out float target))
            g.transform.localScale = Vector3.one * RealSizes.UniformScaleFactor(size, target);
        else
            g.transform.localScale = Vector3.one * (fallbackHeight / Mathf.Max(size.y, 0.01f));
    }

    /// Rotate a spawned item into its plausible resting pose (a spatula lies
    /// flat, a glass rod on its side) BEFORE seating, so bounds-based seating
    /// uses the rotated footprint.
    private static void RestPoseFor(GameObject g, string prefabName)
    {
        if (!PhysicsProfiles.TryGet(prefabName, out var prof)) return;
        g.transform.rotation = PhysicsProfiles.RestRotation(prof.pose, WB(g).size) * g.transform.rotation;
    }

    private static void Seat(Transform t, Vector3 pos)
    {
        float surfaceY = pos.y;
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(pos.x, pos.y + 0.5f, pos.z), Vector3.down, out hit, 1.0f, ~0, QueryTriggerInteraction.Ignore))
            surfaceY = hit.point.y;
        t.position = new Vector3(pos.x, surfaceY + 0.3f, pos.z);
        t.position += Vector3.up * (surfaceY + 0.005f - WB(t.gameObject).min.y);
    }

    private void MakeLabel(string text, Vector3 pos, float scale)
    {
        if (labelsRoot == null) return;
        var go = new GameObject("DynLabel_" + text);
        go.transform.SetParent(labelsRoot, false);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * scale;
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text; tmp.fontSize = 6f; tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white; tmp.fontStyle = FontStyles.Bold;
        tmp.outlineWidth = 0.25f; tmp.outlineColor = new Color32(6, 12, 22, 255);
        go.AddComponent<FaceCamera>();
    }
}
