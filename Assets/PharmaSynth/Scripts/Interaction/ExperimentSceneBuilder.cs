using System.Collections.Generic;
using UnityEngine;
using TMPro;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;
using XRSocket = UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor;
using TeleAnchor = UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationAnchor;
using TeleArea = UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea;

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
        SpawnDemoKit(stage, moduleId);              // demo sessions get the ready-made product
        if (methane) return 0;                      // Methane uses its hand-built stage

        var layout = FindLayout(moduleId);
        if (layout == null) { Debug.LogWarning("[SceneBuilder] no layout for " + moduleId); return 0; }

        int n = 0;
        foreach (var s in layout.stations) { BuildStation(stage, s); n++; }
        foreach (var p in layout.props)    { BuildProp(stage, p); n++; }
        foreach (var v in layout.vessels)  { BuildVessel(stage, v); n++; }
        return n;
    }

    /// Demo sessions (user 2026-07-10): a ready-made vial of the module's end
    /// product spawns on the raw-reagent cabinets' demo shelf, so panelists can
    /// run the tests without performing the synthesis.
    private void SpawnDemoKit(Transform stage, string moduleId)
    {
        if (!Application.isPlaying || !DemoSession.Active || assets == null) return;
        string product = DemoMode.ProductFor(moduleId);
        var chem = product != null ? assets.GetChemical(product) : null;
        var prefab = assets.GetPrefab("TestTube_WithLiquid");
        if (chem == null || prefab == null) return;

        var anchor = GameObject.Find("ReagentCabinets");
        Vector3 pos = anchor != null
            ? anchor.transform.position + new Vector3(-0.35f, 1.0f, 0f)
            : new Vector3(0f, 1.0f, -2.6f);
        var inst = Instantiate(prefab, stage);
        inst.name = "DemoKit_" + product.Replace(" ", "");
        Normalise(inst, "TestTube_WithLiquid", 0.15f);
        Seat(inst.transform, pos);
        var lp = inst.GetComponent<LiquidPhysics>() ?? inst.AddComponent<LiquidPhysics>();
        lp.registry = registry;
        lp.currentChemical = chem;
        lp.currentLiquidVolume = 40f;
        PhysicsProfiles.EnsurePhysics(inst, "TestTube_WithLiquid");
        GrabTuning.Apply(inst.GetComponent<XRGrab>());
        inst.AddComponent<GrabPhysicsPolicy>();
        var respawn = inst.AddComponent<DropRespawn>();
        respawn.SetHome(inst.transform.position, inst.transform.rotation);
        var pl = inst.AddComponent<ProximityLabel>();
        pl.SetLabel("Ready-made: " + product + " (demo)", 1.6f);
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
            var loop = pad.AddComponent<SimLoopAudio>();
            loop.Bind(SimLoopAudio.KeyFor(s.sim));
            rig.SetLoopAudio(loop);
            var vfx = pad.AddComponent<StationVfx>();   // steam/frost/drip/bubbles while occupied
            vfx.Bind(s.sim);
            rig.SetVfx(vfx);

            // Overheat consequence (error-effects pass): smoke + ruined batch +
            // alarm + Overheat mistake when the sim crosses its threshold.
            if (s.sim == StationSim.Heat && temp != null)
            {
                var overheat = pad.AddComponent<OverheatEffects>();
                overheat.Bind(temp, runner, assets != null ? assets.GetChemical("Ruined Mixture") : null);
            }

            // Hot-surface hazard (§1): touching a HEAT station once it is
            // actually hot records a handling mistake. Player-only (props
            // placed on the pad never trigger it) and armed above 50 °C.
            if (s.sim == StationSim.Heat && temp != null)
            {
                var hot = new GameObject("HotSurface_" + s.taskId);
                hot.transform.SetParent(stage, false);
                hot.transform.position = new Vector3(s.pos.x, s.pos.y + 0.06f, s.pos.z);
                var hotCol = hot.AddComponent<SphereCollider>();
                hotCol.isTrigger = true; hotCol.radius = 0.16f;
                var hz = hot.AddComponent<HazardZone>();
                hz.Configure(runner, LabErrorType.HazardousAction, "Hot surface — don't touch heated apparatus!");
                var tempRef = temp;
                hz.SetArmedCheck(() => tempRef != null && tempRef.AtLeast(50f));
                var cam = Camera.main;
                if (cam != null) hz.SetPlayerRoot(cam.transform.root);
            }
        }

        // Teleport anchor: a floor pad in front of the station so thumbstick
        // teleporters land at each workstation (§2 — only the room-wide floor
        // area existed). Mirrors the existing area's layers/provider.
        var anchorGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        anchorGo.name = "TeleAnchor_" + s.taskId;
        anchorGo.transform.SetParent(stage, false);
        Vector3 toward = new Vector3(0.2f, 0f, -2.5f) - new Vector3(s.pos.x, 0f, s.pos.z);
        toward.y = 0f;
        toward = toward.sqrMagnitude > 0.01f ? toward.normalized : Vector3.right;
        anchorGo.transform.position = new Vector3(s.pos.x, 0.01f, s.pos.z) + toward * 0.75f;
        anchorGo.transform.localScale = new Vector3(0.5f, 0.008f, 0.5f);
        var anchor = anchorGo.AddComponent<TeleAnchor>();
        anchor.teleportAnchorTransform = anchorGo.transform;
        var floorArea = FindAnyObjectByType<TeleArea>();
        if (floorArea != null)
        {
            anchor.interactionLayers = floorArea.interactionLayers;
            anchor.teleportationProvider = floorArea.teleportationProvider;
        }

        // Snap socket: releasing the required prop near the pad clicks it into
        // place (parented to the stage — the pad's non-uniform scale would
        // distort a child). Wrong items are rejected by the filter.
        var sockGo = new GameObject("Socket_" + s.taskId);
        sockGo.transform.SetParent(stage, false);
        sockGo.transform.position = new Vector3(s.pos.x, s.pos.y + 0.02f, s.pos.z);
        var sockCol = sockGo.AddComponent<SphereCollider>();
        sockCol.isTrigger = true; sockCol.radius = 0.10f;
        var filter = sockGo.AddComponent<StationSocketFilter>();
        filter.requiredItemId = s.requiredItemId;
        var sock = sockGo.AddComponent<XRSocket>();
        sock.selectFilters.Add(filter);
        sock.attachTransform = sockGo.transform;
        // Ghost preview: bringing the correct item near shows where it snaps.
        sock.showInteractableHoverMeshes = true;
        sock.interactableHoverMeshMaterial = SocketGhostMaterial();
        sockGo.AddComponent<SelectSfx>().Bind(sock, "socket-snap");

        MakeLabel(s.label, new Vector3(s.pos.x, s.pos.y + 0.32f, s.pos.z), 0.13f);
    }

    private static Material _socketGhostMat;
    /// Shared translucent-cyan material for socket hover-mesh previews (built once).
    private static Material SocketGhostMaterial()
    {
        if (_socketGhostMat != null) return _socketGhostMat;
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        var m = new Material(sh) { name = "SocketGhost" };
        var c = new Color(0.5f, 0.9f, 1f, 0.3f);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        m.SetOverrideTag("RenderType", "Transparent");
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        _socketGhostMat = m;
        return m;
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
        var rb = PhysicsProfiles.EnsurePhysics(inst, p.prefabName);
        inst.AddComponent<GrabPhysicsPolicy>();
        GrabTuning.Apply(inst.GetComponent<XRGrab>());   // held items collide with the world
        inst.AddComponent<HoverHighlight>().Bind(inst.GetComponent<XRGrab>());   // hover affordance
        var respawn = inst.AddComponent<DropRespawn>();
        respawn.SetHome(inst.transform.position, inst.transform.rotation);
        if (Mishandling.IsBreakable(p.prefabName))
        {
            var breakable = inst.AddComponent<BreakableGlassware>();
            breakable.Bind(runner, respawn, rb, p.displayName);
            inst.AddComponent<ImpactSound>().Bind(rb, Mishandling.DropSoundKey(p.prefabName), Mishandling.DefaultBreakSpeed);
        }
        else
        {
            inst.AddComponent<ImpactSound>().Bind(rb, Mishandling.DropSoundKey(p.prefabName));
        }
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
            var spill = inst.AddComponent<SpillMistake>();
            spill.Bind(runner, lp, inst.GetComponent<XRGrab>(), p.displayName);
            inst.AddComponent<HazardousMixReactor>().Bind(lp, runner);   // bad-mix consequences
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
        GrabTuning.Apply(inst.GetComponent<XRGrab>());       // collide while held; re-freezes on release
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
        inst.AddComponent<HazardousMixReactor>().Bind(lp, runner);   // bad-mix consequences
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
