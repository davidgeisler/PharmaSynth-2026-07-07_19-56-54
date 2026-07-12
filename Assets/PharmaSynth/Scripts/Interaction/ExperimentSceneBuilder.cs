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

    private static void Kill(Component c)
    {
        if (Application.isPlaying) Object.Destroy(c); else Object.DestroyImmediate(c);
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
        // Demo sessions no longer spawn a floating ready-made vial here (user
        // 2026-07-12): the finished products live on the ReagentShelf and are
        // revealed there by EndProductVisibility while a demo session is active.
        if (methane) return 0;                      // Methane uses its hand-built stage

        var layout = FindLayout(moduleId);
        if (layout == null) { Debug.LogWarning("[SceneBuilder] no layout for " + moduleId); return 0; }

        int n = 0;
        _currentLayout = layout;
        foreach (var s in layout.stations) { BuildStation(stage, s); n++; }
        foreach (var p in layout.props)    { BuildProp(stage, p); n++; }
        foreach (var v in layout.vessels)  { BuildVessel(stage, v); n++; }
        WireVerbControllers(stage, layout);   // stir/grind/burner-gate (W5.8) — needs props+vessels spawned
        SpawnRackKit(stage);                  // test-tube rack, pre-filled (W5.8)
        SpawnSpares(stage);                   // spare beakers/flask (W5.8: duplicates of vital glass)
        StageConsumables(stage, layout);      // matches + striker at Heat experiments (W5.8)
        return n;
    }

    /// A fixed test-tube rack near the vessels, pre-filled with 6 grabbable
    /// tubes (full receiver treatment; each tube's DropRespawn home = its slot,
    /// so the rack refills itself when tubes are abandoned).
    private void SpawnRackKit(Transform stage)
    {
        var prefab = assets != null ? assets.GetPrefab("TestTubeRack") : null;
        if (prefab == null) return;
        var rack = Instantiate(prefab, stage);
        rack.name = "RackKit";
        Normalise(rack, "TestTubeRack", 0.18f);
        Seat(rack.transform, LayoutTidyMath.RackPos);
        var grab = rack.GetComponent<XRGrab>(); if (grab != null) Kill(grab);
        var rrb = rack.GetComponent<Rigidbody>(); if (rrb != null) Kill(rrb);

        var b = WB(rack);
        var tubePrefab = assets.GetPrefab("TestTube");
        if (tubePrefab == null) return;
        for (int i = 0; i < 6; i++)
        {
            int col = i % 3, row = i / 3;
            var pos = new Vector3(
                Mathf.Lerp(b.min.x + 0.03f, b.max.x - 0.03f, col / 2f),
                b.max.y + 0.02f,
                Mathf.Lerp(b.min.z + 0.02f, b.max.z - 0.02f, row));
            SpawnReceiver(stage, tubePrefab, "TestTube", "RackTube_" + i, "Test Tube", pos, 0.15f);
        }
    }

    /// Spare vital glassware on the free front strip (user: "enough duplicates
    /// of things that are vital like beakers").
    private void SpawnSpares(Transform stage)
    {
        if (assets == null) return;
        var specs = new (string prefab, string label)[]
        {
            ("Beaker_100mL", "Spare Beaker"),
            ("Beaker_100mL", "Spare Beaker"),
            ("ErlenmeyerFlask_400mL", "Spare Flask"),
        };
        for (int i = 0; i < specs.Length; i++)
        {
            var prefab = assets.GetPrefab(specs[i].prefab);
            if (prefab == null) continue;
            SpawnReceiver(stage, prefab, specs[i].prefab, "Spare_" + specs[i].prefab + "_" + i,
                          specs[i].label, LayoutTidyMath.SparePos(i), 0.14f);
        }
    }

    /// A grabbable empty receiver vessel with the full W5.8 treatment.
    private void SpawnReceiver(Transform stage, GameObject prefab, string prefabName, string name,
                               string label, Vector3 pos, float targetHeight)
    {
        var swap = VesselPrefabFor(prefabName);
        var inst = Instantiate(swap != null ? swap : prefab, stage);
        inst.name = name;
        Normalise(inst, prefabName, targetHeight);
        Seat(inst.transform, pos);
        var item = inst.GetComponent<LabItem>() ?? inst.AddComponent<LabItem>();
        item.itemId = name; item.displayName = label;
        var rb = PhysicsProfiles.EnsurePhysics(inst, prefabName);
        inst.AddComponent<GrabPhysicsPolicy>();
        GrabTuning.Apply(inst.GetComponent<XRGrab>());
        inst.AddComponent<HoverHighlight>().Bind(inst.GetComponent<XRGrab>());
        var respawn = inst.AddComponent<DropRespawn>();
        respawn.SetHome(inst.transform.position, inst.transform.rotation);
        if (Mishandling.IsBreakable(prefabName))
        {
            var breakable = inst.AddComponent<BreakableGlassware>();
            breakable.Bind(runner, respawn, rb, label);
            inst.AddComponent<ImpactSound>().Bind(rb, Mishandling.DropSoundKey(prefabName), Mishandling.DefaultBreakSpeed);
        }
        else inst.AddComponent<ImpactSound>().Bind(rb, Mishandling.DropSoundKey(prefabName));
        var lp = inst.GetComponent<LiquidPhysics>() ?? inst.AddComponent<LiquidPhysics>();
        lp.registry = registry;
        lp.SetContents(null, 0f);
        EnsureLiquidVisual(inst, lp);
        inst.AddComponent<HazardousMixReactor>().Bind(lp, runner);
        inst.AddComponent<CleanableVessel>().Bind(lp);   // used vessels get dirty (W5.12)
        var pl = inst.AddComponent<ProximityLabel>(); pl.SetLabel(label, 1.6f);
        inst.AddComponent<VesselStatus>().Bind(lp, pl, label, 1.6f);
        inst.AddComponent<MixFeedback>().Bind(lp);
    }

    /// Heat experiments stage two ready matchsticks + a striker block on the
    /// table (the cabinet dispenser remains the endless source). Cloned from
    /// the dispenser's hidden template when it exists; skipped silently if not.
    private void StageConsumables(Transform stage, ExperimentLayout layout)
    {
        bool hasHeat = false;
        foreach (var s in layout.stations) if (s.sim == StationSim.Heat) hasHeat = true;
        if (!hasHeat) return;

        // Striker block (always useful next to the matches).
        var striker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        striker.name = "MatchStriker";
        striker.transform.SetParent(stage, false);
        striker.transform.localScale = new Vector3(0.09f, 0.02f, 0.06f);
        Seat(striker.transform, LayoutTidyMath.StrikerPos);
        striker.AddComponent<MatchStrikerSurface>();
        var spl = striker.AddComponent<ProximityLabel>(); spl.SetLabel("Striker", 1.2f);

        GameObject template = null;
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == "Template_Raw_Matchsticks") { template = t.gameObject; break; }
        if (template == null) return;
        for (int i = 0; i < 2; i++)
        {
            var match = Instantiate(template, stage);
            match.name = "StagedMatch_" + i;
            match.SetActive(true);
            Seat(match.transform, LayoutTidyMath.MatchPos(i));
            var respawn = match.GetComponent<DropRespawn>() ?? match.AddComponent<DropRespawn>();
            respawn.Bind(match.GetComponent<Rigidbody>(), match.GetComponent<XRGrab>());
            respawn.SetHome(match.transform.position, match.transform.rotation);
        }
    }

    private ExperimentLayout _currentLayout;

    /// Post-pass (W5.8): wire the tool verbs that span a station + a prop + a
    /// vessel — the rod stirs the bindings vessel, the pestle grinds the mortar,
    /// and a Heat station whose required prop is a burner only heats while LIT.
    private void WireVerbControllers(Transform stage, ExperimentLayout layout)
    {
        foreach (var s in layout.stations)
        {
            if (s.sim == StationSim.Heat)
            {
                var prop = FindLayoutProp(layout, s.requiredItemId);
                if (prop == null || (prop.prefabName != "BunsenBurner" && prop.prefabName != "AlcoholBurner")) continue;
                var propGo = FindStageChild(stage, "Prop_" + s.requiredItemId);
                var padGo = FindStageChild(stage, "Station_" + s.taskId);
                if (propGo == null || padGo == null) continue;
                var burner = propGo.GetComponent<BurnerController>() ?? propGo.AddComponent<BurnerController>();
                var rig = padGo.GetComponent<ZoneSimStation>();
                if (rig != null) rig.SetIgnitionGate(() => burner != null && burner.IsLit);
                var status = padGo.GetComponent<StationStatusLabel>();
                if (status != null) status.SetIgnitionHint(() => burner != null && burner.IsLit);
                // The burner base doubles as a match striker.
                if (propGo.GetComponent<MatchStrikerSurface>() == null) propGo.AddComponent<MatchStrikerSurface>();
            }
            else if (s.sim == StationSim.Stir)
            {
                var rodGo = FindStageChild(stage, "Prop_" + s.requiredItemId);
                var vesselLp = FirstBindingsVessel(stage);
                if (rodGo == null || vesselLp == null) continue;
                var stir = vesselLp.GetComponent<StirController>() ?? vesselLp.gameObject.AddComponent<StirController>();
                stir.Bind(runner, s.taskId, vesselLp, rodGo.transform);
            }
            else if (s.sim == StationSim.Grind)
            {
                WireGrind(stage, s.taskId);
            }
        }

        // A staged mortar+pestle with no Grind station still grinds (educational).
        bool hasGrindStation = false;
        foreach (var s in layout.stations) if (s.sim == StationSim.Grind) hasGrindStation = true;
        if (!hasGrindStation) WireGrind(stage, null);
    }

    private void WireGrind(Transform stage, string taskId)
    {
        GameObject mortar = null, pestle = null;
        foreach (Transform t in stage)
        {
            if (t.name.StartsWith("Prop_"))
            {
                var li = t.GetComponent<LabItem>();
                string dn = li != null ? (li.displayName ?? "") : "";
                if (mortar == null && (dn.Contains("Mortar") || t.name.Contains("Motar") || dn.Contains("Motar"))) mortar = t.gameObject;
                if (pestle == null && dn.Contains("Pestle")) pestle = t.gameObject;
            }
        }
        if (mortar == null || pestle == null) return;
        var grind = mortar.GetComponent<GrindController>() ?? mortar.AddComponent<GrindController>();
        grind.Bind(runner, taskId, pestle.transform);
    }

    private static ExperimentLayout.Prop FindLayoutProp(ExperimentLayout layout, string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        foreach (var p in layout.props) if (p.itemId == itemId) return p;
        return null;
    }

    private static GameObject FindStageChild(Transform stage, string name)
    {
        foreach (Transform t in stage) if (t.name == name) return t.gameObject;
        return null;
    }

    private LiquidPhysics FirstBindingsVessel(Transform stage)
    {
        foreach (Transform t in stage)
        {
            if (!t.name.StartsWith("Vessel_")) continue;
            var bind = t.GetComponent<LiquidTaskBinding>();
            if (bind != null && bind.ExpectedSteps.Count > 0) return t.GetComponent<LiquidPhysics>();
        }
        return null;
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
        // W5.8 (user): the visible pads cluttered the table — hide the cosmetic
        // cube but keep its collider column + every sensor/sim living on it.
        // The socket ghost + billboard label remain the "place here" cues.
        var padMr = pad.GetComponent<MeshRenderer>();
        if (padMr != null) padMr.enabled = false;

        // Zone sims run a sustained chemistry sim; verb stations (Stir/Grind/
        // Weigh, W5.8) complete through their own tool controllers. Only plain
        // stations still complete on zone-touch.
        bool zoneSim = s.sim == StationSim.Heat || s.sim == StationSim.Crystallise
                    || s.sim == StationSim.Filter || s.sim == StationSim.Collect;
        var st = pad.AddComponent<ExperimentTaskStation>();
        st.Configure(runner, s.taskId, s.requiredItemId, s.sim == StationSim.None, false);
        if (s.sim == StationSim.Weigh) BuildWeighStation(stage, s);

        TemperatureSim temp = null; CrystallizationController cryst = null;
        FiltrationController filt = null; GasCollection gas = null;
        if (zoneSim)
        {
            var sensor = pad.AddComponent<ZoneItemSensor>();
            sensor.SetItemId(s.requiredItemId);
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

        var labelTmp = MakeLabel(s.label, new Vector3(s.pos.x, s.pos.y + 0.32f, s.pos.z), 0.13f);
        // Live status on the billboard (W5.8): Heat shows the temperature climb,
        // the other sims show percent progress — the player can SEE the verb work.
        if (zoneSim && labelTmp != null)
            pad.AddComponent<StationStatusLabel>().Bind(labelTmp, s.label, s.sim, temp, cryst, filt, gas, s.simTargetC);
    }

    /// A functional balance fixture at a Weigh station (W5.8): the Balance model
    /// (fixed, not grabbable), a live grams display, and the WeighStation whose
    /// TaskGraph condition completes the weigh task. The required measure comes
    /// from the layout's vessel binding for the same task (chemical mode); a
    /// station with no such binding falls back to its requiredItemId (tool mode).
    private void BuildWeighStation(Transform stage, ExperimentLayout.Station s)
    {
        GameObject balance;
        var prefab = assets != null ? assets.GetPrefab("Balance") : null;
        if (prefab != null)
        {
            balance = Instantiate(prefab, stage);
            Normalise(balance, "Balance", 0.18f);
        }
        else
        {
            balance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            balance.transform.SetParent(stage, false);
            balance.transform.localScale = new Vector3(0.22f, 0.05f, 0.18f);
        }
        balance.name = "Weigh_" + s.taskId;
        Seat(balance.transform, s.pos);
        // Fixed fixture: collider yes, grab/physics no (the pan must stay put).
        var grab = balance.GetComponent<XRGrab>();
        if (grab != null) Kill(grab);
        var brb = balance.GetComponent<Rigidbody>();
        if (brb != null) Kill(brb);
        if (balance.GetComponentInChildren<Collider>() == null)
        {
            var bc = balance.AddComponent<BoxCollider>();
            bc.size = new Vector3(0.22f, 0.06f, 0.18f);
        }

        // Pan trigger just above the balance top.
        var b = WB(balance);
        var pan = new GameObject("Pan_" + s.taskId);
        pan.transform.SetParent(balance.transform, true);
        pan.transform.position = new Vector3(b.center.x, b.max.y + 0.05f, b.center.z);
        var panCol = pan.AddComponent<BoxCollider>();
        panCol.isTrigger = true;
        panCol.size = new Vector3(0.24f, 0.16f, 0.2f);

        // Grams display above the pan.
        var display = MakeLabel("0.00 g", new Vector3(b.center.x, b.max.y + 0.22f, b.center.z), 0.1f);
        var scale = balance.AddComponent<WeighingScaleController>();
        if (display != null) scale.Bind(display);

        // The required measure: the vessel binding that feeds this task.
        string chem = null; float ml = 0f;
        if (_currentLayout != null)
            foreach (var v in _currentLayout.vessels)
                foreach (var bind in v.bindings)
                    if (bind.taskId == s.taskId) { chem = bind.reagentChemical; ml = bind.requiredMl; }

        var ws = pan.AddComponent<WeighStation>();
        ws.Bind(runner, s.taskId, s.requiredItemId, chem, ml, scale);
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
        // Scooping tools transfer solid reagents by the scoopful (W5.12).
        if (p.prefabName == "Scoopula" || p.prefabName == "Spatula")
            inst.AddComponent<ScoopController>().Bind(inst.GetComponent<XRGrab>());
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
                lp.SetContents(chem, SupplyFor(p, chem));   // finite: ~need + 2 spare pours
            EnsureLiquidVisual(inst, lp);                    // visible fill (W5.8)
            var pourer = inst.GetComponent<LiquidPourer>() ?? inst.AddComponent<LiquidPourer>();
            pourer.Bind(lp);
            if (pourer.spout == null)
            {
                // Mouth at the TOP of the item's real bounds — the old guessed
                // (0, 0.12, 0) sat mid-body on tall bottles and above short vials.
                var b = WB(inst);
                var spout = new GameObject("Spout").transform;
                spout.SetParent(inst.transform, true);
                spout.position = new Vector3(b.center.x, b.max.y - 0.005f, b.center.z);
                pourer.spout = spout;
            }
            var spill = inst.AddComponent<SpillMistake>();
            spill.Bind(runner, lp, inst.GetComponent<XRGrab>(), p.displayName);
            inst.AddComponent<HazardousMixReactor>().Bind(lp, runner);   // bad-mix consequences
        }
        var pl = inst.AddComponent<ProximityLabel>(); pl.SetLabel(p.displayName, 1.6f);
        if (p.pourable)
        {
            var plp = inst.GetComponent<LiquidPhysics>();
            if (plp != null)
            {
                inst.AddComponent<VesselStatus>().Bind(plp, pl, p.displayName, 1.6f);   // live supply tag (W5.8)
                inst.AddComponent<MixFeedback>().Bind(plp);
            }
        }
    }

    /// Empty ChemLab prefabs have no liquid child mesh — their `_WithLiquid`
    /// twin carries the authored PharmaLiquid setup (fill bounds, precipitate
    /// renderer). Receiving vessels swap to the twin so poured liquid RENDERS
    /// (W5.8: "pouring into a beaker showed nothing").
    private GameObject VesselPrefabFor(string prefabName)
    {
        if (assets == null) return null;
        if (!prefabName.EndsWith("_WithLiquid"))
        {
            var twin = assets.GetPrefab(prefabName + "_WithLiquid");
            if (twin != null) return twin;
        }
        return assets.GetPrefab(prefabName);
    }

    private void BuildVessel(Transform stage, ExperimentLayout.Vessel v)
    {
        var prefab = VesselPrefabFor(v.prefabName);
        if (prefab == null) { Debug.LogWarning("[SceneBuilder] missing vessel prefab " + v.prefabName); return; }
        var inst = Instantiate(prefab, stage);
        inst.name = "Vessel_" + v.prefabName;   // keep the AUTHORED name (RealSizes/Mishandling lookups)
        Normalise(inst, v.prefabName, v.targetHeight);
        Seat(inst.transform, v.pos);
        PhysicsProfiles.EnsurePhysics(inst, v.prefabName);   // vessels stay kinematic (no release policy)
        GrabTuning.Apply(inst.GetComponent<XRGrab>());       // collide while held; re-freezes on release
        var lp = inst.GetComponent<LiquidPhysics>() ?? inst.AddComponent<LiquidPhysics>();
        lp.registry = registry;
        // ALWAYS set contents explicitly: the _WithLiquid twin serializes a
        // phantom half-fill, and a blank start must arm the wake-from-empty
        // branch (chem null + 0 ml) so the first pour adopts its chemical.
        var startChem = !string.IsNullOrEmpty(v.startChemical) ? assets.GetChemical(v.startChemical) : null;
        lp.SetContents(startChem, startChem != null ? lp.maxVolume * 0.3f : 0f);
        EnsureLiquidVisual(inst, lp);
        var bind = inst.AddComponent<LiquidTaskBinding>();
        bind.SetVesselAndRunner(lp, runner);
        foreach (var b in v.bindings)
        {
            var reagent = assets.GetChemical(b.reagentChemical);
            if (reagent != null) bind.AddExpected(reagent, b.taskId, b.requiredMl, b.completesTask);
        }
        inst.AddComponent<HazardousMixReactor>().Bind(lp, runner);   // bad-mix consequences
        var pl = inst.AddComponent<ProximityLabel>(); pl.SetLabel(v.displayName, 1.6f);
        inst.AddComponent<VesselStatus>().Bind(lp, pl, v.displayName, 1.6f);   // live "120 ml Ethanol" tag (W5.8)
        inst.AddComponent<MixFeedback>().Bind(lp);                             // reaction/mix/overflow popups (W5.8)
    }

    /// Guarantee the vessel/prop can RENDER its liquid: if LiquidPhysics has no
    /// mainRenderer (or it points at the glass shell, which lacks the PharmaLiquid
    /// fill properties), build an inset "Liquid" child running the liquid shader.
    /// sharedMaterial only — edit-mode safe (the suite drives the builder).
    public static void EnsureLiquidVisual(GameObject inst, LiquidPhysics lp)
    {
        if (inst == null || lp == null) return;
        if (lp.mainRenderer != null && lp.mainRenderer.sharedMaterial != null
            && lp.mainRenderer.sharedMaterial.HasProperty("_Fill")) return;   // authored setup (e.g. _WithLiquid twin)

        // Reuse an existing child named "Liquid" when present but unwired.
        Renderer liquidR = null;
        foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            if (r.name == "Liquid" && r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Fill")) { liquidR = r; break; }

        if (liquidR == null)
        {
            var shader = Shader.Find("PharmaSynth/Liquid");
            if (shader == null) return;   // shader stripped — leave numeric-only rather than magenta
            var b = WB(inst);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Liquid";
            var col = go.GetComponent<Collider>();
            if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
            go.transform.SetParent(inst.transform, true);
            // Inset cylinder ~72% of the glass footprint, floor to just under the rim.
            float w = Mathf.Min(b.size.x, b.size.z) * 0.72f;
            float h = Mathf.Max(0.01f, b.size.y * 0.86f);
            go.transform.position = new Vector3(b.center.x, b.min.y + h * 0.5f + b.size.y * 0.04f, b.center.z);
            go.transform.rotation = inst.transform.rotation;
            var ls = inst.transform.lossyScale;
            go.transform.localScale = new Vector3(
                w / Mathf.Max(1e-4f, Mathf.Abs(ls.x)),
                h * 0.5f / Mathf.Max(1e-4f, Mathf.Abs(ls.y)),   // cylinder mesh is 2 units tall
                w / Mathf.Max(1e-4f, Mathf.Abs(ls.z)));
            liquidR = go.GetComponent<Renderer>();
            liquidR.sharedMaterial = new Material(shader) { name = "PharmaLiquid_Runtime" };
            liquidR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        lp.mainRenderer = liquidR;
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

    private TextMeshPro MakeLabel(string text, Vector3 pos, float scale)
    {
        // Fall back to the stage when the scene's WorldLabels root isn't wired
        // (edit-mode builder tests) — stage teardown cleans those up anyway.
        var parent = labelsRoot != null ? labelsRoot : Stage();
        if (parent == null) return null;
        var go = new GameObject("DynLabel_" + text);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * scale;
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text; tmp.fontSize = 6f; tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white; tmp.fontStyle = FontStyles.Bold;
        // outlineWidth instances the font material — play-mode only (the edit-mode
        // suite builds stages too, and .material instancing errors there).
        if (Application.isPlaying)
        {
            tmp.outlineWidth = 0.25f; tmp.outlineColor = new Color32(6, 12, 22, 255);
        }
        go.AddComponent<FaceCamera>();
        return tmp;
    }
}
